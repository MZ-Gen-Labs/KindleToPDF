# Kindle for PC Automation - Best Practices and Known Issues

## 概要

Kindle for PCの自動化において、実装中に発見した重要なノウハウ、ベストプラクティス、既知の問題と解決策をまとめた技術仕様書。

## 1. 書籍タイトルの取得

### ウィンドウタイトルの形式

Kindle for PCのウィンドウタイトルは以下の形式：

```
Kindle for PC [デバイス名] - [書籍名]
```

**例:**
- `Kindle for PC msi - ハリー・ポッターと賢者の石`
- `Kindle for PC [PC-NAME] - The Great Gatsby`

### 抽出ロジック

最初の `" - "` (スペース-ハイフン-スペース) 以降を書籍名として抽出：

```csharp
string windowTitle = "Kindle for PC msi - ハリー・ポッターと賢者の石";
int separatorIndex = windowTitle.IndexOf(" - ");
if (separatorIndex > 0 && separatorIndex < windowTitle.Length - 3)
{
    string bookTitle = windowTitle.Substring(separatorIndex + 3).Trim();
    // bookTitle = "ハリー・ポッターと賢者の石"
}
```

### ファイル名の無効文字処理

Windowsのファイル名に使用できない文字をアンダースコアに置換：

```csharp
char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
foreach (char c in invalidChars)
{
    bookTitle = bookTitle.Replace(c, '_');
}
```

**無効文字:** `\ / : * ? " < > |`

## 2. ページナビゲーション

### Ctrl+Gダイアログによるページ移動

Kindle for PCでは、`Ctrl+G` でページ移動ダイアログが開く。

#### 最初のページへ移動

```csharp
SendKeys.SendWait("^g");           // Ctrl+G
Thread.Sleep(500);                 // ダイアログ表示待機
SendKeys.SendWait("1");            // ページ番号入力
SendKeys.SendWait("{ENTER}");      // 確定
```

**注意:** `VK_HOME` キーは Kindle for PC では動作しない。

#### 最終ページへ移動

UI Automationを使用してダイアログから総ページ数を取得：

```csharp
public int GetTotalPageCount(IntPtr kindleWnd)
{
    BringWindowToFront(kindleWnd);
    SendKeys.SendWait("^g");
    Thread.Sleep(800);

    AutomationElement focused = AutomationElement.FocusedElement;
    if (focused == null) return -1;

    // ダイアログウィンドウを取得
    AutomationElement dialog = focused;
    while (dialog != null && dialog.Current.ControlType != ControlType.Window)
    {
        dialog = TreeWalker.ControlViewWalker.GetParent(dialog);
    }

    // テキスト要素から "/ 136" のような形式を探す
    Condition condition = new PropertyCondition(
        AutomationElement.ControlTypeProperty, 
        ControlType.Text
    );
    AutomationElementCollection textElements = dialog.FindAll(
        TreeScope.Descendants, 
        condition
    );

    foreach (AutomationElement element in textElements)
    {
        string name = element.Current.Name;
        if (name.Contains("/"))
        {
            string[] parts = name.Split('/');
            if (parts.Length > 1)
            {
                string numberPart = parts[1].Trim();
                string digits = new string(
                    Array.FindAll(numberPart.ToCharArray(), char.IsDigit)
                );
                if (int.TryParse(digits, out int total))
                {
                    SendKeys.SendWait("{ESC}");
                    return total;
                }
            }
        }
    }

    SendKeys.SendWait("{ESC}");
    return -1;
}
```

### ページ送り方向

Kindle for PCのページ送りキーは書籍の方向によって異なる：

| 書籍タイプ | 方向 | 次ページ | 前ページ |
|-----------|------|---------|---------|
| 縦書き（日本語） | 右→左 | ← (Left) | → (Right) |
| 横書き（英語） | 左→右 | → (Right) | ← (Left) |

**実装:**

```csharp
public void SendNextPage(IntPtr hWnd, bool isRightToLeft)
{
    int key = isRightToLeft ? VK_LEFT : VK_RIGHT;
    SendKey(hWnd, key);
}

public void SendPrevPage(IntPtr hWnd, bool isRightToLeft)
{
    int key = isRightToLeft ? VK_RIGHT : VK_LEFT;
    SendKey(hWnd, key);
}
```

## 3. フルスクリーン切り替え

### F11キーの送信

Kindle for PCは `F11` キーでフルスクリーンモードを切り替える：

```csharp
public void ToggleFullScreen(IntPtr hWnd)
{
    SendKey(hWnd, VK_F11);
}

public void SendKey(IntPtr hWnd, int key)
{
    PostMessage(hWnd, WM_KEYDOWN, (IntPtr)key, IntPtr.Zero);
    Thread.Sleep(50);
    PostMessage(hWnd, WM_KEYUP, (IntPtr)key, IntPtr.Zero);
}
```

