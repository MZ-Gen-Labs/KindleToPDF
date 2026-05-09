using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp;
using Rectangle = SixLabors.ImageSharp.Rectangle;
using Point = SixLabors.ImageSharp.Point;
using Image = SixLabors.ImageSharp.Image;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

#if WINDOWS
using System.Windows.Forms;
using System.Windows.Automation;
#endif

namespace KindleToPDF
{
    /// <summary>
    /// Handles automation of Kindle for PC window interactions
    /// </summary>
#if WINDOWS
    public class WindowsAutomationLogic : IAutomationLogic
#else
    public class WindowsAutomationLogic : IAutomationLogic
#endif
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
        static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenIcon(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public Point ptMinPosition;
            public Point ptMaxPosition;
            public Rectangle rcNormalPosition;
        }

        private const int SC_MAXIMIZE = 0xF030;
        private const int SC_RESTORE = 0xF120;
        private const uint WM_SYSCOMMAND = 0x0112;

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
                    Process kindleProcess = processes[0];
                    uint kindleProcessId = (uint)kindleProcess.Id;
                    
                    List<IntPtr> kindleWindows = new List<IntPtr>();

                    // Enumerate all windows belonging to Kindle process
                    EnumWindows((hWnd, lParam) =>
                    {
                        GetWindowThreadProcessId(hWnd, out uint processId);
                        if (processId == kindleProcessId)
                        {
                            kindleWindows.Add(hWnd);
                        }
                        return true;
                    }, IntPtr.Zero);

                    // Find the main Kindle window (Qt5QWindowIcon with title containing "Kindle")
                    foreach (IntPtr hWnd in kindleWindows)
                    {
                        System.Text.StringBuilder className = new System.Text.StringBuilder(256);
                        GetClassName(hWnd, className, className.Capacity);
                        
                        System.Text.StringBuilder title = new System.Text.StringBuilder(256);
                        GetWindowText(hWnd, title, title.Capacity);

                        // Look for Qt5QWindowIcon window with "for PC" in title (the main window)
                        if (className.ToString() == "Qt5QWindowIcon" && 
                            title.ToString().Contains("for PC"))
                        {
                            Logger.Info($"Found Kindle main window: {hWnd}, Title: {title}");
                            return hWnd;
                        }
                    }

                    // Fallback to MainWindowHandle if not found
                    Logger.Warning("Could not find Qt5QWindowIcon Kindle window, using MainWindowHandle");
                    return kindleProcess.MainWindowHandle;
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
                // SendKeys.SendWait("^g");
                Thread.Sleep(Constants.DIALOG_WAIT_MS);
                // SendKeys.SendWait("1");
                // SendKeys.SendWait("{ENTER}");
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
        /// ※ CopyFromScreen は System.Drawing 依存のため、ImageSharp 対応版への書き換えが必要です
        /// </summary>
        public Image<Rgba32> CaptureWindow(Rectangle bounds)
        {
            // System.Drawingをフルネームで呼び出し、ImageSharpとの競合を回避
            using (var bmp = new System.Drawing.Bitmap(bounds.Width, bounds.Height))
            {
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, new System.Drawing.Size(bounds.Width, bounds.Height));
                }
                
