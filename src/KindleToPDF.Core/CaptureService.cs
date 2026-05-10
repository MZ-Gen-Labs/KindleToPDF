using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using Rectangle = SixLabors.ImageSharp.Rectangle;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace KindleToPDF.Core
{
    public class CaptureService
    {
        private readonly IAutomationLogic _automation;
        private readonly AppSettings _settings;

        // UI側に状態を通知するためのイベント
        public event Action<string>? OnLog;
        public event Action<string>? OnPageCaptured;
        public event Action? OnLastPageDetected;

        public CaptureService(IAutomationLogic automation, AppSettings settings)
        {
            _automation = automation;
            _settings = settings;
        }

        public async Task RunCaptureAsync(IntPtr hWnd, string tempDir, int startIndex, CancellationToken token)
        {
            Image<Rgba32>? previousImage = null;
            const int VK_DELETE = 0x2E;
            bool isRightToLeft = _settings.PageDirection == 0;
            int maxPages = _settings.PageCount;
            bool stopAtLast = _settings.StopAtLastPage;
            int interval = _settings.Interval;
            bool autoDetect = _settings.AutoDetect;

            for (int i = startIndex; i < maxPages || stopAtLast; i++)
            {
                if (_automation.IsKeyDown(VK_DELETE)) break;
                token.ThrowIfCancellationRequested();

                _automation.BringWindowToFront(hWnd);
                await Task.Delay(1000, token); // スペース切り替え等の待機
                
                Rectangle bounds = _automation.GetWindowBounds(hWnd);
                if (bounds.Width <= 0 || bounds.Height <= 0) throw new Exception("Invalid window bounds");

                Image<Rgba32> rawImage = _automation.CaptureWindow(bounds);
                Image<Rgba32> currentImage;

                // クロップ処理
                if (_settings.CropRect.Width > 0 && _settings.CropRect.Height > 0)
                {
                    int relX = _settings.CropRect.X - bounds.X;
                    int relY = _settings.CropRect.Y - bounds.Y;
                    Rectangle relativeCrop = new Rectangle(relX, relY, _settings.CropRect.Width, _settings.CropRect.Height);
                    currentImage = _automation.CropImage(rawImage, relativeCrop);
                    rawImage.Dispose();
                }
                else
                {
                    currentImage = rawImage;
                }

                // 最終ページ判定
                if (stopAtLast && previousImage != null)
                {
                    if (_automation.AreImagesSame(previousImage, currentImage))
                    {
                        OnLog?.Invoke("Last page detected (no change). Stopping.");
                        currentImage.Dispose();
                        OnLastPageDetected?.Invoke();
                        break;
                    }
                }

                if (previousImage != null) previousImage.Dispose();
                previousImage = currentImage.Clone();

                // 画像保存
                string imgPath = Path.Combine(tempDir, $"page_{i:D4}.png");
                await currentImage.SaveAsPngAsync(imgPath, token);

                OnPageCaptured?.Invoke(imgPath);
                OnLog?.Invoke($"Captured page {i + 1}");

                currentImage.Dispose();

                if (!stopAtLast && i >= maxPages - 1) break;

                // ページめくり
                _automation.SendPageTurn(hWnd, isRightToLeft);

                if (autoDetect)
                {
                    bool pageChanged = false;
                    int maxRetries = 40;
                    int stableCount = 0;
                    Image<Rgba32>? lastCheck = null;

                    for (int r = 0; r < maxRetries; r++)
                    {
                        await Task.Delay(100, token);
                        if (_automation.IsKeyDown(VK_DELETE)) { break; }
                        token.ThrowIfCancellationRequested();

                        Image<Rgba32> currentCheck = _automation.CaptureWindow(bounds);

                        if (lastCheck != null)
                        {
                            if (_automation.AreImagesSame(lastCheck, currentCheck))
                                stableCount++;
                            else
                                stableCount = 0;
                            lastCheck.Dispose();
                        }
                        lastCheck = currentCheck;

                        if (stableCount >= 2)
                        {
                            if (!_automation.AreImagesSame(previousImage, currentCheck))
                            {
                                pageChanged = true;
                                lastCheck.Dispose();
                                lastCheck = null;
                                break;
                            }
                        }
                    }
                    if (lastCheck != null) lastCheck.Dispose();

                    if (!pageChanged)
                        OnLog?.Invoke("Warning: Page turn not detected (timeout or no change).");
                }
                else
                {
                    for (int t = 0; t < interval; t += 100)
                    {
                        await Task.Delay(Math.Min(100, interval - t), token);
                        if (_automation.IsKeyDown(VK_DELETE)) { break; }
                        token.ThrowIfCancellationRequested();
                    }
                }
            }
            if (previousImage != null) previousImage.Dispose();
        }
    }
}
