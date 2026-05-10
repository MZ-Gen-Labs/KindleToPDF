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

    private string _logText = "";
    public string LogText { get => _logText; set => this.RaiseAndSetIfChanged(ref _logText, value); }

    // コマンド
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

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

        _captureService.OnLog += msg => 
        {
            Dispatcher.UIThread.Post(() => LogText += $"{DateTime.Now:HH:mm:ss} - {msg}\n");
        };
        _captureService.OnPageCaptured += imgPath => { _capturedImages.Add(imgPath); };

        StartCommand = ReactiveCommand.CreateFromTask(StartCaptureAsync);
        StopCommand = ReactiveCommand.Create(() => _cts?.Cancel());
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

        LogText += $"設定を確認: 保存先={OutputDirectory}, 間隔={_settings.Interval}ms, 方向={(IsRightToLeft ? "右開き" : "左開き")}\n";
        
        _capturedImages.Clear();

        try
        {
            // UI上の設定をAppSettingsに確実に反映させる
            _settings.Interval = (int)this.Interval;
            _settings.PageDirection = IsRightToLeft ? 0 : 1;
            // 必要に応じて他の設定（StartNumber等）もここでセット可能
            
            // 設定を保存
            _settings.Save();
            
            // 1. キャプチャ実行
            await _captureService.RunCaptureAsync(hWnd, OutputDirectory, 0, _cts.Token);

            // 2. キャプチャ完了後のPDF生成処理
            if (_capturedImages.Count > 0)
            {
                LogText += "\nキャプチャ完了。PDFの生成を開始します...\n";

                string rawPdfPath = Path.Combine(OutputDirectory, $"{BaseFileName}.pdf");
                string finalPdfPath = FileNameGenerator.GetOutputFilePath(rawPdfPath, _settings);

                await Task.Run(() => 
                {
                    var pdfGen = new PdfGenerator();
                    pdfGen.CreatePdf(_capturedImages, finalPdfPath, _settings); 
                });

                LogText += $"🎉 PDFの生成が完了しました！\n保存先: {finalPdfPath}\n";

                // 3. 一時画像ファイルの自動削除
                LogText += "一時画像ファイルをクリーンアップしています...\n";
                foreach (var imgPath in _capturedImages)
                {
                    if (File.Exists(imgPath))
                    {
                        try { File.Delete(imgPath); } catch { }
                    }
                }
                LogText += "完了しました！\n";
                
                // 自分自身（Avaloniaアプリ）を前面に呼び出す
                _automation.BringSelfToFront();
            }
            else
            {
                LogText += "キャプチャされた画像がありませんでした。\n";
            }
        }
        catch (OperationCanceledException)
        {
            LogText += "キャプチャを停止しました。\n";
            // 停止された場合でも、それまでに撮った画像があればPDF化するかどうかは悩みどころですが、
            // 今回はユーザーの要求通りに実装します（キャプチャ完了後のみ実行）。
        }
        catch (Exception ex)
        {
            LogText += $"エラー発生: {ex.Message}\n";
            Logger.Error("Capture/PDF failed", ex);
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