**定数:**
```csharp
const int VK_F11 = 0x7A;
const uint WM_KEYDOWN = 0x0100;
const uint WM_KEYUP = 0x0101;
```

## 4. 画像処理とPDF生成

### モノクロ変換の閾値

テキストの可読性を保つため、モノクロ変換の閾値は **180** が推奨：

```csharp
public const int MONOCHROME_THRESHOLD = 180;

// グレースケール変換後に2値化
int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
Color monoColor = gray >= MONOCHROME_THRESHOLD ? Color.White : Color.Black;
```

**理由:** 
- 128（中間値）では文字が潰れやすい
- 180にすることで、グレーの文字も黒として認識され、可読性が向上

### 見開きページの自動分割

Kindleの見開き表示を左右に分割してPDFに保存：

```csharp
if (splitDualPage && image.Width > image.Height)
{
    int halfWidth = image.Width / 2;
    
    // 右ページ（日本語の場合は先）
    Bitmap rightPage = new Bitmap(halfWidth, image.Height);
    using (Graphics g = Graphics.FromImage(rightPage))
    {
        g.DrawImage(image, 
            new Rectangle(0, 0, halfWidth, image.Height),
            new Rectangle(halfWidth, 0, halfWidth, image.Height),
            GraphicsUnit.Pixel);
    }
    
    // 左ページ
    Bitmap leftPage = new Bitmap(halfWidth, image.Height);
    using (Graphics g = Graphics.FromImage(leftPage))
    {
        g.DrawImage(image,
            new Rectangle(0, 0, halfWidth, image.Height),
            new Rectangle(0, 0, halfWidth, image.Height),
            GraphicsUnit.Pixel);
    }
    
    // 日本語の場合は右→左の順序
    if (isRightToLeft)
    {
        AddPageToPdf(rightPage);
        AddPageToPdf(leftPage);
    }
    else
    {
        AddPageToPdf(leftPage);
        AddPageToPdf(rightPage);
    }
}
```

### PDF圧縮設定

ファイルサイズを最小化するための推奨設定：

```csharp
PdfDocument document = new PdfDocument();
document.Options.CompressContentStreams = true;
document.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
```

## 5. クロップエリアの管理

### 複数パターンの保存

クロップエリアを複数パターン保存して切り替え可能にする：

```csharp
public class AppSettings
{
    public int MaxPatterns { get; set; } = 5;
    public int SelectedPatternIndex { get; set; } = 0;
    public List<Rectangle> CropPatterns { get; set; } = new List<Rectangle>();
    
    public Rectangle CropRect
    {
        get
        {
            if (SelectedPatternIndex >= 0 && 
                SelectedPatternIndex < CropPatterns.Count)
            {
                return CropPatterns[SelectedPatternIndex];
            }
            return Rectangle.Empty;
        }
        set
        {
            if (SelectedPatternIndex >= 0 && 
                SelectedPatternIndex < CropPatterns.Count)
            {
                CropPatterns[SelectedPatternIndex] = value;
            }
        }
    }
}
```

### オーバーレイでのビジュアル設定

半透明オーバーレイでクロップエリアを視覚的に設定：

```csharp
public class OverlayForm : Form
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        
        // 半透明の背景
        using (SolidBrush brush = new SolidBrush(Color.FromArgb(128, 0, 0, 0)))
        {
            e.Graphics.FillRectangle(brush, this.ClientRectangle);
        }
        
        // クロップエリアを透明に
        e.Graphics.FillRectangle(Brushes.Transparent, cropRect);
        
        // 枠線を描画
        using (Pen pen = new Pen(Color.Red, 2))
        {
            e.Graphics.DrawRectangle(pen, cropRect);
        }
    }
}
```

## 6. 設定の永続化

### JSON形式での保存

`System.Text.Json` を使用した設定の保存・読み込み：

```csharp
public void Save()
{
    string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
    {
        WriteIndented = true
    });
    File.WriteAllText(SETTINGS_FILE, json);
}

public static AppSettings Load()
{
    if (File.Exists(SETTINGS_FILE))
    {
        string json = File.ReadAllText(SETTINGS_FILE);
        return JsonSerializer.Deserialize<AppSettings>(json) 
            ?? new AppSettings();
    }
    return new AppSettings();
}
```

### 保存タイミング

設定は以下のタイミングで保存：

1. **フォーム終了時** (`Form1_FormClosing`)
2. **重要な設定変更時** (即座に保存が必要な場合)

