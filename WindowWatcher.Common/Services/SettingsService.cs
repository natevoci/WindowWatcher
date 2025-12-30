using System.Text.Json;
using WindowWatcher.Common.Models;

namespace WindowWatcher.Common.Services;

public class SettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowWatcher",
        "settings.json");

    private static readonly string SettingsDirectory = Path.GetDirectoryName(SettingsFilePath) ?? string.Empty;

    private AppSettings? _cachedSettings;

    public AppSettings LoadSettings()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                ResolveRelativePaths(settings);
                _cachedSettings = settings;
            }
            else
            {
                var settings = new AppSettings();
                ResolveRelativePaths(settings);
                _cachedSettings = settings;
            }
        }
        catch
        {
            var settings = new AppSettings();
            ResolveRelativePaths(settings);
            _cachedSettings = settings;
        }

        return _cachedSettings;
    }

    private void ResolveRelativePaths(AppSettings settings)
    {
        if (!Path.IsPathRooted(settings.SnapshotsPath))
        {
            settings.SnapshotsPath = Path.GetFullPath(Path.Combine(SettingsDirectory, settings.SnapshotsPath));
        }
        if (!Path.IsPathRooted(settings.DataDirectory))
        {
            settings.DataDirectory = Path.GetFullPath(Path.Combine(SettingsDirectory, settings.DataDirectory));
        }

        if (settings.ScreenshotIntervalSeconds <= 0)
        {
            settings.ScreenshotIntervalSeconds = 5;
        }

        if (string.IsNullOrWhiteSpace(settings.ScreenshotCaptureTarget))
        {
            settings.ScreenshotCaptureTarget = "AllScreens";
        }

        if (settings.ScreenshotRetentionDays <= 0)
        {
            settings.ScreenshotRetentionDays = 30;
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(SettingsFilePath, json);
            _cachedSettings = settings;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save settings: {ex.Message}", ex);
        }
    }

    public void ClearCache()
    {
        _cachedSettings = null;
    }
}
