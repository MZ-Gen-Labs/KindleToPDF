# Kindle Window Detection - Technical Specification

## 概要

Kindle for PCアプリケーションは複数のウィンドウを持ち、`Process.MainWindowHandle`は実際のメインウィンドウではなく、内部で使用される隠しウィンドウを返す。このため、ウィンドウ操作（最小化、復元、最大化）を正しく行うには、すべてのウィンドウを列挙して正しいメインウィンドウを特定する必要がある。

## Kindleプロセスのウィンドウ構造

Kindle for PCプロセスは、通常7つのウィンドウを保持している：

| # | Title | ClassName | 用途 | IsIconic | IsVisible |
|---|-------|-----------|------|----------|-----------|
| 1 | (空) | Internet Explorer_Hidden | 内部ブラウザエンジン | False | True |
| 2 | (空) | VSyncHelper-* | 垂直同期ヘルパー | False | False |
| 3 | コレクションのインポート | Qt5QWindowIcon | インポートダイアログ | False | False |
| 4 | Kindle | Qt5QWindowIcon | サブダイアログ | False | False |
| **5** | **Kindle for PC msi** | **Qt5QWindowIcon** | **メインウィンドウ** | **True/False** | **True** |
| 6 | MSCTFIME UI | MSCTFIME UI | IME UI | False | False |
| 7 | Default IME | IME | IME | False | False |

### メインウィンドウ（Window 5）の特徴

**通常状態:**
- Title: "Kindle for PC msi" または "Kindle for PC [デバイス名] - [書籍名]"
- ClassName: `Qt5QWindowIcon`
- IsIconic: `False`
- IsVisible: `True`
- Bounds: 通常の画面座標とサイズ

**最小化状態:**
- IsIconic: `True`
- Bounds: `{X=-32000, Y=-32000, Width=160, Height=28}`
  - `-32000` はWindowsが最小化ウィンドウを配置する特殊座標
- rcNormalPosition: 復元時の位置とサイズ（通常はフルスクリーン `{0,0,1920,1080}`）

## 問題の詳細

### 従来の実装（誤り）

```csharp
public IntPtr GetKindleWindow()
{
    Process[] processes = Process.GetProcessesByName("Kindle");
    if (processes.Length > 0)
    {
        return processes[0].MainWindowHandle;  // Window 1を返す
    }
    return IntPtr.Zero;
}
```

**問題点:**
- `MainWindowHandle` は **Window 1** (Internet Explorer_Hidden) を返す
- Window 1は `IsIconic: False` なので、最小化されていないと誤判定される
- そのため、`ShowWindow(SW_RESTORE)` などが何も実行しない

### 正しい実装

```csharp
public IntPtr GetKindleWindow()
{
    Process[] processes = Process.GetProcessesByName("Kindle");
    if (processes.Length > 0)
    {
        Process kindleProcess = processes[0];
        uint kindleProcessId = (uint)kindleProcess.Id;
        
        List<IntPtr> kindleWindows = new List<IntPtr>();

        // すべてのトップレベルウィンドウを列挙
        EnumWindows((hWnd, lParam) =>
        {
            GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == kindleProcessId)
            {
                kindleWindows.Add(hWnd);
            }
            return true;
        }, IntPtr.Zero);

        // メインウィンドウを特定
        foreach (IntPtr hWnd in kindleWindows)
        {
            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            
            StringBuilder title = new StringBuilder(256);
            GetWindowText(hWnd, title, title.Capacity);

            // "for PC"を含むQt5QWindowIconウィンドウがメインウィンドウ
            if (className.ToString() == "Qt5QWindowIcon" && 
                title.ToString().Contains("for PC"))
            {
                return hWnd;
            }
        }

        // フォールバック
        return kindleProcess.MainWindowHandle;
    }
    return IntPtr.Zero;
}
```

## 必要なWin32 API

### EnumWindows
すべてのトップレベルウィンドウを列挙する。

```csharp
[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
```

