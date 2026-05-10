using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace KindleToPDF.Core
{
    public class CaptureService
    {
        private readonly IAutomationLogic _automation;
        private readonly AppSettings _settings;

        public event Action<string>? OnLog;
        public event Action<string>? OnPageCaptured;

        public CaptureService(IAutomationLogic automation, AppSettings settings)
        {
            _automation = automation;
            _settings = settings;
        }

        public async Task RunCaptureAsync(IntPtr hWnd, string outputDir, int startPage, CancellationToken ct)
        {
            _automation.BringWindowToFront(hWnd);
            await Task.Delay(1000, ct); // スペース切り替え待ち

            var bounds = _automation.GetWindowBounds(hWnd);
            Image<Rgba32>? lastImage = null;
            int pageIndex = 0;

            for (int i = 0; i < _settings.PageCount; i++)
            {
                if (ct.IsCancellationRequested) break;

                // 1. キャプチャ
                using var currentFullImage = _automation.CaptureWindow(bounds);
                
                // 2. クロップ処理
                var processedImage = ApplyCrop(currentFullImage);

                // 3. 重複チェック（最終ページ判定）
                if (_settings.StopAtLastPage && lastImage != null)
                {
                    if (_automation.AreImagesSame(lastImage, processedImage))
                    {
                        OnLog?.Invoke("最終ページを検出しました。停止します。");
                        processedImage.Dispose();
                        break;
                    }
                }

                // 前回の画像を更新
                lastImage?.Dispose();
                lastImage = processedImage.Clone();

                // 4. カラーモード変換・見開き分割・保存
                ProcessAndSaveImage(processedImage, outputDir, ref pageIndex);

                OnLog?.Invoke($"Captured page {i + 1}");

                // 5. ページめくり
                _automation.SendNextPage(hWnd, _settings.PageDirection == 0);
                await Task.Delay(_settings.Interval, ct);
            }

            lastImage?.Dispose();
        }

        private Image<Rgba32> ApplyCrop(Image<Rgba32> source)
        {
            // UIで設定した CropRect が 0 でなければ切り抜く
            if (_settings.CropRect.Width > 0 && _settings.CropRect.Height > 0)
            {
                return _automation.CropImage(source, _settings.CropRect);
            }
            return source.Clone();
        }

        private void ProcessAndSaveImage(Image<Rgba32> image, string outputDir, ref int pageIndex)
        {
            // 見開き分割がONの場合
            if (_settings.SplitDualPage)
            {
                int mid = image.Width / 2;
                
                // 右開き(和書)なら 右->左 の順、左開き(洋書)なら 左->右 の順
                if (_settings.PageDirection == 0) // 右開き
                {
                    SaveSingleImage(image.Clone(ctx => ctx.Crop(new Rectangle(mid, 0, image.Width - mid, image.Height))), outputDir, ref pageIndex);
                    SaveSingleImage(image.Clone(ctx => ctx.Crop(new Rectangle(0, 0, mid, image.Height))), outputDir, ref pageIndex);
                }
                else // 左開き
                {
                    SaveSingleImage(image.Clone(ctx => ctx.Crop(new Rectangle(0, 0, mid, image.Height))), outputDir, ref pageIndex);
                    SaveSingleImage(image.Clone(ctx => ctx.Crop(new Rectangle(mid, 0, image.Width - mid, image.Height))), outputDir, ref pageIndex);
                }
            }
            else
            {
                SaveSingleImage(image.Clone(), outputDir, ref pageIndex);
            }
        }

        private void SaveSingleImage(Image<Rgba32> image, string outputDir, ref int pageIndex)
        {
            using (image)
            {
                // カラーモード変換
                ApplyColorMode(image);

                // ファイル名生成 (連番)
                string fileName = string.Format($"page_{{0:D{_settings.NumberDigits}}}.{(_settings.ImageFormat == PdfImageFormat.Jpeg ? "jpg" : "png")}", 
                                                _settings.StartNumber + pageIndex);
                string fullPath = Path.Combine(outputDir, fileName);

                // フォーマットに応じて保存
                if (_settings.ImageFormat == PdfImageFormat.Jpeg)
                {
                    image.SaveAsJpeg(fullPath, new JpegEncoder { Quality = _settings.JpegQuality });
                }
                else
                {
                    image.SaveAsPng(fullPath);
                }

                OnPageCaptured?.Invoke(fullPath);
                pageIndex++;
            }
        }

        private void ApplyColorMode(Image<Rgba32> image)
        {
            switch (_settings.ColorMode)
            {
                case ImageColorMode.Monochrome:
                    // 白黒2値化 (しきい値をUIから反映)
                    float threshold = _settings.MonochromeThreshold / 255f;
                    image.Mutate(x => x.BinaryThreshold(threshold));
                    break;
                case ImageColorMode.Grayscale:
                    image.Mutate(x => x.Grayscale());
                    break;
                // 他のモード（Indexedなど）はImageSharpの標準フィルタで対応
            }
        }
    }
}
