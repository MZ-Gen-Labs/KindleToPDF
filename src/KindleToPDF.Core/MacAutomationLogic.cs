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
            // 改行を含む複雑なスクリプトをやめ、ターミナルで成功したシンプルな1行コマンドを2回実行する
            try
            {
                string posStr = RunAppleScriptWithResult($"tell application \"System Events\" to tell process \"{APP_NAME}\" to get position of window 1").Trim();
                string szStr = RunAppleScriptWithResult($"tell application \"System Events\" to tell process \"{APP_NAME}\" to get size of window 1").Trim();

                if (!string.IsNullOrEmpty(posStr) && !string.IsNullOrEmpty(szStr))
                {
                    var posParts = posStr.Split(',');
                    var szParts = szStr.Split(',');

                    if (posParts.Length == 2 && szParts.Length == 2 &&
                        int.TryParse(posParts[0], out int x) && int.TryParse(posParts[1], out int y) &&
                        int.TryParse(szParts[0], out int w) && int.TryParse(szParts[1], out int h))
                    {
                        // --- 微調整オプション ---
                        // タイトルバーなどが写り込む場合は、ここの数値を 30 などに増やしてください
                        int titleBarHeight = 0; 
                        
                        return new Rectangle(x, y + titleBarHeight, w, h - titleBarHeight);
                    }
                }
                
                throw new Exception($"座標パース失敗: pos='{posStr}', sz='{szStr}'");
            }
            catch (Exception ex)
            {
                throw new Exception($"Kindleウィンドウの座標取得に失敗しました。\n詳細: {ex.Message}");
            }
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
                // エラーを握りつぶさずにそのまま投げる
                throw new Exception(ex.Message);
            }
        }

        private string RunCommand(string command, string[] arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true, // ★追加：裏で起きたエラーメッセージも拾う
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in arguments)
            {
                processInfo.ArgumentList.Add(arg);
            }

            using var process = Process.Start(processInfo);
            process?.WaitForExit();
            
            // エラー出力があった場合は例外として投げる
            string error = process?.StandardError.ReadToEnd() ?? string.Empty;
            if (!string.IsNullOrEmpty(error))
            {
                throw new Exception(error.Trim());
            }

            return process?.StandardOutput.ReadToEnd() ?? string.Empty;
        }
    }
}
