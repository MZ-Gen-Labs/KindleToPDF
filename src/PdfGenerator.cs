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
    /// <summary>
    /// Handles PDF generation from captured images with various compression options
    /// </summary>
    public class PdfGenerator
    {
        /// <summary>
        /// Creates a PDF document from a list of image files
        /// </summary>
        /// <param name="imagePaths">List of image file paths</param>
        /// <param name="outputPdfPath">Output PDF file path</param>
        /// <param name="dpi">Target DPI (0 for default)</param>
        /// <param name="colorMode">Image color mode for compression</param>
        /// <param name="format">Image format (Jpeg or Png)</param>
        /// <param name="monochromeThreshold">Threshold for monochrome conversion (0-255)</param>
        public void CreatePdf(List<string> imagePaths, string outputPdfPath, double dpi = 0, ImageColorMode colorMode = ImageColorMode.FullColor, int jpegQuality = 80, PdfImageFormat format = PdfImageFormat.Jpeg, int monochromeThreshold = 128)
        {
            try
            {
                Logger.Info($"CreatePdf: Start. Files={imagePaths.Count}, Out={outputPdfPath}, Mode={colorMode}, Format={format}, Threshold={monochromeThreshold}");
                
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
                            // Process image based on color mode and format
                            processedImagePath = ProcessImage(imagePath, colorMode, jpegQuality, format, monochromeThreshold);
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
                            Logger.Error($"Error processing page {imagePath}: {ex.Message}", ex);
                        }
                        finally
                        {
                            // Clean up processed image if it's a temp file
                            if (isTempFile && File.Exists(processedImagePath))
                            {
                                try 
                                { 
                                    File.Delete(processedImagePath); 
                                } 
                                catch (Exception ex) 
                                { 
                                    Logger.Warning($"Failed to delete temp file {processedImagePath}: {ex.Message}");
                                }
                            }
                        }
                    }

                    document.Save(outputPdfPath);
                    Logger.Info($"PDF saved successfully: {outputPdfPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Critical error in CreatePdf: {ex.Message}", ex);
                throw;
            }
        }

        private string ProcessImage(string imagePath, ImageColorMode colorMode, int jpegQuality, PdfImageFormat format, int monochromeThreshold)
        {
            // If full color and PNG format, no processing needed (assuming input is PNG or compatible)
            // But we might want to ensure it is PNG if format is PNG.
            // For simplicity, if FullColor and format matches input, return input.
            // However, input is likely PNG. If format is Jpeg, we must convert.
            
            if (colorMode == ImageColorMode.FullColor && format == PdfImageFormat.Png)
                return imagePath;

            using (Bitmap original = new Bitmap(imagePath))
            {
                Bitmap? processed = null;

                try
                {
                    switch (colorMode)
                    {
                        case ImageColorMode.Monochrome:
                            processed = ConvertToMonochrome(original, monochromeThreshold);
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
                        default: // FullColor
                             // If we are here, it means we need to convert to JPEG (FullColor + Jpeg)
                             // We need a copy of original to save as JPEG
                             processed = new Bitmap(original);
                             break;
                    }

                    // Save processed image
                    string tempPath = Path.Combine(Path.GetTempPath(), $"processed_{Guid.NewGuid()}");
                    
                    // Use JPEG for color/high color IF format is Jpeg
                    if ((colorMode == ImageColorMode.HighColor || colorMode == ImageColorMode.FullColor) && format == PdfImageFormat.Jpeg)
                    {
                        tempPath += ".jpg";
                        if (processed != null)
                        {
                            SaveAsJpeg(processed, tempPath, jpegQuality);
                        }
                    }
                    else
                    {
                        tempPath += ".png";
                        processed?.Save(tempPath, ImageFormat.Png);
                    }

                    return tempPath;
                }
                finally
                {
                    if (processed != null) processed.Dispose();
                }
            }
        }

        /// <summary>
        /// Converts an image to monochrome (1-bit) using thresholding
        /// </summary>
        private Bitmap ConvertToMonochrome(Bitmap original, int threshold)
        {
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
                        int gray = (int)(r * Constants.RED_WEIGHT + g * Constants.GREEN_WEIGHT + b * Constants.BLUE_WEIGHT);

                        if (gray > threshold)
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

        /// <summary>
        /// Converts an image to grayscale (8-bit)
        /// </summary>
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

                        destBuffer[destIdx] = (byte)(r * Constants.RED_WEIGHT + g * Constants.GREEN_WEIGHT + b * Constants.BLUE_WEIGHT);
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

        /// <summary>
        /// Converts an image to 256-color indexed format
        /// </summary>
        private Bitmap ConvertToIndexed256(Bitmap original)
        {
            return original.Clone(new Rectangle(0, 0, original.Width, original.Height), PixelFormat.Format8bppIndexed);
        }

        /// <summary>
        /// Converts an image to high color (16-bit)
        /// </summary>
        private Bitmap ConvertToHighColor(Bitmap original)
        {
            return original.Clone(new Rectangle(0, 0, original.Width, original.Height), PixelFormat.Format16bppRgb565);
        }

        private void SaveAsJpeg(Bitmap image, string path, int quality)
        {
            EncoderParameters encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            ImageCodecInfo? jpegCodec = GetEncoderInfo("image/jpeg");
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
