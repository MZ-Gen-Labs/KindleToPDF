using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
// Coreプロジェクトの名前空間を追加
using KindleToPDF;

namespace KindleToPDF.Avalonia.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly CaptureService _captureService;
    private readonly IAutomationLogic _automation;
    private CancellationTokenSource? _cts;

    // 画面にバインド（表示）するプロパティ
    private string _logText = "";
    public string LogText
    {
        get => _logText;
        set => SetProperty(ref _logText, value);
    }

    // コマンド（ボタンが押された時の処理）
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    public MainWindowViewModel(CaptureService captureService, IAutomationLogic automation)
    {
        _captureService = captureService;
        _automation = automation;

        // イベントを購読して、ログが来たらUIを更新する
        _captureService.OnLog += msg =>
        {
            // UIスレッドで更新するための安全な呼び出し
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

        try
        {
            // Core側のロジックを呼び出す
            await _captureService.RunCaptureAsync(hWnd, "/tmp", 0, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            LogText += "キャプチャを停止しました。\n";
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