                using (var ms = new System.IO.MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    return Image.Load<Rgba32>(ms); // ImageSharpの型に変換して返す
                }
            }
        }

        /// <summary>
        /// Compares two images for equality using pixel-by-pixel comparison
        /// </summary>
        /// <param name="img1">First image</param>
        /// <param name="img2">Second image</param>
        /// <returns>True if images are identical</returns>
        public bool AreImagesSame(Image<Rgba32> img1, Image<Rgba32> img2)
        {
            if (img1 == null || img2 == null) return false;
            if (img1.Width != img2.Width || img1.Height != img2.Height) return false;

            // ピクセル直接比較 (ImageSharp)
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
                Logger.Warning("Invalid crop rectangle, returning clone of original");
                return src.Clone();
            }

            return src.Clone(ctx => ctx.Crop(safeRect));
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
            // Default to Strategy 1 (Async Restore -> Wait -> Maximize) as it's the most robust theoretical fix
            MaximizeStrategy_AsyncRestoreWait(hWnd);
        }

        // Strategy 1: Async Restore -> Wait -> Maximize
        public void MaximizeStrategy_AsyncRestoreWait(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            if (IsIconic(hWnd))
            {
                Logger.Info("Strategy 1: Window is minimized, restoring async...");
                ShowWindowAsync(hWnd, Constants.SW_RESTORE);
                
                // Wait for restore
                int retries = 0;
                while (IsIconic(hWnd) && retries < 10)
                {
                    Thread.Sleep(200);
                    retries++;
                }
            }

            Logger.Info("Strategy 1: Maximizing async...");
            ShowWindowAsync(hWnd, Constants.SW_MAXIMIZE);
            SetForegroundWindow(hWnd);
        }

        // Strategy 2: Sync Restore -> Wait -> Maximize (Original approach)
        public void MaximizeStrategy_SyncRestoreWait(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            if (IsIconic(hWnd))
            {
                Logger.Info("Strategy 2: Window is minimized, restoring sync...");
                ShowWindow(hWnd, Constants.SW_RESTORE);
                Thread.Sleep(500);
                SetForegroundWindow(hWnd);
                Thread.Sleep(300);
            }

            Logger.Info("Strategy 2: Maximizing sync...");
            ShowWindow(hWnd, Constants.SW_MAXIMIZE);
            Thread.Sleep(Constants.WINDOW_RESTORE_DELAY_MS);
            SetForegroundWindow(hWnd);
        }

        // Strategy 3: Direct Maximize (Async) - The one that failed previously
        public void MaximizeStrategy_DirectAsync(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Strategy 3: Direct Maximize Async...");
            ShowWindowAsync(hWnd, Constants.SW_MAXIMIZE);
            SetForegroundWindow(hWnd);
        }

        // Strategy 4: SetWindowPlacement
        public void MaximizeStrategy_SetWindowPlacement(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Strategy 4: SetWindowPlacement...");

            GetWindowPlacement(hWnd, out WINDOWPLACEMENT placement);
            placement.showCmd = Constants.SW_MAXIMIZE;
            placement.length = Marshal.SizeOf(placement);
            
            SetWindowPlacement(hWnd, ref placement);
            SetForegroundWindow(hWnd);
        }

        // Strategy 5: SendMessage (SC_MAXIMIZE)
        public void MaximizeStrategy_SendMessage(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Strategy 5: SendMessage SC_MAXIMIZE...");

            if (IsIconic(hWnd))
            {
                ShowWindowAsync(hWnd, Constants.SW_RESTORE);
                Thread.Sleep(500);
            }

            PostMessage(hWnd, WM_SYSCOMMAND, (IntPtr)SC_MAXIMIZE, IntPtr.Zero);
            SetForegroundWindow(hWnd);
        }

        // Strategy 6: SwitchToThisWindow
        public void MaximizeStrategy_SwitchToThisWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Strategy 6: SwitchToThisWindow...");

            SwitchToThisWindow(hWnd, true);
            Thread.Sleep(200);
            ShowWindowAsync(hWnd, Constants.SW_MAXIMIZE);
        }

        // Strategy 7: SendKeys (Alt+Space -> x) - Keyboard simulation
        public void MaximizeStrategy_SendKeys_AltSpaceX(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Strategy 7: SendKeys Alt+Space -> x...");

            // Bring window to front first
            SetForegroundWindow(hWnd);
            Thread.Sleep(300);

            try
            {
                // Send Alt+Space to open window menu, then 'x' for maximize
                // SendKeys.SendWait("% ");  // Alt+Space
                Thread.Sleep(200);
                // SendKeys.SendWait("x");   // Maximize (Japanese: 最大化)
                Logger.Info("Strategy 7: Sent Alt+Space -> x");
            }
            catch (Exception ex)
            {
                Logger.Error($"Strategy 7 failed: {ex.Message}", ex);
            }
        }

        // Strategy 8: Restore Only (No Maximize) - Diagnostic test
        public void MaximizeStrategy_RestoreOnly(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Strategy 8: Restore Only (diagnostic)...");

            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, Constants.SW_RESTORE);
                Logger.Info("Strategy 8: Window restored (no maximize)");
            }
            else
            {
                Logger.Info("Strategy 8: Window was not minimized");
            }
        }

        // Strategy 9: Direct SW_MAXIMIZE from minimized (Task Manager style)
        public void MaximizeStrategy_DirectMaximizeFromMinimized(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Strategy 9: Direct SW_MAXIMIZE (TaskManager style)...");

            // Direct maximize without any restore or SetForegroundWindow
            ShowWindow(hWnd, Constants.SW_MAXIMIZE);
            Logger.Info("Strategy 9: Called ShowWindow(SW_MAXIMIZE) directly");
        }

        // Strategy 10: Diagnostic - Check Window State
        public void MaximizeStrategy_Diagnostic(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Strategy 10: Diagnostic Window State Check...");

            bool isIconic = IsIconic(hWnd);
            Logger.Info($"IsIconic: {isIconic}");

            GetWindowPlacement(hWnd, out WINDOWPLACEMENT placement);
            Logger.Info($"showCmd: {placement.showCmd}");
            Logger.Info($"SW_HIDE=0, SW_SHOWNORMAL=1, SW_SHOWMINIMIZED=2, SW_SHOWMAXIMIZED=3, SW_SHOWNOACTIVATE=4, SW_SHOW=5, SW_MINIMIZE=6, SW_SHOWMINNOACTIVE=7, SW_SHOWNA=8, SW_RESTORE=9");
            
            Rectangle bounds = GetWindowBounds(hWnd);
            Logger.Info($"Window bounds: {bounds}");
        }

        // Strategy 11: Using GetWindowPlacement to restore
        public void MaximizeStrategy_UseWindowPlacement(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Strategy 11: Using GetWindowPlacement...");

            GetWindowPlacement(hWnd, out WINDOWPLACEMENT placement);
            Logger.Info($"Current showCmd: {placement.showCmd}");

            // If minimized (showCmd == 2 or 6), restore first
            if (placement.showCmd == 2 || placement.showCmd == 6)
            {
                Logger.Info("Window is minimized, restoring via SetWindowPlacement...");
                placement.showCmd = Constants.SW_RESTORE;
                placement.length = Marshal.SizeOf(placement);
                SetWindowPlacement(hWnd, ref placement);
                Thread.Sleep(500);
            }

            // Now maximize
            Logger.Info("Maximizing via SetWindowPlacement...");
            GetWindowPlacement(hWnd, out placement);
            placement.showCmd = Constants.SW_MAXIMIZE;
            placement.length = Marshal.SizeOf(placement);
            SetWindowPlacement(hWnd, ref placement);
        }

        // Strategy 12: Handle Hidden Window (bounds = 0,0,0,0)
        public void MaximizeStrategy_ShowThenMaximize(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Strategy 12: Show then Maximize (for hidden windows)...");

            Rectangle bounds = GetWindowBounds(hWnd);
            Logger.Info($"Current bounds: {bounds}");

            // If window is hidden (bounds = 0,0,0,0), show it first
            if (bounds.Width == 0 || bounds.Height == 0)
            {
                Logger.Info("Window appears hidden, showing with SW_SHOWNA...");
                ShowWindow(hWnd, Constants.SW_SHOWNA); // SW_SHOWNA = 8 (show without activating)
                Thread.Sleep(300);
                
                bounds = GetWindowBounds(hWnd);
                Logger.Info($"Bounds after SW_SHOWNA: {bounds}");
            }

            // Now try to maximize using ShowWindowAsync
            Logger.Info("Attempting maximize with ShowWindowAsync...");
            ShowWindowAsync(hWnd, Constants.SW_MAXIMIZE);
            Thread.Sleep(300);
            SetForegroundWindow(hWnd);
        }

        // Strategy 13: Show with SW_SHOW then maximize
        public void MaximizeStrategy_ShowActiveThenMaximize(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Strategy 13: SW_SHOW then Maximize...");

            Rectangle bounds = GetWindowBounds(hWnd);
            Logger.Info($"Current bounds: {bounds}");

            if (bounds.Width == 0 || bounds.Height == 0)
            {
                Logger.Info("Window appears hidden, showing with SW_SHOW...");
                ShowWindow(hWnd, Constants.SW_SHOW); // SW_SHOW = 5
                Thread.Sleep(500);
                
                bounds = GetWindowBounds(hWnd);
                Logger.Info($"Bounds after SW_SHOW: {bounds}");
            }

            Logger.Info("Attempting maximize with ShowWindowAsync...");
            ShowWindowAsync(hWnd, Constants.SW_MAXIMIZE);
            Thread.Sleep(300);
            SetForegroundWindow(hWnd);
        }

        // Strategy 14: Show window and resize to screen size (avoid SW_MAXIMIZE)
        public void MaximizeStrategy_ManualResize(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Strategy 14: Manual Resize to Screen Size...");

            Rectangle bounds = GetWindowBounds(hWnd);
            Logger.Info($"Current bounds: {bounds}");

            // Show window first if hidden
            if (bounds.Width == 0 || bounds.Height == 0)
            {
                Logger.Info("Window appears hidden, showing with SW_SHOW...");
                ShowWindow(hWnd, Constants.SW_SHOW);
                Thread.Sleep(500);
            }

            // Get screen size
            System.Drawing.Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            Logger.Info($"Screen bounds: {screenBounds}");

            // Resize window to screen size using MoveWindow
            Logger.Info("Resizing window to screen size with MoveWindow...");
            bool result = MoveWindow(hWnd, 0, 0, screenBounds.Width, screenBounds.Height, true);
            Logger.Info($"MoveWindow result: {result}");
            
            Thread.Sleep(300);
            SetForegroundWindow(hWnd);
        }

        // Strategy 15: Show window and resize using SetWindowPos
        public void MaximizeStrategy_SetWindowPosResize(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Strategy 15: SetWindowPos Resize...");

            Rectangle bounds = GetWindowBounds(hWnd);
            Logger.Info($"Current bounds: {bounds}");

            // Show window first if hidden
            if (bounds.Width == 0 || bounds.Height == 0)
            {
                Logger.Info("Window appears hidden, showing with SW_SHOW...");
                ShowWindow(hWnd, Constants.SW_SHOW);
                Thread.Sleep(500);
            }

            // Get screen size
            System.Drawing.Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            Logger.Info($"Screen bounds: {screenBounds}");

            // SetWindowPos flags
            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_SHOWWINDOW = 0x0040;

            // Resize window using SetWindowPos
            Logger.Info("Resizing window with SetWindowPos...");
            bool result = SetWindowPos(hWnd, IntPtr.Zero, 0, 0, screenBounds.Width, screenBounds.Height, SWP_NOZORDER | SWP_SHOWWINDOW);
            Logger.Info($"SetWindowPos result: {result}");
            
            Thread.Sleep(300);
            SetForegroundWindow(hWnd);
        }

        // ===== RESTORE-ONLY STRATEGIES (No Maximize) =====
        
        // Strategy 16: Check IsWindowVisible + SW_SHOW
        public void RestoreStrategy_IsWindowVisible(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Restore 16: IsWindowVisible check...");

            bool isVisible = IsWindowVisible(hWnd);
            Logger.Info($"IsWindowVisible: {isVisible}");

            if (!isVisible)
            {
                Logger.Info("Window is not visible, showing with SW_SHOW...");
                ShowWindow(hWnd, Constants.SW_SHOW);
                Thread.Sleep(500);
            }

            SetForegroundWindow(hWnd);
            Logger.Info("Restore 16: Complete");
        }

        // Strategy 17: OpenIcon (専用の復元関数)
        public void RestoreStrategy_OpenIcon(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Restore 17: OpenIcon...");

            bool result = OpenIcon(hWnd);
            Logger.Info($"OpenIcon result: {result}");
            
            Thread.Sleep(300);
            SetForegroundWindow(hWnd);
        }

        // Strategy 18: SendMessage WM_SYSCOMMAND SC_RESTORE (タスクバークリック相当)
        public void RestoreStrategy_SendMessageRestore(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Restore 18: SendMessage WM_SYSCOMMAND SC_RESTORE...");

            SendMessage(hWnd, WM_SYSCOMMAND, (IntPtr)SC_RESTORE, IntPtr.Zero);
            Logger.Info("Sent SC_RESTORE message");
            
            Thread.Sleep(300);
            SetForegroundWindow(hWnd);
        }

        // Strategy 19: SW_SHOWNORMAL (Web検索推奨)
        public void RestoreStrategy_ShowNormal(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Restore 19: SW_SHOWNORMAL...");

            ShowWindow(hWnd, 1); // SW_SHOWNORMAL = 1
            Logger.Info("Called ShowWindow(SW_SHOWNORMAL)");
            
            Thread.Sleep(300);
            SetForegroundWindow(hWnd);
        }

        // Strategy 20: Combination - IsWindowVisible + OpenIcon
        public void RestoreStrategy_VisibleCheckThenOpenIcon(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Restore 20: IsWindowVisible + OpenIcon...");

            bool isVisible = IsWindowVisible(hWnd);
            Logger.Info($"IsWindowVisible: {isVisible}");

            if (!isVisible)
            {
                Logger.Info("Window not visible, calling OpenIcon...");
                bool result = OpenIcon(hWnd);
                Logger.Info($"OpenIcon result: {result}");
                Thread.Sleep(500);
            }

            SetForegroundWindow(hWnd);
        }

        // Strategy 21: Move window to screen coordinates first
        public void RestoreStrategy_MoveToScreen(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Restore 21: Move to screen coordinates...");

            Rectangle bounds = GetWindowBounds(hWnd);
            Logger.Info($"Current bounds: {bounds}");

            // Get screen size
            System.Drawing.Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            Logger.Info($"Screen bounds: {screenBounds}");

            // Move window to visible coordinates (100, 100) with reasonable size
            int width = screenBounds.Width - 200;
            int height = screenBounds.Height - 200;
            
            Logger.Info($"Moving window to (100, 100, {width}, {height})...");
            bool result = MoveWindow(hWnd, 100, 100, width, height, true);
            Logger.Info($"MoveWindow result: {result}");
            
            Thread.Sleep(500);
            SetForegroundWindow(hWnd);
        }

        // Strategy 22: SetWindowPos to move to screen
        public void RestoreStrategy_SetWindowPosToScreen(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Restore 22: SetWindowPos to screen...");

            Rectangle bounds = GetWindowBounds(hWnd);
            Logger.Info($"Current bounds: {bounds}");

            System.Drawing.Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            int width = screenBounds.Width - 200;
            int height = screenBounds.Height - 200;

            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_SHOWWINDOW = 0x0040;

            Logger.Info($"SetWindowPos to (100, 100, {width}, {height})...");
            bool result = SetWindowPos(hWnd, IntPtr.Zero, 100, 100, width, height, SWP_NOZORDER | SWP_SHOWWINDOW);
            Logger.Info($"SetWindowPos result: {result}");
            
            Thread.Sleep(500);
            SetForegroundWindow(hWnd);
        }

        // Strategy 23: GetWindowPlacement + modify position
        public void RestoreStrategy_ModifyPlacement(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            Logger.Info("Restore 23: Modify WindowPlacement...");

            GetWindowPlacement(hWnd, out WINDOWPLACEMENT placement);
            Logger.Info($"Current showCmd: {placement.showCmd}");
            Logger.Info($"Current rcNormalPosition: {placement.rcNormalPosition}");

            System.Drawing.Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            
            // Set normal position to visible coordinates
            placement.rcNormalPosition = new Rectangle(100, 100, screenBounds.Width - 200, screenBounds.Height - 200);
            placement.showCmd = 1; // SW_SHOWNORMAL
            placement.length = Marshal.SizeOf(placement);

            Logger.Info($"Setting rcNormalPosition to: {placement.rcNormalPosition}");
            bool result = SetWindowPlacement(hWnd, ref placement);
            Logger.Info($"SetWindowPlacement result: {result}");
            
            Thread.Sleep(500);
            SetForegroundWindow(hWnd);
        }

        // Strategy 24: Enumerate ALL Kindle windows
        public void DiagnosticStrategy_EnumerateKindleWindows(IntPtr hWnd)
        {
            Logger.Info("Diagnostic 24: Enumerating ALL Kindle windows...");

            try
            {
                Process[] processes = Process.GetProcessesByName(Constants.KINDLE_PROCESS_NAME);
                if (processes.Length == 0)
                {
                    Logger.Warning("No Kindle process found");
                    return;
                }

                Process kindleProcess = processes[0];
                uint kindleProcessId = (uint)kindleProcess.Id;
                Logger.Info($"Kindle Process ID: {kindleProcessId}");
                Logger.Info($"MainWindowHandle: {kindleProcess.MainWindowHandle}");
                Logger.Info($"MainWindowTitle: {kindleProcess.MainWindowTitle}");

                List<IntPtr> kindleWindows = new List<IntPtr>();

                // Enumerate all top-level windows
                EnumWindows((hWndEnum, lParam) =>
                {
                    GetWindowThreadProcessId(hWndEnum, out uint processId);
                    if (processId == kindleProcessId)
                    {
                        kindleWindows.Add(hWndEnum);
                    }
                    return true;
                }, IntPtr.Zero);

                Logger.Info($"Found {kindleWindows.Count} Kindle windows");

                for (int i = 0; i < kindleWindows.Count; i++)
                {
                    IntPtr hwnd = kindleWindows[i];
                    Logger.Info($"\n--- Window {i + 1} ---");
                    Logger.Info($"Handle: {hwnd}");

                    // Get window title
                    System.Text.StringBuilder title = new System.Text.StringBuilder(256);
                    GetWindowText(hwnd, title, title.Capacity);
                    Logger.Info($"Title: {title}");

                    // Get class name
                    System.Text.StringBuilder className = new System.Text.StringBuilder(256);
                    GetClassName(hwnd, className, className.Capacity);
                    Logger.Info($"ClassName: {className}");

                    // Get window state
                    bool isVisible = IsWindowVisible(hwnd);
                    bool isIconic = IsIconic(hwnd);
                    Rectangle bounds = GetWindowBounds(hwnd);
                    GetWindowPlacement(hwnd, out WINDOWPLACEMENT placement);

                    Logger.Info($"IsVisible: {isVisible}");
                    Logger.Info($"IsIconic: {isIconic}");
                    Logger.Info($"Bounds: {bounds}");
                    Logger.Info($"ShowCmd: {placement.showCmd}");
                    Logger.Info($"rcNormalPosition: {placement.rcNormalPosition}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error enumerating windows: {ex.Message}", ex);
            }
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
                // SendKeys.SendWait("^g");
                Thread.Sleep(800);

                AutomationElement root = AutomationElement.RootElement;
                AutomationElement focused = AutomationElement.FocusedElement;
                if (focused == null)
                {
                    Logger.Warning("No focused element found when trying to get page count");
                    // SendKeys.SendWait("{ESC}");
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
                    // SendKeys.SendWait("{ESC}");
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
                                // SendKeys.SendWait("{ESC}");
                                Logger.Info($"Total page count detected: {total}");
                                return total;
                            }
                        }
                    }
                }
                
                // SendKeys.SendWait("{ESC}");
                Logger.Warning("Could not parse page count from dialog");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting total page count: {ex.Message}", ex);
                try { /* SendKeys.SendWait("{ESC}"); */ } catch { }
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
                    // SendKeys.SendWait("^g");
                    Thread.Sleep(Constants.DIALOG_WAIT_MS);
                    
                    // SendKeys.SendWait(totalPages.ToString());
                    // SendKeys.SendWait("{ENTER}");
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
