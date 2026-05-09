using System;
using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace KindleToPDF
{
    /// <summary>
    /// Mac環境でのKindle操作を AppleScript ベースで実装するクラス（スタブ）
    /// </summary>
    public class MacAutomationLogic : IAutomationLogic
    {
        public IntPtr GetKindleWindow()
        {
            return new IntPtr(1);
        }

        public void BringWindowToFront(IntPtr hWnd)
        {
            RunAppleScript("tell application \"Kindle\" to activate");
        }

        public void SendPageTurn(IntPtr hWnd, bool isRightToLeft)
        {
            int keyCode = isRightToLeft ? 123 : 124; // 123: Left, 124: Right
            string script = $@"
                tell application ""Kindle"" to activate
                tell application ""System Events""
                    key code {keyCode}
                end tell
            ";
            RunAppleScript(script);
        }

        public void SendPrevPage(IntPtr hWnd, bool isRightToLeft)
        {
            int keyCode = isRightToLeft ? 124 : 123;
            RunAppleScript($"tell application \"System Events\" to key code {keyCode}");
        }

        public void SendNextPage(IntPtr hWnd, bool isRightToLeft)
        {
            SendPageTurn(hWnd, isRightToLeft);
        }

        public Rectangle GetWindowBounds(IntPtr hWnd)
        {
            // TODO: Macのウィンドウサイズ取得処理
            return new Rectangle(0, 0, 1920, 1080); // 仮の値
        }

        public Image<Rgba32> CaptureWindow(Rectangle bounds)
        {
            // TODO: screencapture コマンドで実装予定
            // 例: screencapture -x -R {x},{y},{w},{h} tmpfile.png → Image.Load<Rgba32>(tmpfile)
            return new Image<Rgba32>(bounds.Width, bounds.Height);
        }

        public Image<Rgba32> CropImage(Image<Rgba32> src, Rectangle cropRect)
        {
            var safeRect = new Rectangle(
                Math.Max(0, cropRect.X),
                Math.Max(0, cropRect.Y),
                Math.Min(src.Width - cropRect.X, cropRect.Width),
                Math.Min(src.Height - cropRect.Y, cropRect.Height)
            );

            if (safeRect.Width <= 0 || safeRect.Height <= 0)
            {
                return src.Clone();
            }

            return src.Clone(ctx => ctx.Crop(safeRect));
        }

        public bool AreImagesSame(Image<Rgba32> img1, Image<Rgba32> img2)
        {
            if (img1.Width != img2.Width || img1.Height != img2.Height) return false;

            for (int y = 0; y < img1.Height; y++)
            {
                var row1 = img1.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
                var row2 = img2.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
                for (int x = 0; x < img1.Width; x++)
                {
                    if (row1[x] != row2[x]) return false;
                }
            }
            return true;
        }

        public bool IsKeyDown(int vKey) { return false; }

        public string? GetBookTitleFromWindow(IntPtr hWnd) { return "Mac_Kindle_Book"; }
        public void SendHome(IntPtr hWnd) { /* AppleScript実装 */ }
        public void GoToLastPage(IntPtr hWnd) { /* AppleScript実装 */ }
        public void MaximizeKindleWindow(IntPtr hWnd) { /* AppleScript実装 */ }
        public void MinimizeKindleWindow(IntPtr hWnd) { /* AppleScript実装 */ }
        public void ToggleFullScreen(IntPtr hWnd) { /* AppleScript実装 */ }

        private void RunAppleScript(string script)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments = $"-e '{script.Replace("'", "'\\''")}'",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(processInfo)?.WaitForExit();
            }
            catch (Exception ex)
            {
                Logger.Error($"AppleScript execution failed: {ex.Message}", ex);
            }
        }
    }
}
