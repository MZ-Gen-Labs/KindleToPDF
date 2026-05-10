using Avalonia.Controls;
using Avalonia.Input;
using System;
using SixLabors.ImageSharp;
using Point = Avalonia.Point;

namespace KindleToPDF.Avalonia.Views;

public partial class CropOverlayWindow : Window
{
    private Point _startPoint;
    private bool _isDragging = false;
    
    // 選択された座標をここに保存する
    public Rectangle ResultRect { get; private set; } = Rectangle.Empty;

    public CropOverlayWindow()
    {
        InitializeComponent();
        
        // ESCキーが押されたらキャンセルして閉じる
        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var canvas = sender as Canvas;
        if (canvas == null) return;

        _startPoint = e.GetPosition(canvas);
        _isDragging = true;
        
        SelectionBorder.IsVisible = true;
        Canvas.SetLeft(SelectionBorder, _startPoint.X);
        Canvas.SetTop(SelectionBorder, _startPoint.Y);
        SelectionBorder.Width = 0;
        SelectionBorder.Height = 0;
        
        HintText.IsVisible = false; // ヒントを隠す
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;

        var canvas = sender as Canvas;
        if (canvas == null) return;

        var currentPoint = e.GetPosition(canvas);
        
        var x = Math.Min(_startPoint.X, currentPoint.X);
        var y = Math.Min(_startPoint.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - _startPoint.X);
        var height = Math.Abs(currentPoint.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionBorder, x);
        Canvas.SetTop(SelectionBorder, y);
        SelectionBorder.Width = width;
        SelectionBorder.Height = height;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;

        var canvas = sender as Canvas;
        if (canvas == null) return;

        var currentPoint = e.GetPosition(canvas);
        
        int x = (int)Math.Min(_startPoint.X, currentPoint.X);
        int y = (int)Math.Min(_startPoint.Y, currentPoint.Y);
        int width = (int)Math.Abs(currentPoint.X - _startPoint.X);
        int height = (int)Math.Abs(currentPoint.Y - _startPoint.Y);

        // ある程度の大きさがあれば確定として保存
        if (width > 10 && height > 10)
        {
            ResultRect = new Rectangle(x, y, width, height);
        }

        Close(); // 選択が終わったら自動で閉じる
    }
}
