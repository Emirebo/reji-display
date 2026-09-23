using System;
using System.IO;

namespace RejiDisplay.Services
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    public class LogEventArgs : EventArgs
    {
        public LogLevel Level { get; }
        public string Message { get; }
        public Exception? Exception { get; }
        public DateTime Timestamp { get; }

        public LogEventArgs(LogLevel level, string message, Exception? exception = null)
        {
            Level = level;
            Message = message;
            Exception = exception;
            Timestamp = DateTime.Now;
        }
    }

    public static class AppLogger
    {
        private static readonly object _lock = new();
        private static string? _logFilePath;

        public static event EventHandler<LogEventArgs>? LogOccurred;

        public static string LogFilePath
        {
            get
            {
                if (_logFilePath == null)
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RejiDisplay", "logs");
                    Directory.CreateDirectory(dir);
                    _logFilePath = Path.Combine(dir, "app.log");
                }
                return _logFilePath;
            }
            internal set => _logFilePath = value;
        }

        public static void LogInfo(string message)
        {
            WriteLog(LogLevel.Info, message, null);
        }

        public static void LogWarning(string message, Exception? ex = null)
        {
            WriteLog(LogLevel.Warning, message, ex);
        }

        public static void LogError(string message, Exception? ex = null)
        {
            WriteLog(LogLevel.Error, message, ex);
        }

        private static void WriteLog(LogLevel level, string message, Exception? ex)
        {
            var args = new LogEventArgs(level, message, ex);
            
            try
            {
                LogOccurred?.Invoke(null, args);
            }
            catch
            {
                // Ignore listener exceptions
            }

            string formatted = $"[{args.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{level.ToString().ToUpper()}] {message}";
            if (ex != null)
            {
                formatted += $"{Environment.NewLine}Exception: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}";
            }

            lock (_lock)
            {
                try
                {
                    string path = LogFilePath;
                    // Rotate log file if > 5MB
                    if (File.Exists(path) && new FileInfo(path).Length > 5 * 1024 * 1024)
                    {
                        string oldLog = path + ".old";
                        File.Copy(path, oldLog, overwrite: true);
                        File.Delete(path);
                    }

                    File.AppendAllText(path, formatted + Environment.NewLine);
                }
                catch
                {
                    // Fallback: silently fail logging if disk access fails
                }
            }
        }
    }
}
