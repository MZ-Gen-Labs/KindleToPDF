using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using ReactiveUI;
using KindleToPDF.Core; 

namespace KindleToPDF.Avalonia.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly CaptureService _captureService;
    private readonly IAutomationLogic _automation;
    private readonly AppSettings _settings;
    private CancellationTokenSource? _cts;
    private readonly List<string> _capturedImages = new();

    // ==========================================
    // UIバインディング用プロパティ
    // ==========================================
    private string _outputDirectory = "";
    public string OutputDirectory { get => _outputDirectory; set { this.RaiseAndSetIfChanged(ref _outputDirectory, value); SaveCurrentSettings(); } }

    private string _baseFileName = "";
    public string BaseFileName { get => _baseFileName; set { this.RaiseAndSetIfChanged(ref _baseFileName, value); SaveCurrentSettings(); } }

    private decimal _interval;
    public decimal Interval { get => _interval; set { this.RaiseAndSetIfChanged(ref _interval, value); SaveCurrentSettings(); } }

    private bool _isRightToLeft;
    public bool IsRightToLeft { get => _isRightToLeft; set { this.RaiseAndSetIfChanged(ref _isRightToLeft, value); SaveCurrentSettings(); } }

    // --- 新規追加：詳細設定の復元 ---
    private int _pageCount;
    public int PageCount { get => _pageCount; set { this.RaiseAndSetIfChanged(ref _pageCount, value); SaveCurrentSettings(); } }

    private bool _autoDetect;
    public bool AutoDetect { get => _autoDetect; set { this.RaiseAndSetIfChanged(ref _autoDetect, value); SaveCurrentSettings(); } }

    private bool _stopAtLastPage;
    public bool StopAtLastPage { get => _stopAtLastPage; set { this.RaiseAndSetIfChanged(ref _stopAtLastPage, value); SaveCurrentSettings(); } }

    private bool _isSequential; // 上書きか連番か
    public bool IsSequential { get => _isSequential; set { this.RaiseAndSetIfChanged(ref _isSequential, value); SaveCurrentSettings(); } }

    private int _startNumber;
    public int StartNumber { get => _startNumber; set { this.RaiseAndSetIfChanged(ref _startNumber, value); SaveCurrentSettings(); } }

    private int _numberDigits;
    public int NumberDigits { get => _numberDigits; set { this.RaiseAndSetIfChanged(ref _numberDigits, value); SaveCurrentSettings(); } }

    private int _cropLeft;
    public int CropLeft { get => _cropLeft; set { this.RaiseAndSetIfChanged(ref _cropLeft, value); SaveCurrentSettings(); } }
    
    private int _cropTop;
    public int CropTop { get => _cropTop; set { this.RaiseAndSetIfChanged(ref _cropTop, value); SaveCurrentSettings(); } }

    private int _cropWidth;
    public int CropWidth { get => _cropWidth; set { this.RaiseAndSetIfChanged(ref _cropWidth, value); SaveCurrentSettings(); } }

    private int _cropHeight;
    public int CropHeight { get => _cropHeight; set { this.RaiseAndSetIfChanged(ref _cropHeight, value); SaveCurrentSettings(); } }

    private bool _splitDualPage;
    public bool SplitDualPage { get => _splitDualPage; set { this.RaiseAndSetIfChanged(ref _splitDualPage, value); SaveCurrentSettings(); } }

    private int _colorModeIndex;
    public int ColorModeIndex { get => _colorModeIndex; set { this.RaiseAndSetIfChanged(ref _colorModeIndex, value); SaveCurrentSettings(); } }

    private int _imageFormatIndex;
    public int ImageFormatIndex { get => _imageFormatIndex; set { this.RaiseAndSetIfChanged(ref _imageFormatIndex, value); SaveCurrentSettings(); } }

    private int _jpegQuality;
    public int JpegQuality { get => _jpegQuality; set { this.RaiseAndSetIfChanged(ref _jpegQuality, value); SaveCurrentSettings(); } }

    private int _monochromeThreshold;
    public int MonochromeThreshold { get => _monochromeThreshold; set { this.RaiseAndSetIfChanged(ref _monochromeThreshold, value); SaveCurrentSettings(); } }

    private string _logText = "";
    public string LogText { get => _logText; set => this.RaiseAndSetIfChanged(ref _logText, value); }

    // コマンド
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand AbortCommand { get; }
    public ICommand TopCommand { get; }
    public ICommand PrevCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand BottomCommand { get; }
    public ICommand FullScreenCommand { get; }
    public ICommand MaximizeCommand { get; }
    public ICommand MinimizeCommand { get; }
    public ICommand ResetCropCommand { get; }
    public ICommand FetchTitleCommand { get; }

    public MainWindowViewModel(CaptureService captureService, IAutomationLogic automation, AppSettings settings)
    {
        _captureService = captureService;
        _automation = automation;
        _settings = settings;

        // 起動時に保存された設定を読み込む
        _outputDirectory = _settings.OutputDirectory;
        _baseFileName = _settings.BaseFileName;
        _interval = _settings.Interval;
        _isRightToLeft = _settings.PageDirection == 0;
        
        // 新規追加分のロード
        _pageCount = _settings.PageCount;
        _autoDetect = _settings.AutoDetect;
        _stopAtLastPage = _settings.StopAtLastPage;
        _isSequential = _settings.Mode == FileNameMode.Sequential;
        _startNumber = _settings.StartNumber;
        _numberDigits = _settings.NumberDigits;
        _cropLeft = _settings.CropRect.X;
        _cropTop = _settings.CropRect.Y;
        _cropWidth = _settings.CropRect.Width;
        _cropHeight = _settings.CropRect.Height;
        _splitDualPage = _settings.SplitDualPage;
        _colorModeIndex = (int)_settings.ColorMode;
        _imageFormatIndex = (int)_settings.ImageFormat;
        _jpegQuality = _settings.JpegQuality;
        _monochromeThreshold = _settings.MonochromeThreshold;

        _captureService.OnLog += msg => 
        {
            Dispatcher.UIThread.Post(() => LogText += $"{DateTime.Now:HH:mm:ss} - {msg}\n");
        };
        _captureService.OnPageCaptured += imgPath => { _capturedImages.Add(imgPath); };

        StartCommand = ReactiveCommand.CreateFromTask(StartCaptureAsync);
        StopCommand = ReactiveCommand.Create(() => _cts?.Cancel());
        AbortCommand = ReactiveCommand.Create(AbortCapture);
        TopCommand = ReactiveCommand.Create(() => ExecuteNavigation(hWnd => _automation.SendHome(hWnd), "Top"));
        PrevCommand = ReactiveCommand.Create(() => ExecuteNavigation(hWnd => _automation.SendPrevPage(hWnd, IsRightToLeft), "Prev Page"));
        NextCommand = ReactiveCommand.Create(() => ExecuteNavigation(hWnd => _automation.SendNextPage(hWnd, IsRightToLeft), "Next Page"));
        BottomCommand = ReactiveCommand.Create(() => ExecuteNavigation(hWnd => _automation.GoToLastPage(hWnd), "Bottom"));
        FullScreenCommand = ReactiveCommand.Create(() => ExecuteNavigation(hWnd => _automation.ToggleFullScreen(hWnd), "Full Screen"));
        MaximizeCommand = ReactiveCommand.Create(() => ExecuteNavigation(hWnd => _automation.MaximizeKindleWindow(hWnd), "Maximize"));
        MinimizeCommand = ReactiveCommand.Create(() => ExecuteNavigation(hWnd => _automation.MinimizeKindleWindow(hWnd), "Minimize"));
        ResetCropCommand = ReactiveCommand.Create(ResetCrop);
        FetchTitleCommand = ReactiveCommand.CreateFromTask(FetchBookTitleAsync);
    }

    private void ResetCrop()
    {
        CropLeft = 0;
        CropTop = 0;
        CropWidth = 0;
        CropHeight = 0;
    }

    private async Task FetchBookTitleAsync()
    {
        try
        {
            // Avaloniaの機能を使ってOSのクリップボードを取得
            var clipboard = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Clipboard;
            if (clipboard == null)
            {
                LogText += $"{DateTime.Now:HH:mm:ss} - クリップボードにアクセスできません。\n";
                return;
            }

            // クリップボードのテキストを取得
            string? text = await clipboard.GetTextAsync();
            
            if (string.IsNullOrWhiteSpace(text))
            {
                LogText += $"{DateTime.Now:HH:mm:ss} - クリップボードが空です。Kindleで本文を少しコピーしてから実行してください。\n";
                return;
            }

            // 抽出ロジックの呼び出し
            string? title = ExtractTitleFromClipboardText(text);

            if (!string.IsNullOrEmpty(title))
            {
                BaseFileName = title; // UIのテキストボックスを更新
                SaveCurrentSettings(); // 設定ファイルにも保存
                LogText += $"{DateTime.Now:HH:mm:ss} - クリップボードからタイトルを抽出しました: {title}\n";
            }
            else
            {
                LogText += $"{DateTime.Now:HH:mm:ss} - タイトルを抽出できませんでした。Kindleで本文を少しコピーしてから再度お試しください。\n";
            }
        }
        catch (Exception ex)
        {
            LogText += $"{DateTime.Now:HH:mm:ss} - タイトル取得中にエラーが発生しました: {ex.Message}\n";
        }
    }

    private string? ExtractTitleFromClipboardText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 複数行コピーされた場合、Kindleは「最後の行」に引用情報を付与する
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var lastLine = lines.LastOrDefault();

        if (lastLine == null || !lastLine.Contains("Kindle")) return null;

        // 例: "夏目漱石. 吾輩は猫である (Kindle の位置No.12-13). Publisher. Kindle 版."
        // 最初の「. 」と「 (Kindle」の間にあるのが書籍名
        int firstDotIndex = lastLine.IndexOf(". ");
        if (firstDotIndex == -1) return null;

        int startIndex = firstDotIndex + 2;
        int endIndex = lastLine.IndexOf(" (Kindle", startIndex);
        
        if (endIndex == -1)
        {
            // "(Kindle の位置..." がない場合は次のピリオドまでとする
            endIndex = lastLine.IndexOf(". ", startIndex);
        }

        if (endIndex != -1 && endIndex > startIndex)
        {
            string title = lastLine.Substring(startIndex, endIndex - startIndex).Trim();
            
            // ファイル名に使用できない禁止文字（\ / : * ? " < > | など）をアンダースコアに置換
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                title = title.Replace(c, '_');
            }
            return title;
        }

        return null;
    }

    private void SaveCurrentSettings()
    {
        _settings.OutputDirectory = this.OutputDirectory;
        _settings.Interval = (int)this.Interval;
        _settings.PageCount = (int)this.PageCount;
        _settings.PageDirection = this.IsRightToLeft ? 0 : 1;
        _settings.StopAtLastPage = this.StopAtLastPage;
        _settings.BaseFileName = this.BaseFileName;
        _settings.NumberDigits = (int)this.NumberDigits;
        _settings.StartNumber = (int)this.StartNumber;
        _settings.SplitDualPage = this.SplitDualPage;
        _settings.ColorMode = (ImageColorMode)this.ColorModeIndex;
        _settings.ImageFormat = (PdfImageFormat)this.ImageFormatIndex;
        _settings.JpegQuality = (int)this.JpegQuality;
        _settings.MonochromeThreshold = (int)this.MonochromeThreshold;

        _settings.Save();
    }

    private void AbortCapture()
    {
        LogText += $"{DateTime.Now:HH:mm:ss} - 処理を中止 (Abort) し、キャプチャした画像を破棄します。\n";
        
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
            LogText += $"{DateTime.Now:HH:mm:ss} - Navigation: {actionName} コマンドを送信しました。\n";
        }
        else
        {
            LogText += $"{DateTime.Now:HH:mm:ss} - エラー: Kindleウィンドウが見つかりません。\n";
        }
    }


    private async Task StartCaptureAsync()
    {
        _cts = new CancellationTokenSource();
        IntPtr hWnd = _automation.GetKindleWindow();
        
        if (hWnd == IntPtr.Zero)
        {
            LogText += "Kindleウィンドウが見つかりません。\n";
            return;
        }

        // 保存直前の最新設定をロード（UIからの変更を確定させる）
        var currentSettings = AppSettings.Load();
        
        // リストをリセット
        _capturedImages.Clear(); 

        try
        {
            // CaptureServiceの実行。現在のインスタンスの _settings が更新されていることを確認
            await _captureService.RunCaptureAsync(hWnd, OutputDirectory, 0, _cts.Token);

            if (_capturedImages.Count > 0)
            {
                LogText += "PDF生成中...\n";
                string finalPdfPath = Path.Combine(OutputDirectory, $"{BaseFileName}.pdf");
                
                await Task.Run(() => 
                {
                    var pdfGen = new PdfGenerator();
                    pdfGen.CreatePdf(_capturedImages, finalPdfPath, currentSettings); 
                });

                LogText += $"🎉 完了: {finalPdfPath}\n";
                _automation.BringSelfToFront();

                // 一時ファイルの削除
                foreach (var img in _capturedImages) File.Delete(img);
            }
        }
        catch (OperationCanceledException)
        {
            LogText += "停止しました。\n";
        }
        catch (Exception ex)
        {
            LogText += $"エラー: {ex.Message}\n";
        }
    }
}

// CommunityToolkit.Mvvm を使う場合の軽量 RelayCommand
internal class RelayCommand : ICommand
{
    private readonly Func<Task>? _asyncExecute;
    private readonly Action? _execute;

    public RelayCommand(Func<Task> execute) => _asyncExecute = execute;
    public RelayCommand(Action execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public async void Execute(object? parameter)
    {
        if (_asyncExecute != null) await _asyncExecute();
        else _execute?.Invoke();
    }
}
