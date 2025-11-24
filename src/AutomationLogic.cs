using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Automation;

namespace KindleToPDF
{
    public class AutomationLogic
    {
        // Win32 API Imports
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        private const uint SPI_GETFOREGROUNDLOCKTIMEOUT = 0x2000;
        private const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;
        private const int SPIF_SENDCHANGE = 0x2;

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public Point ptMinPosition;
            public Point ptMaxPosition;
            public RECT rcNormalPosition;
        }

        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOW = 5;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const int VK_LEFT = 0x25;
        private const int VK_RIGHT = 0x27;
        private const int VK_HOME = 0x24;
        private const int VK_NEXT = 0x22; // PageDown
        private const int VK_F11 = 0x7A;
        private const int SW_RESTORE = 9;
        private const int SW_MINIMIZE = 6;

        private Action<string>? _logCallback;

        public void SetLogCallback(Action<string> logCallback)
        {
            _logCallback = logCallback;
        }

        private void Log(string message)
        {
            if (_logCallback != null)
                _logCallback(message);
            else
                Debug.WriteLine(message);
        }

        public IntPtr GetKindleWindow()
        {
            // Process.MainWindowHandle returns IntPtr.Zero for minimized windows
            // Use EnumWindows to find Kindle window regardless of state
            Process[] processes = Process.GetProcessesByName("Kindle");
            Log($"GetKindleWindow: Found {processes.Length} Kindle processes");
            
            if (processes.Length == 0)
                return IntPtr.Zero;

            uint kindleProcessId = (uint)processes[0].Id;
            Log($"GetKindleWindow: Kindle process ID = {kindleProcessId}");
            
            IntPtr foundWindow = IntPtr.Zero;
            int windowCount = 0;

            EnumWindows((hWnd, lParam) =>
            {
                GetWindowThreadProcessId(hWnd, out uint processId);
                if (processId == kindleProcessId)
                {
                    // Check if this is a visible window (not a child window)
                    System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                    int length = GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString();
                    
                    if (length > 0 && title.Contains("Kindle"))
                    {
                        foundWindow = hWnd;
                        return false; // Stop enumeration
                    }
                }
                return true; // Continue enumeration
            }, IntPtr.Zero);

            Log($"GetKindleWindow: Checked {windowCount} windows for process {kindleProcessId}, found={foundWindow}");
            return foundWindow;
        }

        public Rectangle GetWindowBounds(IntPtr hWnd)
        {
            if (GetWindowRect(hWnd, out RECT rect))
            {
                return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            }
            return Rectangle.Empty;
        }

        public void SendKey(IntPtr hWnd, int key)
        {
            PostMessage(hWnd, WM_KEYDOWN, (IntPtr)key, IntPtr.Zero);
            Thread.Sleep(50);
            PostMessage(hWnd, WM_KEYUP, (IntPtr)key, IntPtr.Zero);
        }

        public void SendHome(IntPtr hWnd)
        {
            // "^{HOME}" didn't work.
            // User suggested Ctrl+G -> 1 -> Enter
            
            // Send Ctrl+G
            SendKeys.SendWait("^g");
            
            // Wait for dialog to appear (500ms should be enough)
            Thread.Sleep(500);
            
            // Send "1"
            SendKeys.SendWait("1");
            
            // Send Enter
            SendKeys.SendWait("{ENTER}");
        }

        public void ToggleFullScreen(IntPtr hWnd)
        {
            // SendKeys.SendWait is more reliable after window is restored and brought to front
            SendKeys.SendWait("{F11}");
        }

        public void SendPrevPage(IntPtr hWnd, bool isRightToLeft)
        {
            // Right-to-Left (JP): Prev page is RIGHT Arrow
            // Left-to-Right (EN): Prev page is LEFT Arrow
            int key = isRightToLeft ? VK_RIGHT : VK_LEFT;
            SendKey(hWnd, key);
        }

        public void SendNextPage(IntPtr hWnd, bool isRightToLeft)
        {
            SendPageTurn(hWnd, isRightToLeft);
        }

        public void SendPageTurn(IntPtr hWnd, bool isRightToLeft)
        {
            // Right-to-Left (JP): Next page is LEFT Arrow
            // Left-to-Right (EN): Next page is RIGHT Arrow
            int key = isRightToLeft ? VK_LEFT : VK_RIGHT;
            SendKey(hWnd, key);
        }

