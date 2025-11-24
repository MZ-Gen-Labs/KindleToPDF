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
    /// <summary>
    /// Handles automation of Kindle for PC window interactions
    /// </summary>
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

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Gets the main window handle of the Kindle for PC application
        /// </summary>
        /// <returns>Window handle, or IntPtr.Zero if not found</returns>
        public IntPtr GetKindleWindow()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(Constants.KINDLE_PROCESS_NAME);
                if (processes.Length > 0)
                {
                    return processes[0].MainWindowHandle;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get Kindle window: {ex.Message}", ex);
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Gets the screen bounds of a window
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        /// <returns>Rectangle representing window bounds, or Rectangle.Empty if failed</returns>
        public Rectangle GetWindowBounds(IntPtr hWnd)
        {
            if (GetWindowRect(hWnd, out RECT rect))
            {
                return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            }
            Logger.Warning($"Failed to get window bounds for handle {hWnd}");
            return Rectangle.Empty;
        }

        /// <summary>
        /// Sends a key press to the specified window
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        /// <param name="key">Virtual key code</param>
        public void SendKey(IntPtr hWnd, int key)
        {
            PostMessage(hWnd, Constants.WM_KEYDOWN, (IntPtr)key, IntPtr.Zero);
            Thread.Sleep(Constants.KEY_PRESS_DELAY_MS);
            PostMessage(hWnd, Constants.WM_KEYUP, (IntPtr)key, IntPtr.Zero);
        }

        /// <summary>
        /// Navigates to the first page using Ctrl+G dialog
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        public void SendHome(IntPtr hWnd)
        {
            try
            {
                SendKeys.SendWait("^g");
                Thread.Sleep(Constants.DIALOG_WAIT_MS);
                SendKeys.SendWait("1");
                SendKeys.SendWait("{ENTER}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to send Home command: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Toggles full screen mode (F11)
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        public void ToggleFullScreen(IntPtr hWnd)
        {
            SendKey(hWnd, Constants.VK_F11);
        }

        /// <summary>
        /// Sends previous page command
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        /// <param name="isRightToLeft">True for right-to-left reading direction (Japanese)</param>
        public void SendPrevPage(IntPtr hWnd, bool isRightToLeft)
        {
            int key = isRightToLeft ? Constants.VK_RIGHT : Constants.VK_LEFT;
            SendKey(hWnd, key);
        }

        /// <summary>
        /// Sends next page command
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        /// <param name="isRightToLeft">True for right-to-left reading direction (Japanese)</param>
        public void SendNextPage(IntPtr hWnd, bool isRightToLeft)
        {
            SendPageTurn(hWnd, isRightToLeft);
        }

        /// <summary>
        /// Sends page turn command (next page)
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        /// <param name="isRightToLeft">True for right-to-left reading direction (Japanese)</param>
        public void SendPageTurn(IntPtr hWnd, bool isRightToLeft)
        {
            int key = isRightToLeft ? Constants.VK_LEFT : Constants.VK_RIGHT;
            SendKey(hWnd, key);
        }

        /// <summary>
        /// Captures a screenshot of the specified screen area
        /// </summary>
        /// <param name="bounds">Screen area to capture</param>
        /// <returns>Bitmap of the captured area</returns>
        public Bitmap CaptureWindow(Rectangle bounds)
        {
            Bitmap bmp = new Bitmap(bounds.Width, bounds.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            }
            return bmp;
        }

        /// <summary>
        /// Compares two images for equality using pixel-by-pixel comparison
        /// </summary>
        /// <param name="img1">First image</param>
        /// <param name="img2">Second image</param>
        /// <returns>True if images are identical</returns>
        public bool AreImagesSame(Bitmap img1, Bitmap img2)
        {
            if (img1 == null || img2 == null) return false;
            if (img1.Width != img2.Width || img1.Height != img2.Height) return false;

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
            catch (Exception ex)
            {
                Logger.Error($"Error comparing images: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Checks if a key is currently pressed
        /// </summary>
        /// <param name="vKey">Virtual key code</param>
        /// <returns>True if key is pressed</returns>
        public bool IsKeyDown(int vKey)
        {
            return (GetAsyncKeyState(vKey) & 0x8000) != 0;
        }

        /// <summary>
        /// Crops a bitmap to the specified rectangle
        /// </summary>
        /// <param name="src">Source bitmap</param>
        /// <param name="cropRect">Crop rectangle</param>
        /// <returns>Cropped bitmap</returns>
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
                Logger.Warning("Invalid crop rectangle, returning clone of original");
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

        /// <summary>
        /// Brings a window to the foreground, restoring it if minimized
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        public void BringWindowToFront(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            
            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, Constants.SW_RESTORE);
                Thread.Sleep(Constants.WINDOW_RESTORE_DELAY_MS);
            }
            
            SetForegroundWindow(hWnd);
        }

        /// <summary>
        /// Maximizes the Kindle window
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        public void MaximizeKindleWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                Logger.Warning("Cannot maximize window: invalid handle");
                return;
            }

            // First restore if minimized
            if (IsIconic(hWnd))
            {
                Logger.Info("Window is minimized, restoring first...");
                ShowWindow(hWnd, Constants.SW_RESTORE);
                Thread.Sleep(500); // Longer delay for restore
                
                // Bring to foreground after restore
                SetForegroundWindow(hWnd);
                Thread.Sleep(300); // Additional delay to ensure window is active
            }

            // Maximize the window
            ShowWindow(hWnd, Constants.SW_MAXIMIZE);
            Thread.Sleep(Constants.WINDOW_RESTORE_DELAY_MS);
            
            // Bring to foreground to ensure it's active
            SetForegroundWindow(hWnd);
            Thread.Sleep(100); // Final delay to ensure focus
            
            Logger.Info("Kindle window maximized");
        }

        /// <summary>
        /// Minimizes the Kindle window
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        public void MinimizeKindleWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                Logger.Warning("Cannot minimize window: invalid handle");
                return;
            }

            ShowWindow(hWnd, Constants.SW_MINIMIZE);
            Logger.Info("Kindle window minimized");
        }

        /// <summary>
        /// Extracts the book title from the Kindle window title
        /// </summary>
        /// <param name="hWnd">Window handle</param>
        /// <returns>Book title with invalid filename characters replaced, or null if not found</returns>
        public string? GetBookTitleFromWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;

            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
                int length = GetWindowText(hWnd, sb, sb.Capacity);
                
                if (length == 0) return null;

                string windowTitle = sb.ToString();
                
                // Extract book title from "Kindle for PC [device] - [book title]"
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
                    
                    return bookTitle;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to get book title: {ex.Message}", ex);
            }

            return null;
        }
        /// <summary>
        /// Gets the total page count from the Kindle "Go to" dialog
        /// </summary>
        /// <param name="kindleWnd">Kindle window handle</param>
        /// <returns>Total page count, or -1 if failed</returns>
        public int GetTotalPageCount(IntPtr kindleWnd)
        {
            try
            {
                BringWindowToFront(kindleWnd);
                SendKeys.SendWait("^g");
                Thread.Sleep(800);

                AutomationElement root = AutomationElement.RootElement;
                AutomationElement focused = AutomationElement.FocusedElement;
                if (focused == null)
                {
                    Logger.Warning("No focused element found when trying to get page count");
                    SendKeys.SendWait("{ESC}");
                    return -1;
                }

                AutomationElement dialog = focused;
                while (dialog != null && dialog.Current.ControlType != ControlType.Window)
                {
                    dialog = TreeWalker.ControlViewWalker.GetParent(dialog);
                }
                
                if (dialog == null)
                {
                    Logger.Warning("Could not find dialog window");
                    SendKeys.SendWait("{ESC}");
                    return -1;
                }

                Condition condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text);
                AutomationElementCollection textElements = dialog.FindAll(TreeScope.Descendants, condition);

                foreach (AutomationElement element in textElements)
                {
                    string name = element.Current.Name;
                    if (name.Contains("/"))
                    {
                        string[] parts = name.Split('/');
                        if (parts.Length > 1)
                        {
                            string numberPart = parts[1].Trim();
                            string digits = new string(Array.FindAll(numberPart.ToCharArray(), char.IsDigit));
                            if (int.TryParse(digits, out int total))
                            {
                                SendKeys.SendWait("{ESC}");
                                Logger.Info($"Total page count detected: {total}");
                                return total;
                            }
                        }
                    }
                }
                
                SendKeys.SendWait("{ESC}");
                Logger.Warning("Could not parse page count from dialog");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting total page count: {ex.Message}", ex);
                try { SendKeys.SendWait("{ESC}"); } catch { }
            }

            return -1;
        }

        /// <summary>
        /// Navigates to the last page of the book
        /// </summary>
        /// <param name="kindleWnd">Kindle window handle</param>
        public void GoToLastPage(IntPtr kindleWnd)
        {
            try
            {
                int totalPages = GetTotalPageCount(kindleWnd);
                if (totalPages > 0)
                {
                    Thread.Sleep(Constants.DIALOG_WAIT_MS);
                    
                    BringWindowToFront(kindleWnd);
                    SendKeys.SendWait("^g");
                    Thread.Sleep(Constants.DIALOG_WAIT_MS);
                    
                    SendKeys.SendWait(totalPages.ToString());
                    SendKeys.SendWait("{ENTER}");
                    Logger.Info($"Navigated to last page: {totalPages}");
                }
                else
                {
                    Logger.Warning("Could not navigate to last page - total page count unknown");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error navigating to last page: {ex.Message}", ex);
            }
        }
    }
}
