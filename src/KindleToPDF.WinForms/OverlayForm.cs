using System;
using System.Drawing;
using System.Windows.Forms;

namespace KindleToPDF
{
    public class OverlayForm : Form
    {
        public Rectangle CropRect { get; private set; }
        private Rectangle _screenBounds;
        private int _left, _top, _right, _bottom;
        private bool _draggingLeft, _draggingTop, _draggingRight, _draggingBottom;
        private const int HandleSize = Constants.HANDLE_SIZE;

        public OverlayForm(Rectangle initialCropRect = default)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.Black;
            this.Opacity = 0.3; // Semi-transparent background
            this.DoubleBuffered = true;
            this.TopMost = true;
            this.Cursor = Cursors.Cross;

            _screenBounds = Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty;
            
            // If initialCropRect is provided and valid, use it; otherwise use default 10% margin
            if (initialCropRect != Rectangle.Empty && initialCropRect.Width > 0 && initialCropRect.Height > 0)
            {
                _left = initialCropRect.Left;
                _top = initialCropRect.Top;
                _right = initialCropRect.Right;
                _bottom = initialCropRect.Bottom;
            }
            else
            {
                // Default crop: 10% margin
                _left = _screenBounds.Width / 10;
                _top = _screenBounds.Height / 10;
                _right = _screenBounds.Width - _left;
                _bottom = _screenBounds.Height - _top;
            }

            this.Paint += OverlayForm_Paint;
            this.MouseDown += OverlayForm_MouseDown;
            this.MouseMove += OverlayForm_MouseMove;
            this.MouseUp += OverlayForm_MouseUp;
            this.KeyDown += OverlayForm_KeyDown;
        }

        private void OverlayForm_Paint(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Pen pen = new Pen(Color.Red, 2);

            // Draw lines
            g.DrawLine(pen, _left, 0, _left, _screenBounds.Height); // Left
            g.DrawLine(pen, _right, 0, _right, _screenBounds.Height); // Right
            g.DrawLine(pen, 0, _top, _screenBounds.Width, _top); // Top
            g.DrawLine(pen, 0, _bottom, _screenBounds.Width, _bottom); // Bottom

            // Draw handles (optional, for visual cue)
            // Draw text
            string msg = "Drag red lines to set crop area. Press ENTER to Save, ESC to Cancel.";
            Font font = new Font("Arial", 16, FontStyle.Bold);
            g.DrawString(msg, font, Brushes.White, 50, 50);
        }

        private void OverlayForm_MouseDown(object? sender, MouseEventArgs e)
        {
            if (Math.Abs(e.X - _left) < HandleSize) _draggingLeft = true;
            else if (Math.Abs(e.X - _right) < HandleSize) _draggingRight = true;
            else if (Math.Abs(e.Y - _top) < HandleSize) _draggingTop = true;
            else if (Math.Abs(e.Y - _bottom) < HandleSize) _draggingBottom = true;
        }

        private void OverlayForm_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_draggingLeft) _left = e.X;
            if (_draggingRight) _right = e.X;
            if (_draggingTop) _top = e.Y;
            if (_draggingBottom) _bottom = e.Y;

            if (_draggingLeft || _draggingRight || _draggingTop || _draggingBottom)
            {
                this.Invalidate();
            }
            else
            {
                // Cursor update
                if (Math.Abs(e.X - _left) < HandleSize || Math.Abs(e.X - _right) < HandleSize) this.Cursor = Cursors.SizeWE;
                else if (Math.Abs(e.Y - _top) < HandleSize || Math.Abs(e.Y - _bottom) < HandleSize) this.Cursor = Cursors.SizeNS;
                else this.Cursor = Cursors.Default;
            }
        }

        private void OverlayForm_MouseUp(object? sender, MouseEventArgs e)
        {
            _draggingLeft = _draggingTop = _draggingRight = _draggingBottom = false;
        }

        private void OverlayForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // Normalize rect
                int x = Math.Min(_left, _right);
                int y = Math.Min(_top, _bottom);
                int w = Math.Abs(_right - _left);
                int h = Math.Abs(_bottom - _top);
                CropRect = new Rectangle(x, y, w, h);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }
    }
}
