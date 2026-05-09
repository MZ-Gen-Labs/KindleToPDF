using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using KindleToPDF;

namespace KindleToPDF.Avalonia.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly CaptureService _captureService;
    private readonly IAutomationLogic _automation;
    private readonly AppSettings _settings;
    private CancellationTokenSource? _cts;

    // === UIにバインドする設定プロパティ ===
    public string OutputDirectory
    {
        get => string.IsNullOrEmpty(_settings.OutputDirectory) ? "/tmp" : _settings.OutputDirectory;
        set
        {
            _settings.OutputDirectory = value;
            OnPropertyChanged();
        }
    }

    private string _baseFileName = "KindleBook";
    public string BaseFileName
    {
        get => _baseFileName;
        set => SetProperty(ref _baseFileName, value);
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

        try
        {
            // 設定を保存
            _settings.Save();
            
            // Core側のロジックを呼び出す
            await _captureService.RunCaptureAsync(hWnd, OutputDirectory, 0, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            LogText += "キャプチャを停止しました。\n";
        }
        catch (Exception ex)
        {
            LogText += $"エラー発生: {ex.Message}\n";
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
