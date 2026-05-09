using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using KindleToPDF.Avalonia.ViewModels;
using KindleToPDF.Avalonia.Views;

namespace KindleToPDF.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            IAutomationLogic automation = new MacAutomationLogic();
            var settings = new AppSettings();
            var captureService = new CaptureService(automation, settings);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(captureService, automation),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}