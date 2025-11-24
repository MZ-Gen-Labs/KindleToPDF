using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KindleToPDF
{
    public class GuidelineOverlay : Form
    {
        private Rectangle _cropRect = Rectangle.Empty;

        // Win32 API for click-through
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = Constants.GWL_EXSTYLE;
        private const int WS_EX_LAYERED = Constants.WS_EX_LAYERED;
        private const int WS_EX_TRANSPARENT = Constants.WS_EX_TRANSPARENT;

        public GuidelineOverlay()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;

            this.Load += GuidelineOverlay_Load;
            this.Paint += GuidelineOverlay_Paint;
        }

        private void GuidelineOverlay_Load(object? sender, EventArgs e)
        {
            // Make the form click-through
            int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        }

        public void UpdateCropRect(Rectangle cropRect)
        {
            _cropRect = cropRect;
            this.Invalidate();
        }

        private void GuidelineOverlay_Paint(object? sender, PaintEventArgs e)
        {
            if (_cropRect == Rectangle.Empty) return;

            Graphics g = e.Graphics;
            using (Pen pen = new Pen(Color.Red, 2))
            {
                Rectangle screenBounds = Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;
                
                // Draw lines
                g.DrawLine(pen, _cropRect.Left, 0, _cropRect.Left, screenBounds.Height); // Left
                g.DrawLine(pen, _cropRect.Right, 0, _cropRect.Right, screenBounds.Height); // Right
                g.DrawLine(pen, 0, _cropRect.Top, screenBounds.Width, _cropRect.Top); // Top
                g.DrawLine(pen, 0, _cropRect.Bottom, screenBounds.Width, _cropRect.Bottom); // Bottom
            }
        }
    }
}
