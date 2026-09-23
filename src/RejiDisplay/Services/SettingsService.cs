using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RejiDisplay.Models;

namespace RejiDisplay.Services
{
    public class SettingsService
    {
        private readonly string _settingsFilePath;
        private readonly object _saveLock = new();
        private CancellationTokenSource? _debounceCts;
        private AppSettings? _pendingSettings;

        public SettingsService(string? customPath = null)
        {
            if (!string.IsNullOrWhiteSpace(customPath))
            {
                _settingsFilePath = customPath;
            }
            else
            {
                string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RejiDisplay");
                Directory.CreateDirectory(appDataDir);
                _settingsFilePath = Path.Combine(appDataDir, "settings.json");
            }
        }

        public string SettingsFilePath => _settingsFilePath;

        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        settings.Migrate();
                        AppLogger.LogInfo("Ayar dosyası başarıyla yüklendi.");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"Ayar dosyası okunurken hata oluştu, varsayılan ayarlara dönülüyor: {ex.Message}", ex);
            }

            return new AppSettings();
        }

        public bool SaveSettings(AppSettings settings)
        {
            lock (_saveLock)
            {
                // Cancel any pending debounced save as we are performing an immediate save
                _debounceCts?.Cancel();
                _pendingSettings = null;

                return SaveInternal(settings);
            }
        }

        public void SaveSettingsDebounced(AppSettings settings, int delayMs = 300)
        {
            lock (_saveLock)
            {
                _pendingSettings = settings;
                _debounceCts?.Cancel();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;

                Task.Delay(delayMs, token).ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully && !token.IsCancellationRequested)
                    {
                        AppSettings? toSave;
                        lock (_saveLock)
                        {
                            toSave = _pendingSettings;
                            _pendingSettings = null;
                        }

                        if (toSave != null)
                        {
                            SaveInternal(toSave);
                        }
                    }
                }, TaskScheduler.Default);
            }
        }

        public void FlushPendingSave()
        {
            lock (_saveLock)
            {
                if (_pendingSettings != null)
                {
                    _debounceCts?.Cancel();
                    SaveInternal(_pendingSettings);
                    _pendingSettings = null;
                }
            }
        }

        private bool SaveInternal(AppSettings settings)
        {
            try
            {
                string dir = Path.GetDirectoryName(_settingsFilePath)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string tempPath = _settingsFilePath + ".tmp";
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);

                File.WriteAllText(tempPath, json);
                File.Copy(tempPath, _settingsFilePath, overwrite: true);
                File.Delete(tempPath);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Settings save failed: {ex.Message}", ex);
                return false;
            }
        }
    }
}
