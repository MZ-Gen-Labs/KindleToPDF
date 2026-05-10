using System;
using System.IO;
using System.Text.Json;
// ※ ImageSharpのRectangleを使うためのエイリアス
using Rectangle = SixLabors.ImageSharp.Rectangle; 

namespace KindleToPDF.Core
{
    // --- 旧Windows版で使っていた列挙型を復元 ---
    public enum FileNameMode { Overwrite, Sequential }
    public enum SequentialType { Number, Alphabet, DateTime }
    public enum CaptureMode { Continuous, Manual }
    public enum ImageColorMode { Monochrome, Grayscale, Indexed256, HighColor, FullColor }
    public enum PdfImageFormat { Jpeg, Png }

    public class AppSettings
    {
        // 基本設定
        public int PageCount { get; set; } = 100;
        public int Interval { get; set; } = 1000;
        public int PageDirection { get; set; } = 0; // 0:右開き, 1:左開き
        public bool StopAtLastPage { get; set; } = true;
        public bool AutoDetect { get; set; } = true;
        
        // 出力設定
        public string OutputDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public string BaseFileName { get; set; } = "KindleBook";
        public FileNameMode Mode { get; set; } = FileNameMode.Sequential;
        
        // 連番設定
        public SequentialType SeqType { get; set; } = SequentialType.Number;
        public int StartNumber { get; set; } = 1;
        public int NumberDigits { get; set; } = 3;
        public string StartChar { get; set; } = "A";
        public string DateTimeFormat { get; set; } = "_yyyyMMdd_HHmmss";

        // （※将来ステップ用の設定項目も保持しておきます）
        public int DpiIndex { get; set; } = 0;
        public bool SplitDualPage { get; set; } = false;
        public ImageColorMode ColorMode { get; set; } = ImageColorMode.Grayscale;
        public PdfImageFormat ImageFormat { get; set; } = PdfImageFormat.Jpeg;
        public Rectangle CropRect { get; set; } = new Rectangle(0, 0, 0, 0);

        // --- 設定の保存・読み込みロジック ---
        private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch { return new AppSettings(); }
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"設定の保存に失敗: {ex.Message}");
            }
        }
    }
}
