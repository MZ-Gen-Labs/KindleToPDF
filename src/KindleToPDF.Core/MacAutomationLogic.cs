using System;
using System.Diagnostics;
using System.Drawing;

namespace KindleToPDF
{
    public class MacAutomationLogic : IAutomationLogic
    {
        public IntPtr GetKindleWindow()
        {
            // Macではプロセス名で直接指定することが多いため、
            // IntPtr はダミー（1など）を返すか、Process IDを返します
            return new IntPtr(1); 
        }

        public void BringWindowToFront(IntPtr hWnd)
        {
            // AppleScriptでKindleを最前面に持ってくる
            RunAppleScript("tell application \"Kindle\" to activate");
        }

        public void SendPageTurn(IntPtr hWnd, bool isRightToLeft)
        {
            // isRightToLeftがtrue(日本語)なら左矢印、falseなら右矢印
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

        // --- 以下は実装が必要なメソッドのスタブ（枠組み） ---

        public Rectangle GetWindowBounds(IntPtr hWnd)
        {
            // TODO: Macのウィンドウサイズ取得処理
            // (AppleScriptで bounds of window 1 を取得するなど)
            return new Rectangle(0, 0, 1000, 800); // 仮の値
        }

        public Bitmap CaptureWindow(Rectangle bounds)
        {
            // TODO: Macでのスクリーンショット取得処理
            // 例: screencapture コマンドを実行して一時ファイルに保存し、それを読み込む
            return new Bitmap(1, 1); 
        }

        // 画像比較とクロップはWindowsと同じロジックを流用可能
        public Bitmap CropBitmap(Bitmap src, Rectangle cropRect)
        {
            Rectangle rect = new Rectangle(
                Math.Max(0, cropRect.X),
                Math.Max(0, cropRect.Y),
                Math.Min(src.Width - cropRect.X, cropRect.Width),
                Math.Min(src.Height - cropRect.Y, cropRect.Height)
            );

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return (Bitmap)src.Clone();
            }

            Bitmap target = new Bitmap(rect.Width, rect.Height);
            using (Graphics g = Graphics.FromImage(target))
            {
                g.DrawImage(src, new Rectangle(0, 0, target.Width, target.Height),
                            rect,
                            GraphicsUnit.Pixel);
            }
            return target;
        }

        public bool AreImagesSame(Bitmap img1, Bitmap img2)
        {
            // TODO: 実装が必要ならWindows版から移植
            return false;
        }
        
        public bool IsKeyDown(int vKey) { return false; } // Macでグローバルキーフックは権限上難しいため、通常はUIの停止ボタンを利用します
        
        public string? GetBookTitleFromWindow(IntPtr hWnd) { return "Mac_Kindle_Book"; }
        public void SendHome(IntPtr hWnd) { /* AppleScript実装 */ }
        public void GoToLastPage(IntPtr hWnd) { /* AppleScript実装 */ }
        public void MaximizeKindleWindow(IntPtr hWnd) { /* AppleScript実装 */ }
        public void MinimizeKindleWindow(IntPtr hWnd) { /* AppleScript実装 */ }
        public void ToggleFullScreen(IntPtr hWnd) { /* AppleScript実装 */ }

        // AppleScriptを実行するためのヘルパーメソッド
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
