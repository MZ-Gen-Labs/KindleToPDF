using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

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
        private const int VK_RIGHT = 0x27;
        private const int VK_NEXT = 0x22; // PageDown

        public IntPtr GetKindleWindow()
        {
            // Kindle main window usually has title "Kindle" or class name "Qt5QWindowIcon" (varies by version)
            // Trying by window name first
            Process[] processes = Process.GetProcessesByName("Kindle");
            if (processes.Length > 0)
            {
                return processes[0].MainWindowHandle;
            }
            return IntPtr.Zero;
        }

        public Rectangle GetWindowBounds(IntPtr hWnd)
        {
            if (GetWindowRect(hWnd, out RECT rect))
            {
                return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            }
            return Rectangle.Empty;
        }

        public void SendPageTurn(IntPtr hWnd)
        {
            // Try sending Right Arrow key
            // Note: PostMessage might not work for all apps. 
            // If it fails, we might need SetForegroundWindow + SendKeys.SendWait
            
            // Method 1: PostMessage (Background friendly-ish)
            PostMessage(hWnd, WM_KEYDOWN, (IntPtr)VK_RIGHT, IntPtr.Zero);
            Thread.Sleep(50);
            PostMessage(hWnd, WM_KEYUP, (IntPtr)VK_RIGHT, IntPtr.Zero);

            // Fallback/Alternative: SetForeground and SendKeys (more reliable for Kindle)
            // SetForegroundWindow(hWnd);
            // Thread.Sleep(100);
            // SendKeys.SendWait("{RIGHT}");
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
            
            // Restore if minimized
            // ShowWindow(hWnd, SW_RESTORE); // Need PInvoke for ShowWindow if we want to handle minimized state properly
            
            SetForegroundWindow(hWnd);
        }

        public string GetBookTitleFromWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;

            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            int length = GetWindowText(hWnd, sb, sb.Capacity);
            
            if (length == 0) return null;

            string windowTitle = sb.ToString();
            
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
                
                return bookTitle;
            }

            return null;
        }
    }
}
