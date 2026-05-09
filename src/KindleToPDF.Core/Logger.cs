using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace KindleToPDF
{
    /// <summary>
    /// Centralized logging utility for the application
    /// </summary>
    public static class Logger
    {
        private static readonly object _lockObject = new object();
        private static bool _isDebugMode = false;
        private static string _logFilePath = "app_log.txt";

        /// <summary>
        /// Enable or disable debug mode
        /// </summary>
        public static bool IsDebugMode
        {
            get => _isDebugMode;
            set => _isDebugMode = value;
        }

        /// <summary>
        /// Set the log file path
        /// </summary>
        public static string LogFilePath
        {
            get => _logFilePath;
            set => _logFilePath = value;
        }

        /// <summary>
        /// Log a debug message (only in debug mode)
        /// </summary>
        public static void Debug(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            if (!_isDebugMode) return;

            string fileName = Path.GetFileName(filePath);
            string logMessage = $"[DEBUG] [{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{fileName}:{lineNumber}] {memberName}: {message}";
            WriteToFile(logMessage);
        }

        /// <summary>
        /// Log an info message
        /// </summary>
        public static void Info(string message)
        {
            string logMessage = $"[INFO] [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            WriteToFile(logMessage);
        }

        /// <summary>
        /// Log a warning message
        /// </summary>
        public static void Warning(string message)
        {
            string logMessage = $"[WARNING] [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            WriteToFile(logMessage);
        }

        /// <summary>
        /// Log an error message
        /// </summary>
        public static void Error(string message, Exception? ex = null)
        {
            string logMessage = $"[ERROR] [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            if (ex != null)
            {
                logMessage += $"\nException: {ex.Message}\nStackTrace: {ex.StackTrace}";
            }
            WriteToFile(logMessage);
        }

        /// <summary>
        /// Clear the log file
        /// </summary>
        public static void ClearLog()
        {
            try
            {
                lock (_lockObject)
                {
                    if (File.Exists(_logFilePath))
                    {
                        File.Delete(_logFilePath);
                    }
                }
            }
            catch
            {
                // Silently fail if we can't clear the log
            }
        }

        private static void WriteToFile(string message)
        {
            try
            {
                lock (_lockObject)
                {
                    File.AppendAllText(_logFilePath, message + Environment.NewLine);
                }
            }
            catch
            {
                // Silently fail if we can't write to the log
                // We don't want logging failures to crash the application
            }
        }
    }
}
