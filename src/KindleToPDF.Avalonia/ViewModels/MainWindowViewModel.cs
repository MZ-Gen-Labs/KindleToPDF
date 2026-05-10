using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using KindleToPDF;
using KindleToPDF.Core;

namespace KindleToPDF.Avalonia.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly CaptureService _captureService;
    private readonly IAutomationLogic _automation;
    private readonly AppSettings _settings;
    private CancellationTokenSource? _cts;

    // --- キャプチャした画像パスを保持するリスト ---
    private readonly List<string> _capturedImages = new();

    // === UIにバインドする設定プロパティ ===
    public string OutputDirectory
    {
        get => _settings.OutputDirectory;
        set
        {
            _settings.OutputDirectory = value;
            OnPropertyChanged();
            _settings.Save();
        }
    }

    public string BaseFileName
    {
        get => _settings.BaseFileName;
        set
        {
            _settings.BaseFileName = value;
            OnPropertyChanged();
            _settings.Save();
        }
    }

    public decimal? IntervalDecimal
    {
        get => (decimal)_settings.Interval;
        set
        {
            if (value.HasValue)
            {
                _settings.Interval = (int)value.Value;
                OnPropertyChanged();
                _settings.Save();
            }
        }
    }

    public bool IsRightToLeft
    {
        get => _settings.PageDirection == 0;
        set
        {
            if (value)
            {
                _settings.PageDirection = 0;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLeftToRight));
                _settings.Save();
            }
        }
    }

    public bool IsLeftToRight
    {
        get => _settings.PageDirection == 1;
        set
        {
            if (value)
            {
                _settings.PageDirection = 1;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRightToLeft));
                _settings.Save();
            }
        }
    }

    private string _logText = "";
    public string LogText
    {
        get => _logText;
        set => SetProperty(ref _logText, value);
    }

    // ===================================

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    public MainWindowViewModel(CaptureService captureService, IAutomationLogic automation, AppSettings settings)
    {
        _captureService = captureService;
        _automation = automation;
        _settings = settings;

        _captureService.OnLog += msg => 
        {
            Dispatcher.UIThread.Post(() => LogText += $"{DateTime.Now:HH:mm:ss} - {msg}\n");
        };

        // 画像がキャプチャされるたびにリストにパスを追加
        _captureService.OnPageCaptured += imgPath =>
        {
            _capturedImages.Add(imgPath);
        };

        StartCommand = new RelayCommand(async () => await StartCaptureAsync());
        StopCommand = new RelayCommand(() => _cts?.Cancel());
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
            _settings.Interval = (int)(IntervalDecimal ?? 1000);
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
