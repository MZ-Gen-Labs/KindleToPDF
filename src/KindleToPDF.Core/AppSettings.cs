using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KindleToPDF
{
    public enum FileNameMode { Overwrite, Sequential }
    public enum SequentialType { Number, Alphabet, DateTime }
    public enum CaptureMode { Continuous, Manual }
    public enum ImageColorMode { Monochrome, Grayscale, Indexed256, HighColor, FullColor }
    public enum PdfImageFormat { Jpeg, Png }

    public class AppSettings
    {
        public AppSettings()
        {
            EnsurePatterns();
        }
        public int Interval { get; set; } = 1000;
        public int PageCount { get; set; } = 10;
        public bool AutoDetect { get; set; } = true;
        public bool StopAtLastPage { get; set; } = true;
        public bool AlwaysOnTop { get; set; } = true;
        public int DpiIndex { get; set; } = 0;
        public int PageDirection { get; set; } = 0; // 0: R2L (JP), 1: L2R (EN)
        
        // Capture Mode
        public CaptureMode CaptureMode { get; set; } = CaptureMode.Continuous;
        
        // Image Compression
        public ImageColorMode ColorMode { get; set; } = ImageColorMode.Grayscale;
        public PdfImageFormat ImageFormat { get; set; } = PdfImageFormat.Jpeg;
        public int JpegQuality { get; set; } = 80; // 60-100
        public int MonochromeThreshold { get; set; } = 180; // 0-255, default 180 for better text visibility
        public bool SplitDualPage { get; set; } = false; // Auto-split dual pages
        
        // Naming Options
        public string OutputDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public string BaseFileName { get; set; } = "KindleBook";
        public FileNameMode Mode { get; set; } = FileNameMode.Sequential;
        public SequentialType SeqType { get; set; } = SequentialType.Number;
        public int StartNumber { get; set; } = 1;
        public int NumberDigits { get; set; } = 3;
        public string StartChar { get; set; } = "a";
        public string DateTimeFormat { get; set; } = "yyyyMMdd";
        
        public List<Rectangle> CropPatterns { get; set; } = new List<Rectangle>();
        public int SelectedPatternIndex { get; set; } = 0;
        public int MaxPatterns { get; set; } = 5;

        [JsonIgnore]
        public Rectangle CropRect
        {
            get 
            {
                EnsurePatterns();
                if (SelectedPatternIndex >= 0 && SelectedPatternIndex < CropPatterns.Count)
                {
                    return CropPatterns[SelectedPatternIndex];
                }
                return Rectangle.Empty;
            }
            set
            {
                EnsurePatterns();
                if (SelectedPatternIndex >= 0 && SelectedPatternIndex < CropPatterns.Count)
                {
                    CropPatterns[SelectedPatternIndex] = value;
                }
            }
        }

        public void EnsurePatterns()
        {
            if (CropPatterns == null) CropPatterns = new List<Rectangle>();
            
            while (CropPatterns.Count < MaxPatterns)
            {
                CropPatterns.Add(Rectangle.Empty);
            }
            
            if (SelectedPatternIndex >= MaxPatterns) SelectedPatternIndex = MaxPatterns - 1;
            if (SelectedPatternIndex < 0) SelectedPatternIndex = 0;
        }

        private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.SETTINGS_FILE_NAME);

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    settings.EnsurePatterns();
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load settings from {SettingsPath}: {ex.Message}", ex);
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
                Logger.Error($"Failed to save settings to {SettingsPath}: {ex.Message}", ex);
            }
        }
    }
}
