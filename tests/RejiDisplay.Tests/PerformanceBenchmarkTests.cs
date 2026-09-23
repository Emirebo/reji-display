using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using RejiDisplay.Models;
using RejiDisplay.Services;
using Xunit;

namespace RejiDisplay.Tests
{
    public class PerformanceBenchmarkTests
    {
        [Fact]
        public void Benchmark_SettingsSave_DebouncedVsSynchronous()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"reji_bench_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // 1. Measure 100 rapid setting updates with synchronous SaveSettings
                string syncPath = Path.Combine(tempDir, "sync.json");
                var syncService = new SettingsService(syncPath);
                var settings = new AppSettings();

                Stopwatch swSync = Stopwatch.StartNew();
                for (int i = 0; i < 100; i++)
                {
                    settings.LeftOutput.DraftLayout.OffsetX = i;
                    syncService.SaveSettings(settings); // 100 disk writes
                }
                swSync.Stop();

                // 2. Measure 100 rapid setting updates with Debounced SaveSettings
                string debouncedPath = Path.Combine(tempDir, "debounced.json");
                var debouncedService = new SettingsService(debouncedPath);

                Stopwatch swDebounced = Stopwatch.StartNew();
                for (int i = 0; i < 100; i++)
                {
                    settings.LeftOutput.DraftLayout.OffsetX = i;
                    debouncedService.SaveSettingsDebounced(settings, 100); // Debounced
                }
                debouncedService.FlushPendingSave();
                swDebounced.Stop();

                // Log timing comparison
                double syncMs = swSync.Elapsed.TotalMilliseconds;
                double debouncedMs = swDebounced.Elapsed.TotalMilliseconds;

                AppLogger.LogInfo($"[PERF BENCHMARK] Synchronous 100 Saves: {syncMs:F2} ms | Debounced 100 Saves: {debouncedMs:F2} ms");

                Assert.True(debouncedMs <= syncMs, $"Debounced ({debouncedMs:F2} ms) should be faster/equal than synchronous ({syncMs:F2} ms)");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }
    }
}
