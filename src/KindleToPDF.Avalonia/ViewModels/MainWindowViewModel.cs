using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
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
    public ICommand ResetCropCommand { get; }

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
        ResetCropCommand = ReactiveCommand.Create(ResetCrop);
    }

    private void ResetCrop()
    {
        CropLeft = 0;
        CropTop = 0;
        CropWidth = 0;
        CropHeight = 0;
    }

    private void SaveCurrentSettings()
    {
        _settings.OutputDirectory = this.OutputDirectory;
        _settings.BaseFileName = this.BaseFileName;
        _settings.Interval = (int)this.Interval;
        _settings.PageDirection = this.IsRightToLeft ? 0 : 1;
        _settings.PageCount = this.PageCount;
        _settings.AutoDetect = this.AutoDetect;
        _settings.StopAtLastPage = this.StopAtLastPage;
        _settings.Mode = this.IsSequential ? FileNameMode.Sequential : FileNameMode.Overwrite;
        _settings.StartNumber = this.StartNumber;
        _settings.NumberDigits = this.NumberDigits;
        _settings.CropRect = new SixLabors.ImageSharp.Rectangle(this.CropLeft, this.CropTop, this.CropWidth, this.CropHeight);
        _settings.SplitDualPage = this.SplitDualPage;
        _settings.ColorMode = (ImageColorMode)this.ColorModeIndex;
        _settings.ImageFormat = (PdfImageFormat)this.ImageFormatIndex;
        _settings.JpegQuality = this.JpegQuality;
        _settings.MonochromeThreshold = this.MonochromeThreshold;
        _settings.Save();
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