```csharp
private void Form1_FormClosing(object sender, FormClosingEventArgs e)
{
    // UI状態を設定に反映
    _settings.Interval = int.Parse(txtInterval.Text);
    _settings.PageCount = int.Parse(txtPages.Text);
    _settings.CropRect = _cropRect;
    // ... 他の設定
    
    _settings.Save();
}
```

## 7. UI/UXのベストプラクティス

### Always On Top

アプリケーションを常に最前面に表示：

```csharp
this.TopMost = true;

// チェックボックスで切り替え可能に
chkAlwaysOnTop.CheckedChanged += (s, e) => 
{
    this.TopMost = chkAlwaysOnTop.Checked;
};
```

### タブ化UI

機能をタブで整理して使いやすく：

```csharp
TabControl tabControl = new TabControl
{
    Dock = DockStyle.Fill,
    Padding = new Point(10, 5)
};

TabPage tabHome = new TabPage("Home");
TabPage tabSettings = new TabPage("Settings");
TabPage tabCrop = new TabPage("Crop");
TabPage tabLog = new TabPage("Log");

tabControl.TabPages.Add(tabHome);
tabControl.TabPages.Add(tabSettings);
tabControl.TabPages.Add(tabCrop);
tabControl.TabPages.Add(tabLog);
```

### ツールチップによるガイダンス

```csharp
ToolTip toolTip = new ToolTip
{
    AutoPopDelay = 5000,
    InitialDelay = 500,
    ReshowDelay = 100,
    ShowAlways = true
};

toolTip.SetToolTip(chkAutoDetect, 
    "ページ遷移を自動検出します。ONの場合、Intervalは無効になります。");
```

## 8. エラーハンドリングとロギング

### ロガーの実装

```csharp
public static class Logger
{
    private static readonly string LOG_FILE = "app.log";
    
    public static void Info(string message)
    {
        Log("INFO", message);
    }
    
    public static void Warning(string message)
    {
        Log("WARNING", message);
    }
    
    public static void Error(string message, Exception ex = null)
    {
        string fullMessage = ex != null 
            ? $"{message}\n{ex.StackTrace}" 
            : message;
        Log("ERROR", fullMessage);
    }
    
    private static void Log(string level, string message)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string logEntry = $"[{level}] [{timestamp}] {message}";
        
        File.AppendAllText(LOG_FILE, logEntry + Environment.NewLine);
        
        // UIにも表示（txtLogがある場合）
        if (Application.OpenForms.Count > 0)
        {
            Form mainForm = Application.OpenForms[0];
            mainForm.Invoke((MethodInvoker)delegate
            {
                TextBox txtLog = mainForm.Controls.Find("txtLog", true)
                    .FirstOrDefault() as TextBox;
                if (txtLog != null)
                {
                    txtLog.AppendText(logEntry + Environment.NewLine);
                }
            });
        }
    }
}
```

## 9. 既知の問題と回避策

### 問題1: SendKeysがフォーカスを失う

**症状:** `SendKeys.SendWait` がフォーカスを失って動作しない

**解決策:** 先に `SetForegroundWindow` でウィンドウをアクティブにする

```csharp
SetForegroundWindow(kindleWindow);
Thread.Sleep(100);
SendKeys.SendWait("^g");
```

### 問題2: ページ遷移の検出が不安定

**症状:** 画像比較でページ遷移を検出できない

**解決策:** 連続2回の一致で安定判定

```csharp
int stableCount = 0;
const int REQUIRED_STABLE_COUNT = 2;

while (stableCount < REQUIRED_STABLE_COUNT)
{
    Bitmap current = CaptureWindow(bounds);
    if (AreImagesSame(previous, current))
    {
        stableCount++;
    }
    else
    {
        stableCount = 0;
    }
    previous = current;
    Thread.Sleep(100);
}
```

### 問題3: 最小化からの復元でハング

**症状:** 最小化状態から `ShowWindow(SW_MAXIMIZE)` を呼ぶとハング

**解決策:** 先に復元してから最大化

```csharp
if (IsIconic(hWnd))
{
    ShowWindow(hWnd, SW_RESTORE);
    Thread.Sleep(200);
}
ShowWindow(hWnd, SW_MAXIMIZE);
```

## 参考資料

- [Kindle Window Detection](./KINDLE_WINDOW_DETECTION.md) - ウィンドウ検出の詳細
- [SPECS.md](./SPECS.md) - 要件定義書
- [Microsoft Docs - Windows API](https://learn.microsoft.com/en-us/windows/win32/)

## 変更履歴

- 2025-11-25: 初版作成 - Kindle自動化のベストプラクティス集
