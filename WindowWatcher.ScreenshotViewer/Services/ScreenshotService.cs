using System.IO;
using System.Globalization;
using WindowWatcher.Common.Services;
using WindowWatcher.ScreenshotViewer.Models;

namespace WindowWatcher.ScreenshotViewer.Services;

public class ScreenshotService
{
    private readonly SettingsService _settingsService;
    
    public ScreenshotService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    private string? _screenshotsPathOverride;

    public string ScreenshotsPath => _screenshotsPathOverride ?? _settingsService.LoadSettings().ScreenshotsPath;

    public void SetScreenshotsPathOverride(string screenshotsPath)
    {
        _screenshotsPathOverride = screenshotsPath;
    }

    private string GetScreenshotsPath() => ScreenshotsPath;

    private string? GetArchivePath()
    {
        var settings = _settingsService.LoadSettings();
        if (string.IsNullOrWhiteSpace(settings.ScreenshotsArchivePath))
            return null;

        // Only include archive when the viewer is showing the real screenshots folder, not a custom override.
        if (_screenshotsPathOverride != null &&
            !string.Equals(Path.GetFullPath(_screenshotsPathOverride), Path.GetFullPath(settings.ScreenshotsPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Directory.Exists(settings.ScreenshotsArchivePath) ? settings.ScreenshotsArchivePath : null;
    }

    public List<DateTime> GetAvailableDates()
    {
        var dates = new HashSet<DateTime>();

        CollectDates(GetScreenshotsPath(), dates);

        var archivePath = GetArchivePath();
        if (archivePath != null)
            CollectDates(archivePath, dates);

        return dates.OrderByDescending(d => d).ToList();
    }

    private static void CollectDates(string folder, HashSet<DateTime> dates)
    {
        if (!Directory.Exists(folder))
            return;
        foreach (var directory in Directory.GetDirectories(folder))
        {
            var dirName = Path.GetFileName(directory);
            if (DateTime.TryParseExact(dirName, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                dates.Add(date);
            }
        }
    }
    public List<ScreenshotInfo> GetScreenshotsForDate(DateTime date)
    {
        var screenshots = new List<ScreenshotInfo>();
        var dateStr = date.ToString("yyyy-MM-dd");

        LoadScreenshotsFromFolder(Path.Combine(GetScreenshotsPath(), dateStr), date, screenshots);

        var archivePath = GetArchivePath();
        if (archivePath != null)
            LoadScreenshotsFromFolder(Path.Combine(archivePath, dateStr), date, screenshots);

        screenshots = screenshots.OrderBy(s => s.TimeOfDay).ToList();
        for (int i = 0; i < screenshots.Count; i++)
        {
            screenshots[i] = screenshots[i] with { Index = i };
        }

        return screenshots;
    }

    private void LoadScreenshotsFromFolder(string folder, DateTime date, List<ScreenshotInfo> screenshots)
    {
        if (!Directory.Exists(folder))
            return;

        var imageFiles = Directory.EnumerateFiles(folder)
            .Where(f =>
            {
                var extension = Path.GetExtension(f).ToLowerInvariant();
                return extension is ".png" or ".jpg" or ".jpeg";
            });

        foreach (var filePath in imageFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (TryParseTimestamp(fileName, out var timeOfDay))
            {
                screenshots.Add(new ScreenshotInfo
                {
                    FilePath = filePath,
                    TimeOfDay = timeOfDay,
                    Date = date,
                    Index = 0 // Will be set after sorting
                });
            }
        }
    }

    private bool TryParseTimestamp(string fileName, out TimeSpan timeOfDay)
    {
        // Expected format: HH.MM.SS.XX or HH.MM.SS.XXXX
        var parts = fileName.Split('.');
        
        if (parts.Length == 4 &&
            int.TryParse(parts[0], out var hours) &&
            int.TryParse(parts[1], out var minutes) &&
            int.TryParse(parts[2], out var seconds))
        {
            try
            {
                var baseTime = new TimeSpan(hours, minutes, seconds);

                if (parts[3].Length == 2 && int.TryParse(parts[3], out var centiseconds))
                {
                    timeOfDay = baseTime + TimeSpan.FromMilliseconds(centiseconds * 10);
                    return true;
                }

                if (parts[3].Length == 4 && int.TryParse(parts[3], out var fractionalSeconds))
                {
                    timeOfDay = baseTime + TimeSpan.FromTicks(fractionalSeconds * 1000L);
                    return true;
                }
            }
            catch
            {
                // Fall through to false return below.
            }
        }
        
        timeOfDay = TimeSpan.Zero;
        return false;
    }

    public ScreenshotInfo? FindScreenshotBeforeTime(List<ScreenshotInfo> screenshots, TimeSpan targetTime)
    {
        return screenshots.LastOrDefault(s => s.TimeOfDay < targetTime);
    }

    public ScreenshotInfo? FindScreenshotAfterTime(List<ScreenshotInfo> screenshots, TimeSpan targetTime)
    {
        return screenshots.FirstOrDefault(s => s.TimeOfDay > targetTime);
    }
}
