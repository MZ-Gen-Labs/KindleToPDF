using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace KindleToPDF
{
    public class PdfGenerator
    {
        public void CreatePdf(List<string> imagePaths, string outputPdfPath, double dpi = 0)
        {
            using (PdfDocument document = new PdfDocument())
            {
                document.Info.Title = "Kindle Capture";

                foreach (string imagePath in imagePaths)
                {
                    if (!File.Exists(imagePath)) continue;

                    PdfPage page = document.AddPage();
                    using (XImage image = XImage.FromFile(imagePath))
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

                document.Save(outputPdfPath);
            }
        }
    }
}
