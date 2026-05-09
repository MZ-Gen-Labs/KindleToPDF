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
}