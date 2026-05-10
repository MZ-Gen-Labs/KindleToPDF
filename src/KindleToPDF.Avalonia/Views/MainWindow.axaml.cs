using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using KindleToPDF.Avalonia.ViewModels;
using System.Threading.Tasks;

namespace KindleToPDF.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // 「参照...」ボタンが押されたときの処理
    private async void OnBrowseFolderClick(object sender, RoutedEventArgs e)
    {
        // Mac/Windows ネイティブのフォルダ選択ダイアログを表示
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "保存先フォルダを選択してください",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            // 選択されたパスを取得し、ViewModelのプロパティにセットする
            var folderPath = folders[0].Path.LocalPath;
            if (DataContext is MainWindowViewModel vm)
            {
                vm.OutputDirectory = folderPath;
            }
        }
    }

    private async void OnSetCropAreaClick(object sender, RoutedEventArgs e)
    {
        // 1. 設定画面が邪魔にならないよう、一時的に最小化する
        this.WindowState = WindowState.Minimized;
        await Task.Delay(300); // 最小化アニメーションを待つ

        // 2. 透過ウィンドウを開く
        var overlay = new CropOverlayWindow();
        await overlay.ShowDialog(this);

        // 3. 終わったら設定画面を元に戻す
        this.WindowState = WindowState.Normal;

        // 4. 取得した座標を ViewModel に渡す
        if (overlay.ResultRect.Width > 0 && DataContext is MainWindowViewModel vm)
        {
            vm.CropLeft = overlay.ResultRect.X;
            vm.CropTop = overlay.ResultRect.Y;
            vm.CropWidth = overlay.ResultRect.Width;
            vm.CropHeight = overlay.ResultRect.Height;
        }
    }
}