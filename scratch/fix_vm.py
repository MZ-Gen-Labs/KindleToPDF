
import os

path = '/Users/miyazawahayato/git/KindleToPDF/src/KindleToPDF.Avalonia/ViewModels/MainWindowViewModel.cs'

with open(path, 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

# Find the start of AbortCapture
start_marker = 'private void AbortCapture()'
start_idx = content.find(start_marker)

# Find the end marker (StartCaptureAsync)
end_marker = 'private async Task StartCaptureAsync()'
end_idx = content.find(end_marker)

if start_idx != -1 and end_idx != -1:
    new_methods = """private void AbortCapture()
    {
        LogText += $"{DateTime.Now:HH:mm:ss} - 処理を中止 (Abort) し、キャプチャした画像を破棄します。\\n";
        
        _cts?.Cancel();
        
        foreach (var img in _capturedImages)
        {
            try { if (File.Exists(img)) File.Delete(img); } catch { }
        }
        _capturedImages.Clear();
    }

    private void ExecuteNavigation(Action<IntPtr> navigationAction, string actionName)
    {
        IntPtr hWnd = _automation.GetKindleWindow();
        if (hWnd != IntPtr.Zero)
        {
            _automation.BringWindowToFront(hWnd);
            navigationAction(hWnd);
            LogText += $"{DateTime.Now:HH:mm:ss} - Navigation: {actionName} コマンドを送信しました。\\n";
        }
        else
        {
            LogText += $"{DateTime.Now:HH:mm:ss} - エラー: Kindleウィンドウが見つかりません。\\n";
        }
    }

    private async Task CaptureManualPageAsync()
    {
        IntPtr hWnd = _automation.GetKindleWindow();
        if (hWnd == IntPtr.Zero) { LogText += "Kindleが見つかりません。\\n"; return; }

        // キャプチャに映らないよう、一瞬アプリを隠す
        var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (mainWindow != null) mainWindow.IsVisible = false;
        await Task.Delay(300);

        try
        {
            var bounds = _automation.GetWindowBounds(hWnd);
            using var rawImage = _automation.CaptureWindow(bounds);
            
            // --- ★修正: Tempフォルダではなく、CaptureServiceの正規パイプラインを通す ---
            var currentSettings = AppSettings.Load();
            var service = new CaptureService(_automation, currentSettings);
            
            // 画像が保存されたらリストに追加するイベントを登録
            service.OnPageCaptured += imgPath => _capturedImages.Add(imgPath);

            // クロップ、カラー変換、見開き分割、Outputフォルダへの保存をすべて実行
            await service.ProcessManualCaptureAsync(rawImage, OutputDirectory, _capturedImages.Count);

            UpdateManualCount();
            LogText += $"{DateTime.Now:HH:mm:ss} - 手動キャプチャ成功 (計 {_capturedImages.Count} 枚)\\n";
        }
        catch (Exception ex)
        {
            LogText += $"エラー: {ex.Message}\\n";
        }
        finally
        {
            if (mainWindow != null) mainWindow.IsVisible = true;
        }
    }

    private void RemoveLastCapture()
    {
        if (_capturedImages.Count == 0) return;
        string path = _capturedImages[^1];
        _capturedImages.RemoveAt(_capturedImages.Count - 1);
        try { if (File.Exists(path)) File.Delete(path); } catch { }
        UpdateManualCount();
        LogText += "最後のキャプチャを削除しました。\\n";
    }

    private void ClearAllCaptures()
    {
        foreach (var img in _capturedImages) try { File.Delete(img); } catch { }
        _capturedImages.Clear();
        UpdateManualCount();
        LogText += "すべてのキャプチャを破棄しました。\\n";
    }

    private async Task FinalizeManualPdfAsync()
    {
        if (_capturedImages.Count == 0) return;
        
        LogText += $"{DateTime.Now:HH:mm:ss} - PDFを生成中...\\n";
        
        await Task.Run(() => {
            var settings = AppSettings.Load();
            string pdfPath = Path.Combine(OutputDirectory, $"{BaseFileName}.pdf");
            new PdfGenerator().CreatePdf(_capturedImages, pdfPath, settings);
        });
        
        LogText += $"🎉 手動作成完了: {BaseFileName}.pdf\\n";
        _automation.BringSelfToFront();

        // ★追加: 自動モードと同様に、PDF化が終わったら元の画像を消してリセットする
        foreach (var img in _capturedImages) { try { File.Delete(img); } catch { } }
        _capturedImages.Clear();
        UpdateManualCount();
    }

    private void UpdateManualCount() => ManualCaptureCountText = $"Captured: {_capturedImages.Count} pages";


    """
    new_content = content[:start_idx] + new_methods + content[end_idx:]
    with open(path, 'w', encoding='utf-8') as f:
        f.write(new_content)
    print("Fixed.")
else:
    print(f"Markers not found: start={start_idx}, end={end_idx}")
