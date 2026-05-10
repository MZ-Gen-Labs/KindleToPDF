using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using KindleToPDF.Avalonia.ViewModels;
using KindleToPDF.Avalonia.Views;
using KindleToPDF.Core;

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
            var settings = AppSettings.Load();
            var captureService = new CaptureService(automation, settings);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(captureService, automation, settings),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}