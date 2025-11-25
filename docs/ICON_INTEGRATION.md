# Application Icon Integration - Technical Guide

## 概要

Windowsアプリケーションにアイコンを統合する際の技術的な詳細とベストプラクティス。EXEファイルのアイコンとウィンドウのアイコンの両方を設定する方法を説明する。

## 背景

Windowsアプリケーションには2種類のアイコンが必要：

1. **EXEファイルのアイコン**: エクスプローラーで表示されるアイコン
2. **ウィンドウのアイコン**: タイトルバーとタスクバーに表示されるアイコン

これらは異なる方法で設定する必要がある。

## 1. アイコン画像の作成

### 推奨仕様

- **形式**: PNG（元画像）、ICO（Windows用）
- **サイズ**: 256x256ピクセル以上（高解像度ディスプレイ対応）
- **デザイン**: シンプルで視認性の高いデザイン
- **背景**: 透過背景（RGBA）

### 生成方法

AI画像生成ツールを使用する場合：

```
プロンプト例:
"A modern, sleek application icon for [アプリ名]. 
The design should feature [主要な要素]. 
Use a color palette of [色1], [色2], and [色3]. 
Minimalist, flat design with subtle shadows, 
suitable for a Windows application icon. 
White background. Square aspect ratio."
```

## 2. ICOファイルの作成

### 問題: PowerShellでの変換は不完全

以下のPowerShellコマンドは**動作しない**：

```powershell
# ❌ これは正しいICOファイルを生成しない
$img = [System.Drawing.Bitmap]::FromFile('AppIcon.png')
$icon = [System.Drawing.Icon]::FromHandle($img.GetHicon())
$stream = [System.IO.File]::OpenWrite('AppIcon.ico')
$icon.Save($stream)
```

**エラー**: "can not be a picture that can be used as a Icon"

### 解決策: ImageSharpを使用したC#プログラム

正しいICOファイルを作成するには、複数のサイズを含むICOファイルを生成する必要がある。

#### ステップ1: 変換プログラムの作成

**IcoConverter.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="SixLabors.ImageSharp" Version="3.1.5" />
  </ItemGroup>
</Project>
```

**Program.cs**:
```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