        public Bitmap CaptureWindow(Rectangle bounds)
        {
            Bitmap bmp = new Bitmap(bounds.Width, bounds.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            }
            return bmp;
        }

        public bool AreImagesSame(Bitmap img1, Bitmap img2)
        {
            if (img1 == null || img2 == null) return false;
            if (img1.Width != img2.Width || img1.Height != img2.Height) return false;

            // Simple pixel comparison (checking center and corners for speed, or full scan)
            // For robustness, let's do a stride-based comparison or a sampled comparison
            // Here is a reasonably fast full comparison using LockBits
            
            try
            {
                BitmapData data1 = img1.LockBits(new Rectangle(0, 0, img1.Width, img1.Height), ImageLockMode.ReadOnly, img1.PixelFormat);
                BitmapData data2 = img2.LockBits(new Rectangle(0, 0, img2.Width, img2.Height), ImageLockMode.ReadOnly, img2.PixelFormat);

                int bytes = Math.Abs(data1.Stride) * img1.Height;
                byte[] buffer1 = new byte[bytes];
                byte[] buffer2 = new byte[bytes];

                Marshal.Copy(data1.Scan0, buffer1, 0, bytes);
                Marshal.Copy(data2.Scan0, buffer2, 0, bytes);

                img1.UnlockBits(data1);
                img2.UnlockBits(data2);

                for (int i = 0; i < bytes; i++)
                {
                    if (buffer1[i] != buffer2[i]) return false;
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
            return (GetAsyncKeyState(vKey) & 0x8000) != 0;
        }

        public Bitmap CropBitmap(Bitmap src, Rectangle cropRect)
        {
            // Ensure cropRect is within src bounds
            Rectangle rect = new Rectangle(
                Math.Max(0, cropRect.X),
                Math.Max(0, cropRect.Y),
                Math.Min(src.Width - cropRect.X, cropRect.Width),
                Math.Min(src.Height - cropRect.Y, cropRect.Height)
            );

            if (rect.Width <= 0 || rect.Height <= 0) return (Bitmap)src.Clone();

            Bitmap target = new Bitmap(rect.Width, rect.Height);
            using (Graphics g = Graphics.FromImage(target))
            {
                g.DrawImage(src, new Rectangle(0, 0, target.Width, target.Height),
                            rect,
                            GraphicsUnit.Pixel);
            }
            return target;
        }

        public void BringWindowToFront(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            // Check window placement to see if minimized
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            GetWindowPlacement(hWnd, ref placement);

            bool isMinimized = placement.showCmd == 2; // SW_SHOWMINIMIZED = 2

            if (isMinimized || IsIconic(hWnd))
            {
                Log("BringWindowToFront: Window is minimized, restoring...");
                ShowWindow(hWnd, SW_RESTORE);
                Thread.Sleep(500); 
                ShowWindow(hWnd, SW_SHOW); // Ensure it's shown
                Thread.Sleep(200);
            }

            // Use SwitchToThisWindow which is often more effective than SetForegroundWindow
            // for bringing windows to front
            SwitchToThisWindow(hWnd, true);
            Thread.Sleep(100);

            // Verify if it worked
            IntPtr foreground = GetForegroundWindow();
            if (foreground != hWnd)
            {
                Log($"BringWindowToFront: SwitchToThisWindow didn't fully activate (Foreground={foreground}). Retrying with AttachThreadInput...");
                
                // Fallback to the AttachThreadInput method if SwitchToThisWindow didn't make it the foreground window
                uint foregroundThreadId = GetWindowThreadProcessId(foreground, out _);
                uint appThreadId = GetCurrentThreadId();
                uint targetThreadId = GetWindowThreadProcessId(hWnd, out _);

                if (foregroundThreadId != targetThreadId)
                {
                    AttachThreadInput(foregroundThreadId, appThreadId, true);
                    AttachThreadInput(targetThreadId, appThreadId, true);

                    SetForegroundWindow(hWnd);
                    
                    AttachThreadInput(foregroundThreadId, appThreadId, false);
                    AttachThreadInput(targetThreadId, appThreadId, false);
                }
                else
                {
                    SetForegroundWindow(hWnd);
                }
            }
            
            // Final check and attempt
            if (GetForegroundWindow() != hWnd)
            {
                 Log("BringWindowToFront: Still not foreground. Trying Alt key trick...");
                 keybd_event(0, 0, 0, 0);
                 SwitchToThisWindow(hWnd, true);
            }
        }

        public string? GetBookTitleFromWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;

            // Restore if minimized to get full window title
            bool wasMinimized = IsIconic(hWnd);
            if (wasMinimized)
            {
                Log("GetBookTitleFromWindow: Window is minimized, restoring...");
                ShowWindow(hWnd, SW_RESTORE);
                Thread.Sleep(800); // Wait longer for window to fully restore and title to update
                Log("GetBookTitleFromWindow: Window restored");
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            int length = GetWindowText(hWnd, sb, sb.Capacity);
            
            // Restore minimized state if it was minimized
            if (wasMinimized)
            {
                ShowWindow(hWnd, SW_MINIMIZE);
            }
            
            if (length == 0) return null;

            string windowTitle = sb.ToString();
            Log($"GetBookTitleFromWindow: Window title = '{windowTitle}'");
            
            // Extract book title from "Kindle for PC [device] - [book title]"
            // Find first occurrence of " - " and take everything after it
            int separatorIndex = windowTitle.IndexOf(" - ");
            if (separatorIndex > 0 && separatorIndex < windowTitle.Length - 3)
            {
                string bookTitle = windowTitle.Substring(separatorIndex + 3).Trim();
                
                // Replace invalid filename characters with underscore
                char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
                foreach (char c in invalidChars)
                {
                    bookTitle = bookTitle.Replace(c, '_');
                }
                
                Log($"GetBookTitleFromWindow: Extracted book title = '{bookTitle}'");
                return bookTitle;
            }

            Log($"GetBookTitleFromWindow: Could not extract book title from '{windowTitle}'");
            return null;
        }
        public int GetTotalPageCount(IntPtr kindleWnd)
        {
            // 1. Open "Go to" dialog
            BringWindowToFront(kindleWnd);
            SendKeys.SendWait("^g");
            Thread.Sleep(800); // Wait for dialog

            // 2. Find the dialog
            // We can try to find the dialog window using UIA
            // The dialog usually has a specific title like "移動..." or "Go to..."
            // Or we can just search for a new window under the Kindle process/root
            
            try
            {
                AutomationElement root = AutomationElement.RootElement;
                // Optimization: We could scope this to the Kindle window if we had its AutomationElement, 
                // but FromHandle might be safer.
                
                // Let's try to find the dialog. It should be a Window control type.
                // Since we don't know the exact title (lang dependent), we might look for a modal window 
                // or just the focused window?
                
                // Strategy: Get the focused element. It should be the text box in the dialog.
                AutomationElement focused = AutomationElement.FocusedElement;
                if (focused == null) return -1;

                // The focused element is likely the TextBox ("位置No.").
                // We need to find the label next to it, or the parent window and then the label.
                
                AutomationElement dialog = focused;
                while (dialog != null && dialog.Current.ControlType != ControlType.Window)
                {
                    dialog = TreeWalker.ControlViewWalker.GetParent(dialog);
                }
                
                if (dialog == null) return -1;

                // Now search for text elements in this dialog
                Condition condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text);
                AutomationElementCollection textElements = dialog.FindAll(TreeScope.Descendants, condition);

                foreach (AutomationElement element in textElements)
                {
                    string name = element.Current.Name;
                    // Look for format like "/ 136" or "/136"
                    if (name.Contains("/"))
                    {
                        string[] parts = name.Split('/');
                        if (parts.Length > 1)
                        {
                            string numberPart = parts[1].Trim();
                            // Remove any non-digit chars just in case
                            string digits = new string(Array.FindAll(numberPart.ToCharArray(), char.IsDigit));
                            if (int.TryParse(digits, out int total))
                            {
                                // Close dialog
                                SendKeys.SendWait("{ESC}");
                                return total;
                            }
                        }
                    }
                }
                
                // If parsing failed, close dialog
                SendKeys.SendWait("{ESC}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UIA Error: " + ex.Message);
                SendKeys.SendWait("{ESC}"); // Ensure closed
            }

            return -1;
        }

        public void GoToLastPage(IntPtr kindleWnd)
        {
            int totalPages = GetTotalPageCount(kindleWnd);
            if (totalPages > 0)
            {
                Thread.Sleep(500); // Wait for dialog to close completely
                
                // Open dialog again
                BringWindowToFront(kindleWnd);
                SendKeys.SendWait("^g");
                Thread.Sleep(500);
                
                // Input last page
                SendKeys.SendWait(totalPages.ToString());
                SendKeys.SendWait("{ENTER}");
            }
        }
    }
}
