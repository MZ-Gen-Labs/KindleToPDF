using System;
using System.Diagnostics;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Rectangle = SixLabors.ImageSharp.Rectangle;

namespace KindleToPDF.Core
{
    public class MacAutomationLogic : IAutomationLogic
    {
        public IntPtr GetKindleWindow()
        {
            // Macではプロセス名ベースで操作するため、ダミーのハンドル(1)を返して「見つかった」ことにします
            string result = RunAppleScriptWithResult("tell application \"System Events\" to exists process \"Kindle\"");
            return result.Trim().ToLower() == "true" ? new IntPtr(1) : IntPtr.Zero;
        }

        public Rectangle GetWindowBounds(IntPtr hWnd)
        {
            // AppleScriptでKindleのウィンドウ座標とサイズを取得
            string script = @"
            tell application ""System Events""
                tell process ""Kindle""
                    set pos to position of window 1
                    set sz to size of window 1
                    return (item 1 of pos) & "","" & (item 2 of pos) & "","" & (item 1 of sz) & "","" & (item 2 of sz)
                end tell
            end tell";
            
            string result = RunAppleScriptWithResult(script).Trim();
            
            if (!string.IsNullOrEmpty(result))
            {
                var parts = result.Split(',');
                if (parts.Length == 4 && 
                    int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y) &&
                    int.TryParse(parts[2], out int w) && int.TryParse(parts[3], out int h))
                {
                    return new Rectangle(x, y, w, h);
                }
            }
            return new Rectangle(0, 0, 800, 600); // 取得失敗時のフォールバック
        }

        public void BringWindowToFront(IntPtr hWnd)
        {
            RunAppleScript("tell application \"Kindle\" to activate");
        }

        public Image<Rgba32> CaptureWindow(Rectangle bounds)
        {
            // Macの screencapture コマンドを使用して一時ファイルに保存
            string tempFile = Path.Combine(Path.GetTempPath(), $"kindle_cap_{Guid.NewGuid()}.png");
            
            // 文字列結合ではなく、配列を使って安全に引数を構築する
            string[] args = { "-R", $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}", "-x", tempFile };
            RunCommand("screencapture", args);

            if (File.Exists(tempFile))
            {
                var img = Image.Load<Rgba32>(tempFile);
                File.Delete(tempFile); // 読み込み終わったら削除
                return img;
            }
            
            throw new Exception("Macでのスクリーンショット取得に失敗しました。");
        }

        public void SendPageTurn(IntPtr hWnd, bool isRightToLeft)
        {
            // isRightToLeftがtrue(日本語・右開き)なら左矢印(123)、falseなら右矢印(124)
            int keyCode = isRightToLeft ? 123 : 124; 
            
            string script = $@"
                tell application ""Kindle"" to activate
                delay 0.3
                tell application ""System Events""
                    key code {keyCode}
                end tell
            ";
            RunAppleScript(script);
        }

        public void SendPrevPage(IntPtr hWnd, bool isRightToLeft)
        {
            int keyCode = isRightToLeft ? 124 : 123;
            string script = $@"
                tell application ""Kindle"" to activate
                delay 0.3
                tell application ""System Events""
                    key code {keyCode}
                end tell
            ";
            RunAppleScript(script);
        }

        public void SendNextPage(IntPtr hWnd, bool isRightToLeft)
        {
            SendPageTurn(hWnd, isRightToLeft);
        }

        public Image<Rgba32> CropImage(Image<Rgba32> src, Rectangle cropRect)
        {
            var safeRect = new Rectangle(
                Math.Max(0, cropRect.X),
                Math.Max(0, cropRect.Y),
                Math.Min(src.Width - cropRect.X, cropRect.Width),
                Math.Min(src.Height - cropRect.Y, cropRect.Height)
            );

            if (safeRect.Width <= 0 || safeRect.Height <= 0) return src.Clone();
            return src.Clone(ctx => ctx.Crop(safeRect));
        }

        public bool AreImagesSame(Image<Rgba32> img1, Image<Rgba32> img2)
        {
            if (img1 == null || img2 == null) return false;
            if (img1.Width != img2.Width || img1.Height != img2.Height) return false;

            try
            {
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
            catch
            {
                return false;
            }
        }

        public bool IsKeyDown(int vKey) 
        {
            // MacではC#からグローバルなキーボードフックを取得するのが非常に難しいため、
            // 停止は「Avalonia画面のStopボタン」で行う運用とし、ここは一旦 false を返します。
            return false; 
        }
        
        public string? GetBookTitleFromWindow(IntPtr hWnd) 
        {
            string script = "tell application \"System Events\" to tell process \"Kindle\" to get name of window 1";
            string title = RunAppleScriptWithResult(script).Trim();
            return string.IsNullOrEmpty(title) ? "Kindle_Book" : title;
        }

        public void SendHome(IntPtr hWnd) { /* Mac用ショートカットの実装 */ }
        public void GoToLastPage(IntPtr hWnd) { /* Mac用ショートカットの実装 */ }
        public void MaximizeKindleWindow(IntPtr hWnd) { /* Mac用の最大化処理 */ }
        public void MinimizeKindleWindow(IntPtr hWnd) { /* Mac用の最小化処理 */ }
        public void ToggleFullScreen(IntPtr hWnd) { /* Mac用のフルスクリーン処理 */ }

        // --- ヘルパーメソッド ---

        private void RunAppleScript(string script)
        {
            RunAppleScriptWithResult(script);
        }

        private string RunAppleScriptWithResult(string script)
        {
            try
            {
                // シングルクォートで囲むハックをやめ、配列として渡す
                return RunCommand("osascript", new[] { "-e", script });
            }
            catch (Exception ex)
            {
                Logger.Error($"AppleScript execution failed: {ex.Message}");
                return string.Empty;
            }
        }

        // 第2引数を string から string[] に変更
        private string RunCommand(string command, string[] arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // .NET Core推奨の ArgumentList を使って安全に引数を追加
            foreach (var arg in arguments)
            {
                processInfo.ArgumentList.Add(arg);
            }

            using var process = Process.Start(processInfo);
            process?.WaitForExit();
            return process?.StandardOutput.ReadToEnd() ?? string.Empty;
        }
    }
}