class Program
{
    static void Main(string[] args)
    {
        string pngPath = @"c:\Path\To\AppIcon.png";
        string icoPath = @"c:\Path\To\AppIcon.ico";

        try
        {
            using var image = Image.Load(pngPath);
            using var icoStream = new FileStream(icoPath, FileMode.Create);
            using var writer = new BinaryWriter(icoStream);

            // ICO header
            writer.Write((short)0); // Reserved
            writer.Write((short)1); // Type (1 = ICO)
            
            // Windows標準サイズ
            int[] sizes = { 16, 32, 48, 64, 128, 256 };
            writer.Write((short)sizes.Length);

            int offset = 6 + (16 * sizes.Length);
            var imageDataList = new List<byte[]>();

            // ディレクトリエントリを書き込み
            foreach (int size in sizes)
            {
                using var resized = image.Clone(ctx => ctx.Resize(size, size));
                using var ms = new MemoryStream();
                resized.Save(ms, new PngEncoder());
                byte[] imageData = ms.ToArray();
                imageDataList.Add(imageData);

                writer.Write((byte)size);      // Width
                writer.Write((byte)size);      // Height
                writer.Write((byte)0);         // Color palette
                writer.Write((byte)0);         // Reserved
                writer.Write((short)1);        // Color planes
                writer.Write((short)32);       // Bits per pixel
                writer.Write(imageData.Length); // Size
                writer.Write(offset);          // Offset
                
                offset += imageData.Length;
            }

            // 画像データを書き込み
            foreach (var imageData in imageDataList)
            {
                writer.Write(imageData);
            }

            Console.WriteLine($"Successfully created ICO file: {icoPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
```

#### ステップ2: 実行

```bash
dotnet run --project IcoConverter
```

### 代替方法: オンラインツール

プログラミング不要の方法：

- https://convertio.co/ja/png-ico/
- https://www.icoconverter.com/

**推奨設定**:
- 複数サイズを含める: 16, 32, 48, 64, 128, 256
- 32ビットカラー（透過対応）

## 3. プロジェクトへの統合

### 3.1 EXEファイルのアイコン設定

**KindleToPDF.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <!-- EXEファイルのアイコンを設定 -->
    <ApplicationIcon>Resources\AppIcon.ico</ApplicationIcon>
  </PropertyGroup>

  <ItemGroup>
    <!-- ウィンドウアイコン用にPNGを埋め込みリソースとして追加 -->
    <EmbeddedResource Include="Resources\AppIcon.png" />
  </ItemGroup>
</Project>
```

**重要**: `ApplicationIcon` プロパティは**正しい形式のICOファイル**が必要。PowerShellで生成したICOファイルは使用できない。

### 3.2 ウィンドウアイコンの設定

**Form1.cs**:
```csharp
public Form1()
{
    InitializeComponent();
    
    // ウィンドウアイコンを埋め込みリソースから読み込み
    try
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        string resourceName = "KindleToPDF.Resources.AppIcon.png";
        
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream != null)
            {
                using (var bitmap = new Bitmap(stream))
                {
                    IntPtr hIcon = bitmap.GetHicon();
                    this.Icon = Icon.FromHandle(hIcon);
                    Logger.Info("App icon loaded successfully");
                }
            }
            else
            {
                Logger.Warning($"Embedded resource not found: {resourceName}");
            }
        }
    }
    catch (Exception ex)
    {
        Logger.Warning($"Failed to load app icon: {ex.Message}");
    }
}
```

**なぜPNGを使用するか**:
- ICOファイルは `Icon.FromHandle()` で読み込むと「正しいアイコンではない」エラーが発生する場合がある
- PNGから動的に変換する方が確実
- 埋め込みリソースとして配布が簡単

## 4. リソース名の確認方法

埋め込みリソースの名前は `<プロジェクト名>.<フォルダパス>.<ファイル名>` の形式になる。

確認方法：

```csharp
var assembly = System.Reflection.Assembly.GetExecutingAssembly();
string[] resourceNames = assembly.GetManifestResourceNames();
foreach (string name in resourceNames)
{
    Console.WriteLine(name);
}
```

## 5. トラブルシューティング

### 問題1: EXEファイルにアイコンが表示されない

**原因**: ICOファイルが正しい形式ではない

**解決策**:
1. ImageSharpを使用したC#プログラムでICOファイルを再生成
2. または、オンラインツールを使用
3. `dotnet clean` → `dotnet build` で再ビルド

### 問題2: ウィンドウにアイコンが表示されない

**原因**: 埋め込みリソース名が間違っている、またはリソースが埋め込まれていない

**解決策**:
1. csprojで `<EmbeddedResource Include="Resources\AppIcon.png" />` が設定されているか確認
2. リソース名を確認（上記の確認方法を使用）
3. ログでエラーメッセージを確認

### 問題3: "can not be a picture that can be used as a Icon" エラー

**原因**: PowerShellで生成したICOファイルを使用している

**解決策**: ImageSharpまたはオンラインツールで正しいICOファイルを生成

## 6. ベストプラクティス

### ファイル配置

```
src/
├── Resources/
│   ├── AppIcon.png    # 元画像（256x256以上）
│   └── AppIcon.ico    # 変換後（複数サイズ含む）
├── KindleToPDF.csproj
└── Form1.cs
```

### ビルド後の確認

1. **EXEアイコン**: エクスプローラーで `bin\Debug\net10.0-windows\KindleToPDF.exe` を確認
2. **ウィンドウアイコン**: アプリを起動してタイトルバーとタスクバーを確認
3. **ログ**: `app_log.txt` でアイコン読み込みのログを確認

### 推奨ワークフロー

1. AI画像生成ツールでPNG画像を作成（256x256以上）
2. ImageSharpプログラムまたはオンラインツールでICOに変換
3. PNGとICOの両方を `Resources/` フォルダに配置
4. csprojを更新（ApplicationIcon + EmbeddedResource）
5. Form1.csでウィンドウアイコンを設定
6. ビルドして確認

## 7. 参考資料

- [Icon File Format Specification](https://en.wikipedia.org/wiki/ICO_(file_format))
- [SixLabors.ImageSharp Documentation](https://docs.sixlabors.com/articles/imagesharp/index.html)
- [Windows Application Icon Guidelines](https://learn.microsoft.com/en-us/windows/apps/design/style/iconography/app-icon-design)

## 変更履歴

- 2025-11-25: 初版作成 - アプリケーションアイコン統合ガイド
