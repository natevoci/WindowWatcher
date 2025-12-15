using System.IO;
using System.Globalization;
using WindowWatcher.Common.Services;
using WindowWatcher.ScreenshotViewer.Models;

namespace WindowWatcher.ScreenshotViewer.Services;

public class SnapshotService
{
    private readonly SettingsService _settingsService;
    
    public SnapshotService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    private string GetSnapshotsPath() => _settingsService.LoadSettings().SnapshotsPath;

    public List<DateTime> GetAvailableDates()
    {
        var snapshotsPath = GetSnapshotsPath();
        if (!Directory.Exists(snapshotsPath))
            return new List<DateTime>();

        var dates = new List<DateTime>();
        
        foreach (var directory in Directory.GetDirectories(snapshotsPath))
        {
            var dirName = Path.GetFileName(directory);
            if (DateTime.TryParseExact(dirName, "yyyy-MM-dd", 
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                dates.Add(date);
            }
        }
        
        return dates.OrderByDescending(d => d).ToList();
    }
    public List<SnapshotInfo> GetSnapshotsForDate(DateTime date)
    {
        var snapshotsPath = GetSnapshotsPath();
        var dateFolder = Path.Combine(snapshotsPath, date.ToString("yyyy-MM-dd"));
        
        if (!Directory.Exists(dateFolder))
            return new List<SnapshotInfo>();

        var snapshots = new List<SnapshotInfo>();
        var pngFiles = Directory.GetFiles(dateFolder, "*.png");
        
        foreach (var filePath in pngFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (TryParseTimestamp(fileName, out var timeOfDay))
            {
                snapshots.Add(new SnapshotInfo
                {
                    FilePath = filePath,
                    TimeOfDay = timeOfDay,
                    Date = date,
                    Index = 0 // Will be set after sorting
                });
            }
        }
        
        // Sort by time and assign indices
        snapshots = snapshots.OrderBy(s => s.TimeOfDay).ToList();
        for (int i = 0; i < snapshots.Count; i++)
        {
            snapshots[i] = snapshots[i] with { Index = i };
        }
        
        return snapshots;
    }

    private bool TryParseTimestamp(string fileName, out TimeSpan timeOfDay)
    {
        // Expected format: HH.MM.SS.MS (e.g., "19.07.00.70")
        var parts = fileName.Split('.');
        
        if (parts.Length == 4 &&
            int.TryParse(parts[0], out var hours) &&
            int.TryParse(parts[1], out var minutes) &&
            int.TryParse(parts[2], out var seconds) &&
            int.TryParse(parts[3], out var milliseconds))
        {
            try
            {
                timeOfDay = new TimeSpan(0, hours, minutes, seconds, milliseconds * 10);
                return true;
            }
            catch
            {
                timeOfDay = TimeSpan.Zero;
                return false;
            }
        }
        
        timeOfDay = TimeSpan.Zero;
        return false;
    }

    public SnapshotInfo? FindSnapshotBeforeTime(List<SnapshotInfo> snapshots, TimeSpan targetTime)
    {
        return snapshots.LastOrDefault(s => s.TimeOfDay < targetTime);
    }

    public SnapshotInfo? FindSnapshotAfterTime(List<SnapshotInfo> snapshots, TimeSpan targetTime)
    {
        return snapshots.FirstOrDefault(s => s.TimeOfDay > targetTime);
    }
}
