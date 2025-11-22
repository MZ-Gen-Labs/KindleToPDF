using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KindleToPDF
{
    public class AppSettings
    {
        public int Interval { get; set; } = 1000;
        public int PageCount { get; set; } = 10;
        public bool AutoDetect { get; set; } = true;
        public bool StopAtLastPage { get; set; } = true;
        public bool AlwaysOnTop { get; set; } = true;
        public int DpiIndex { get; set; } = 0;
        
        public int CropX { get; set; }
        public int CropY { get; set; }
        public int CropW { get; set; }
        public int CropH { get; set; }

        [JsonIgnore]
        public Rectangle CropRect
        {
            get => new Rectangle(CropX, CropY, CropW, CropH);
            set
            {
                CropX = value.X;
                CropY = value.Y;
                CropW = value.Width;
                CropH = value.Height;
            }
        }

        private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch 
            {
                // Ignore errors and return default
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
            catch 
            {
                // Ignore errors
            }
        }
    }
}
