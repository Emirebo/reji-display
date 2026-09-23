using System;
using System.IO;
using RejiDisplay.Services;
using Xunit;

namespace RejiDisplay.Tests
{
    public class AppLoggerTests
    {
        [Fact]
        public void AppLogger_LogInfo_WritesToFileAndTriggersEvent()
        {
            string tempLogDir = Path.Combine(Path.GetTempPath(), $"reji_log_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempLogDir);
            string tempLogFile = Path.Combine(tempLogDir, "test_app.log");
            AppLogger.LogFilePath = tempLogFile;

            bool eventFired = false;
            string? loggedMessage = null;

            EventHandler<LogEventArgs> handler = (s, e) =>
            {
                eventFired = true;
                loggedMessage = e.Message;
            };

            AppLogger.LogOccurred += handler;

            try
            {
                AppLogger.LogInfo("Test log entry");

                Assert.True(eventFired);
                Assert.Equal("Test log entry", loggedMessage);
                Assert.True(File.Exists(tempLogFile));

                string content = File.ReadAllText(tempLogFile);
                Assert.Contains("[INFO] Test log entry", content);
            }
            finally
            {
                AppLogger.LogOccurred -= handler;
                if (Directory.Exists(tempLogDir))
                {
                    try { Directory.Delete(tempLogDir, true); } catch { }
                }
            }
        }

        [Fact]
        public void AppLogger_LogErrorWithException_WritesStackTrace()
        {
            string tempLogDir = Path.Combine(Path.GetTempPath(), $"reji_log_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempLogDir);
            string tempLogFile = Path.Combine(tempLogDir, "test_app.log");
            AppLogger.LogFilePath = tempLogFile;

            try
            {
                var ex = new InvalidOperationException("Simulated error");
                AppLogger.LogError("An error occurred", ex);

                Assert.True(File.Exists(tempLogFile));
                string content = File.ReadAllText(tempLogFile);
                Assert.Contains("[ERROR] An error occurred", content);
                Assert.Contains("InvalidOperationException: Simulated error", content);
            }
            finally
            {
                if (Directory.Exists(tempLogDir))
                {
                    try { Directory.Delete(tempLogDir, true); } catch { }
                }
            }
        }
    }
}
