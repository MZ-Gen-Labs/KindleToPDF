using System;
using System.Drawing;

namespace KindleToPDF
{
    /// <summary>
    /// OS固有のウィンドウ操作・画像キャプチャ処理を抽象化するインターフェース
    /// </summary>
    public interface IAutomationLogic
    {
        // ウィンドウ関連
        IntPtr GetKindleWindow();
        Rectangle GetWindowBounds(IntPtr hWnd);
        void BringWindowToFront(IntPtr hWnd);
        void MaximizeKindleWindow(IntPtr hWnd);
        void MinimizeKindleWindow(IntPtr hWnd);
        void ToggleFullScreen(IntPtr hWnd);
        string? GetBookTitleFromWindow(IntPtr hWnd);

        // Kindle操作関連
        void SendHome(IntPtr hWnd);
        void GoToLastPage(IntPtr hWnd);
        void SendPageTurn(IntPtr hWnd, bool isRightToLeft);
        void SendPrevPage(IntPtr hWnd, bool isRightToLeft);
        void SendNextPage(IntPtr hWnd, bool isRightToLeft);

        // 画像・状態取得関連
        Bitmap CaptureWindow(Rectangle bounds);
        Bitmap CropBitmap(Bitmap src, Rectangle cropRect);
        bool AreImagesSame(Bitmap img1, Bitmap img2);
        bool IsKeyDown(int vKey);
    }
}