### GetWindowThreadProcessId
ウィンドウが属するプロセスIDを取得する。

```csharp
[DllImport("user32.dll")]
static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
```

### GetClassName
ウィンドウのクラス名を取得する。

```csharp
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
```

### GetWindowText
ウィンドウのタイトルを取得する。

```csharp
[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
```

## ウィンドウ操作のベストプラクティス

### 最小化からの復元

```csharp
public void RestoreKindleWindow(IntPtr hWnd)
{
    if (IsIconic(hWnd))
    {
        ShowWindow(hWnd, SW_RESTORE);
        Thread.Sleep(200);
    }
    SetForegroundWindow(hWnd);
}
```

### 最大化

```csharp
public void MaximizeKindleWindow(IntPtr hWnd)
{
    // 最小化されている場合は先に復元
    if (IsIconic(hWnd))
    {
        ShowWindowAsync(hWnd, SW_RESTORE);
        
        // 復元完了を待つ
        int retries = 0;
        while (IsIconic(hWnd) && retries < 10)
        {
            Thread.Sleep(200);
            retries++;
        }
    }

    // 最大化
    ShowWindowAsync(hWnd, SW_MAXIMIZE);
    SetForegroundWindow(hWnd);
}
```

## トラブルシューティング

### 診断用コード

すべてのKindleウィンドウの状態を列挙してログに出力する：

```csharp
public void DiagnosticsEnumerateKindleWindows()
{
    Process[] processes = Process.GetProcessesByName("Kindle");
    if (processes.Length == 0) return;

    uint kindleProcessId = (uint)processes[0].Id;
    List<IntPtr> kindleWindows = new List<IntPtr>();

    EnumWindows((hWnd, lParam) =>
    {
        GetWindowThreadProcessId(hWnd, out uint processId);
        if (processId == kindleProcessId)
        {
            kindleWindows.Add(hWnd);
        }
        return true;
    }, IntPtr.Zero);

    for (int i = 0; i < kindleWindows.Count; i++)
    {
        IntPtr hwnd = kindleWindows[i];
        
        StringBuilder title = new StringBuilder(256);
        GetWindowText(hwnd, title, title.Capacity);
        
        StringBuilder className = new StringBuilder(256);
        GetClassName(hwnd, className, className.Capacity);
        
        bool isVisible = IsWindowVisible(hwnd);
        bool isIconic = IsIconic(hwnd);
        Rectangle bounds = GetWindowBounds(hwnd);
        GetWindowPlacement(hwnd, out WINDOWPLACEMENT placement);

        Logger.Info($"Window {i + 1}:");
        Logger.Info($"  Handle: {hwnd}");
        Logger.Info($"  Title: {title}");
        Logger.Info($"  ClassName: {className}");
        Logger.Info($"  IsVisible: {isVisible}");
        Logger.Info($"  IsIconic: {isIconic}");
        Logger.Info($"  Bounds: {bounds}");
        Logger.Info($"  ShowCmd: {placement.showCmd}");
    }
}
```

### よくある問題

**Q: `ShowWindow(SW_RESTORE)` が何も実行しない**  
A: 間違ったウィンドウハンドルを使用している可能性が高い。上記の診断コードで正しいウィンドウを特定すること。

**Q: ウィンドウが画面に表示されない**  
A: ウィンドウが画面外に配置されている可能性がある。`MoveWindow` または `SetWindowPos` で画面内の座標に移動させる。

**Q: 操作がハングする**  
A: `ShowWindow(SW_MAXIMIZE)` を最小化状態のウィンドウに直接実行するとハングする場合がある。先に `SW_RESTORE` で復元してから最大化すること。

## 参考資料

- [ShowWindow function (Microsoft Docs)](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindow)
- [EnumWindows function (Microsoft Docs)](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumwindows)
- [GetWindowPlacement function (Microsoft Docs)](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowplacement)

## 変更履歴

- 2025-11-25: 初版作成 - Kindleウィンドウ検出問題の解決
