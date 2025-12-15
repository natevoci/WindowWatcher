using System.Text.Json;
using WindowWatcher.Common.Models;

namespace WindowWatcher.Common.Services;

public class SettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowWatcher",
        "settings.json");

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
                _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                _cachedSettings = new AppSettings();
            }
        }
        catch
        {
            _cachedSettings = new AppSettings();
        }

        return _cachedSettings;
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
