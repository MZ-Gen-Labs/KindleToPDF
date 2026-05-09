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
        // ★【重要】お使いのKindleアプリの名前に変更してください（例: "Kindle Classic", "Amazon Kindle" など）
        private const string APP_NAME = "Kindle"; 

        public IntPtr GetKindleWindow()
        {
            string result = RunAppleScriptWithResult($"tell application \"System Events\" to exists process \"{APP_NAME}\"");
            return result.Trim().ToLower() == "true" ? new IntPtr(1) : IntPtr.Zero;
        }

        public Rectangle GetWindowBounds(IntPtr hWnd)
        {
            string script = $@"
            tell application ""System Events""
                tell process ""{APP_NAME}""
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
                    // --- 微調整オプション ---
                    // もし「タイトルバー（上のバー）」が写り込んでしまう場合は、
                    // 以下の titleBarHeight を 28〜40 程度の数値にしてY座標を下げてください。
                    int titleBarHeight = 0; 
                    
                    return new Rectangle(x, y + titleBarHeight, w, h - titleBarHeight);
                }
            }
            
            // 取得失敗時に固定サイズで撮るのをやめ、エラーを出して原因をわかりやすくします
            throw new Exception($"Kindleウィンドウの座標取得に失敗しました。(AppleScript結果: '{result}')\nAPP_NAME '{APP_NAME}' が実際のアプリ名と一致しているか確認してください。");
        }

        public void BringWindowToFront(IntPtr hWnd)
        {
            RunAppleScript($"tell application \"{APP_NAME}\" to activate");
        }

        public Image<Rgba32> CaptureWindow(Rectangle bounds)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"kindle_cap_{Guid.NewGuid()}.png");
            
            string[] args = { "-R", $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}", "-x", tempFile };
            RunCommand("screencapture", args);

            if (File.Exists(tempFile))
            {
                var img = Image.Load<Rgba32>(tempFile);
                File.Delete(tempFile); 
                return img;
            }
            
            throw new Exception("Macでのスクリーンショット取得に失敗しました。");
        }

        public void SendPageTurn(IntPtr hWnd, bool isRightToLeft)
        {
            int keyCode = isRightToLeft ? 123 : 124; 
            string script = $@"
                tell application ""{APP_NAME}"" to activate
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
                tell application ""{APP_NAME}"" to activate
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

        public bool IsKeyDown(int vKey) { return false; }
        
        public string? GetBookTitleFromWindow(IntPtr hWnd) 
        {
            string script = $"tell application \"System Events\" to tell process \"{APP_NAME}\" to get name of window 1";
            string title = RunAppleScriptWithResult(script).Trim();
            return string.IsNullOrEmpty(title) ? "Kindle_Book" : title;
        }

        public void SendHome(IntPtr hWnd) { }
        public void GoToLastPage(IntPtr hWnd) { }
        public void MaximizeKindleWindow(IntPtr hWnd) { }
        public void MinimizeKindleWindow(IntPtr hWnd) { }
        public void ToggleFullScreen(IntPtr hWnd) { }

        private void RunAppleScript(string script)
        {
            RunAppleScriptWithResult(script);
        }

        private string RunAppleScriptWithResult(string script)
        {
            try
            {
                return RunCommand("osascript", new[] { "-e", script });
            }
            catch (Exception ex)
            {
                Logger.Error($"AppleScript execution failed: {ex.Message}");
                return string.Empty;
            }
        }

        private string RunCommand(string command, string[] arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

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
