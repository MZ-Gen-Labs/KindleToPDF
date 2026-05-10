using System;

namespace KindleToPDF.Core
{
    /// <summary>
    /// Application-wide constants
    /// </summary>
    public static class Constants
    {
        // Virtual Key Codes
        public const int VK_LEFT = 0x25;
        public const int VK_RIGHT = 0x27;
        public const int VK_HOME = 0x24;
        public const int VK_NEXT = 0x22; // PageDown
        public const int VK_F11 = 0x7A;
        public const int VK_DELETE = 0x2E;

        // Window Messages
        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_KEYUP = 0x0101;

        // Window Styles
        public const int SW_RESTORE = 9;
        public const int SW_MAXIMIZE = 3;
        public const int SW_MINIMIZE = 6;
        public const int SW_SHOW = 5;
        public const int SW_SHOWNA = 8;
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_LAYERED = 0x80000;
        public const int WS_EX_TRANSPARENT = 0x20;

        // UI Constants
        public const int HANDLE_SIZE = 10;
        public const int DEFAULT_INTERVAL_MS = 1000;
        public const int DEFAULT_PAGE_COUNT = 10;
        public const int DEFAULT_JPEG_QUALITY = 80;
        public const int MIN_JPEG_QUALITY = 60;
        public const int MAX_JPEG_QUALITY = 100;
        public const int DEFAULT_MAX_PATTERNS = 5;

        // Timing Constants
        public const int WINDOW_HIDE_DELAY_MS = 200;
        public const int WINDOW_RESTORE_DELAY_MS = 200;
        public const int KEY_PRESS_DELAY_MS = 50;
        public const int DIALOG_WAIT_MS = 500;
        public const int FULLSCREEN_TOGGLE_DELAY_MS = 100;
        public const int PAGE_TURN_CHECK_INTERVAL_MS = 100;
        public const int MAX_PAGE_TURN_RETRIES = 40;
        public const int STABLE_IMAGE_COUNT = 2;

        // File and Directory Names
        public const string TEMP_DIRECTORY_NAME = "KindleToPDF_Temp";
        public const string SETTINGS_FILE_NAME = "settings.json";
        public const string DEFAULT_OUTPUT_FILENAME = "output.pdf";
        public const string PAGE_FILE_FORMAT = "page_{0:D4}.png";
        public const string PROCESSED_FILE_PREFIX = "processed_";

        // Process Names
        public const string KINDLE_PROCESS_NAME = "Kindle";

        // Default Crop Margin (10% of screen)
        public const double DEFAULT_CROP_MARGIN_PERCENT = 0.1;

        // Image Comparison
        public const int IMAGE_THRESHOLD = 128; // For monochrome conversion

        // Color Conversion Weights (ITU-R BT.601)
        public const double RED_WEIGHT = 0.299;
        public const double GREEN_WEIGHT = 0.587;
        public const double BLUE_WEIGHT = 0.114;
    }
}
