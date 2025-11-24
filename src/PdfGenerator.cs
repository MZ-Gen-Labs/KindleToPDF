using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace KindleToPDF
{
    public class PdfGenerator
    {
        public void CreatePdf(List<string> imagePaths, string outputPdfPath, double dpi = 0, ImageColorMode colorMode = ImageColorMode.FullColor, int jpegQuality = 80)
        {
            try
            {
                File.AppendAllText("debug_log.txt", $"CreatePdf: Start. Files={imagePaths.Count}, Out={outputPdfPath}, Mode={colorMode}\n");
                
                using (PdfDocument document = new PdfDocument())
                {
                    document.Info.Title = "Kindle Capture";
                    
                    // Enable PDF compression
                    document.Options.CompressContentStreams = true;
                    document.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;

                    foreach (string imagePath in imagePaths)
                    {
                        if (!File.Exists(imagePath)) continue;

                        string processedImagePath = imagePath;
                        bool isTempFile = false;

                        try
                        {
                            // Process image based on color mode
                            processedImagePath = ProcessImage(imagePath, colorMode, jpegQuality);
                            if (processedImagePath != imagePath) isTempFile = true;

                            PdfPage page = document.AddPage();
                            using (XImage image = XImage.FromFile(processedImagePath))
                            {
                                // If DPI is specified, adjust the size
                                if (dpi > 0)
                                {
                                    // PDF points = (pixels / dpi) * 72
                                    page.Width = XUnit.FromPoint((image.PixelWidth / dpi) * 72);
                                    page.Height = XUnit.FromPoint((image.PixelHeight / dpi) * 72);
                                }
                                else
                                {
                                    // Use image's internal DPI or default
                                    page.Width = XUnit.FromPoint(image.PointWidth);
                                    page.Height = XUnit.FromPoint(image.PointHeight);
                                }

                                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                                {
                                    gfx.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText("debug_log.txt", $"ERROR processing page {imagePath}: {ex.Message}\n{ex.StackTrace}\n");
                        }
                        finally
                        {
                            // Clean up processed image if it's a temp file
                            if (isTempFile && File.Exists(processedImagePath))
                            {
                                try { File.Delete(processedImagePath); } catch { }
                            }
                        }
                    }

                    document.Save(outputPdfPath);
                    File.AppendAllText("debug_log.txt", "CreatePdf: Saved successfully.\n");
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText("debug_log.txt", $"CRITICAL ERROR in CreatePdf: {ex.Message}\n{ex.StackTrace}\n");
                throw; // Rethrow to let UI handle/show it
            }
        }

        private string ProcessImage(string imagePath, ImageColorMode colorMode, int jpegQuality)
        {
            // If full color, no processing needed
            if (colorMode == ImageColorMode.FullColor)
                return imagePath;

            using (Bitmap original = new Bitmap(imagePath))
            {
                Bitmap? processed = null;

                try
                {
                    switch (colorMode)
                    {
                        case ImageColorMode.Monochrome:
                            processed = ConvertToMonochrome(original);
                            break;
                        case ImageColorMode.Grayscale:
                            processed = ConvertToGrayscale(original);
                            break;
                        case ImageColorMode.Indexed256:
                            processed = ConvertToIndexed256(original);
                            break;
                        case ImageColorMode.HighColor:
                            processed = ConvertToHighColor(original);
                            break;
                        default:
                            return imagePath;
                    }

                    // Save processed image
                    string tempPath = Path.Combine(Path.GetTempPath(), $"processed_{Guid.NewGuid()}");
                    
                    // Use JPEG for color/high color, PNG for indexed
                    if (colorMode == ImageColorMode.HighColor || colorMode == ImageColorMode.FullColor)
                    {
                        tempPath += ".jpg";
                        SaveAsJpeg(processed, tempPath, jpegQuality);
                    }
                    else
                    {
                        tempPath += ".png";
                        processed.Save(tempPath, ImageFormat.Png);
                    }

                    return tempPath;
                }
                finally
                {
                    if (processed != null) processed.Dispose();
                }
            }
        }

        private Bitmap ConvertToMonochrome(Bitmap original)
        {
            // Thresholding
            Bitmap bmp = new Bitmap(original.Width, original.Height, PixelFormat.Format1bppIndexed);
            
            // Lock Source as 24bpp for easy reading
            BitmapData dataSrc = original.LockBits(new Rectangle(0, 0, original.Width, original.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            // Lock Dest
            BitmapData dataDest = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);

            try
            {
                int height = original.Height;
                int width = original.Width;
                int srcStride = dataSrc.Stride;
                int destStride = dataDest.Stride;
                
                int srcBytes = Math.Abs(srcStride) * height;
                int destBytes = Math.Abs(destStride) * height;
                
                byte[] srcBuffer = new byte[srcBytes];
                byte[] destBuffer = new byte[destBytes]; // Initialized to 0 (Black)

                Marshal.Copy(dataSrc.Scan0, srcBuffer, 0, srcBytes);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Get Gray
                        int srcIdx = y * srcStride + x * 3;
                        byte b = srcBuffer[srcIdx];
                        byte g = srcBuffer[srcIdx + 1];
                        byte r = srcBuffer[srcIdx + 2];
                        int gray = (int)(r * 0.299 + g * 0.587 + b * 0.114);

                        // Threshold
                        if (gray > 128)
                        {
                            // Set bit to 1 (White)
                            int destIdx = y * destStride + (x >> 3); // x / 8
                            byte mask = (byte)(0x80 >> (x & 7)); // 0x80 >> (x % 8)
                            destBuffer[destIdx] |= mask;
                        }
                    }
                }

                Marshal.Copy(destBuffer, 0, dataDest.Scan0, destBytes);
            }
            finally
            {
                original.UnlockBits(dataSrc);
                bmp.UnlockBits(dataDest);
            }

            return bmp;
        }

        private Bitmap ConvertToGrayscale(Bitmap original)
        {
            Bitmap bmp = new Bitmap(original.Width, original.Height, PixelFormat.Format8bppIndexed);
            
            // Set Grayscale Palette
            ColorPalette palette = bmp.Palette;
            for (int i = 0; i < 256; i++) palette.Entries[i] = Color.FromArgb(i, i, i);
            bmp.Palette = palette;

            BitmapData dataSrc = original.LockBits(new Rectangle(0, 0, original.Width, original.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            BitmapData dataDest = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);

            try
            {
                int height = original.Height;
                int width = original.Width;
                int srcStride = dataSrc.Stride;
                int destStride = dataDest.Stride;
                
                int srcBytes = Math.Abs(srcStride) * height;
                int destBytes = Math.Abs(destStride) * height;

                byte[] srcBuffer = new byte[srcBytes];
                byte[] destBuffer = new byte[destBytes];

                Marshal.Copy(dataSrc.Scan0, srcBuffer, 0, srcBytes);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int srcIdx = y * srcStride + x * 3;
                        int destIdx = y * destStride + x;

                        byte b = srcBuffer[srcIdx];
                        byte g = srcBuffer[srcIdx + 1];
                        byte r = srcBuffer[srcIdx + 2];

                        destBuffer[destIdx] = (byte)(r * 0.299 + g * 0.587 + b * 0.114);
                    }
                }

                Marshal.Copy(destBuffer, 0, dataDest.Scan0, destBytes);
            }
            finally
            {
                original.UnlockBits(dataSrc);
                bmp.UnlockBits(dataDest);
            }
            
            return bmp;
        }

        private Bitmap ConvertToIndexed256(Bitmap original)
        {
            // GDI+ Clone to 8bppIndexed uses a default palette (usually halftone).
            // This is safe (no Graphics used on indexed image) and simplest for now.
            // If higher quality is needed, an octree quantizer would be required.
            return original.Clone(new Rectangle(0, 0, original.Width, original.Height), PixelFormat.Format8bppIndexed);
        }

        private Bitmap ConvertToHighColor(Bitmap original)
        {
            return original.Clone(new Rectangle(0, 0, original.Width, original.Height), PixelFormat.Format16bppRgb565);
        }

        private void SaveAsJpeg(Bitmap image, string path, int quality)
        {
            EncoderParameters encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");
            if (jpegCodec != null)
            {
                image.Save(path, jpegCodec, encoderParams);
            }
            else
            {
                image.Save(path, ImageFormat.Jpeg);
            }
        }

        private ImageCodecInfo? GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.MimeType == mimeType)
                    return codec;
            }
            return null;
        }
    }
}
