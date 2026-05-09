using System;
using System.Collections.Generic;
using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace KindleToPDF.Core
{
    public class PdfGenerator
    {
        public void CreatePdf(List<string> imagePaths, string outputPath, AppSettings settings)
        {
            if (imagePaths == null || imagePaths.Count == 0)
            {
                throw new Exception("PDFに変換する画像がありません。");
            }

            // PDFドキュメントの作成
            using var document = new PdfDocument();
            document.Info.Title = "KindleToPDF Document";

            foreach (var imgPath in imagePaths)
            {
                if (!File.Exists(imgPath)) continue;

                try
                {
                    // PdfSharp 6.x の機能を使ってPNGファイルを直接読み込む
                    using var image = XImage.FromFile(imgPath);
                    
                    // 新しいページを追加
                    var page = document.AddPage();
                    
                    // ページのサイズを、読み込んだ画像の実サイズ（ポイント単位）にぴったり合わせる
                    page.Width = image.PointWidth;
                    page.Height = image.PointHeight;

                    // ページに画像を描画
                    using var gfx = XGraphics.FromPdfPage(page);
                    gfx.DrawImage(image, 0, 0, page.Width, page.Height);
                }
                catch (Exception ex)
                {
                    Logger.Error($"画像のPDF追加中にエラー: {imgPath} - {ex.Message}");
                }
            }

            // 出力先のディレクトリが存在しない場合は作成
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // PDFをファイルとして保存
            document.Save(outputPath);
        }
    }
}
