using System.Globalization;
using System.IO;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Daemon;

public sealed class DataCleanupService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly LoggingService _loggingService;
    private readonly object _sync = new();

    private CancellationTokenSource? _cts;
    private Task? _cleanupTask;

    public DataCleanupService(SettingsService settingsService, LoggingService loggingService)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_cleanupTask != null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _cleanupTask = Task.Run(() => CleanupLoopAsync(_cts.Token));
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? task;

        lock (_sync)
        {
            cts = _cts;
            task = _cleanupTask;
            _cts = null;
            _cleanupTask = null;
        }

        if (cts != null)
        {
            cts.Cancel();
        }

        if (task != null)
        {
            try
            {
                task.Wait();
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is TaskCanceledException or OperationCanceledException))
            {
                // Expected on shutdown.
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown.
            }
        }

        cts?.Dispose();
    }

    private async Task CleanupLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                RunCleanup();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Data cleanup failed.", ex);
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private void RunCleanup()
    {
        var settings = _settingsService.LoadSettings();
        CleanupScreenshots(settings.ScreenshotsPath, settings.ScreenshotRetentionDays);
        if (!string.IsNullOrWhiteSpace(settings.ScreenshotsArchivePath) && Directory.Exists(settings.ScreenshotsArchivePath))
            CleanupScreenshots(settings.ScreenshotsArchivePath, settings.ScreenshotRetentionDays);
        ArchiveScreenshots(settings.ScreenshotsPath, settings.ScreenshotsArchivePath, settings.ScreenshotsArchiveAfterDays);
        CleanupActivityLogs(settings.DataDirectory, settings.LogRetentionDays);
        CleanupLogFiles(_loggingService.LogDirectory, settings.LogRetentionDays);
    }

    private void CleanupScreenshots(string screenshotsPath, int retentionDays)
    {
        if (!Directory.Exists(screenshotsPath))
        {
            return;
        }

        DateTime cutoffDate = DateTime.Today.AddDays(-Math.Max(1, retentionDays));
        foreach (string directoryPath in Directory.GetDirectories(screenshotsPath))
        {
            string folderName = Path.GetFileName(directoryPath);
            if (!DateTime.TryParseExact(folderName, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime folderDate))
            {
                continue;
            }

            if (folderDate < cutoffDate)
            {
                TryDeleteDirectory(directoryPath);
            }
        }
    }

    private void CleanupActivityLogs(string dataDirectory, int retentionDays)
    {
        if (!Directory.Exists(dataDirectory))
        {
            return;
        }

        DateTime cutoffDate = DateTime.Today.AddDays(-Math.Max(1, retentionDays));
        foreach (string filePath in Directory.GetFiles(dataDirectory, "stats-*.csv"))
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string datePart = fileName.StartsWith("stats-", StringComparison.OrdinalIgnoreCase)
                ? fileName["stats-".Length..]
                : string.Empty;

            if (!DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fileDate))
            {
                continue;
            }

            if (fileDate < cutoffDate)
            {
                TryDeleteFile(filePath);
            }
        }
    }

    private void CleanupLogFiles(string logDirectory, int retentionDays)
    {
        if (!Directory.Exists(logDirectory))
        {
            return;
        }

        DateTime cutoffDate = DateTime.Today.AddDays(-Math.Max(1, retentionDays));
        foreach (string filePath in Directory.GetFiles(logDirectory, "windowwatcher-*.log"))
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string datePart = fileName.StartsWith("windowwatcher-", StringComparison.OrdinalIgnoreCase)
                ? fileName["windowwatcher-".Length..]
                : string.Empty;

            if (!DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fileDate))
            {
                continue;
            }

            if (fileDate < cutoffDate)
            {
                TryDeleteFile(filePath);
            }
        }
    }

    private void ArchiveScreenshots(string screenshotsPath, string archivePath, int archiveAfterDays)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            return;

        string normalizedScreenshots = Path.GetFullPath(screenshotsPath);
        string normalizedArchive = Path.GetFullPath(archivePath);
        if (string.Equals(normalizedScreenshots, normalizedArchive, StringComparison.OrdinalIgnoreCase))
            return;

        if (!Directory.Exists(screenshotsPath))
            return;

        if (!Directory.Exists(archivePath))
        {
            // If the drive of the archive path doesn't exist we will skip archiving for now.
            string? root = Path.GetPathRoot(archivePath);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return;

            try
            {
                Directory.CreateDirectory(archivePath);
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Failed to create archive directory '{archivePath}'.", ex);
                return;
            }
        }

        DateTime cutoffDate = DateTime.Today.AddDays(-Math.Max(1, archiveAfterDays));
        foreach (string directoryPath in Directory.GetDirectories(screenshotsPath))
        {
            string folderName = Path.GetFileName(directoryPath);
            if (!DateTime.TryParseExact(folderName, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime folderDate))
                continue;

            if (folderDate <= cutoffDate)
                TryMoveDirectory(directoryPath, Path.Combine(archivePath, folderName));
        }
    }

    private void TryMoveDirectory(string source, string destination)
    {
        try
        {
            if (!Directory.Exists(destination))
            {
                try
                {
                    Directory.Move(source, destination);
                    return;
                }
                catch (IOException) when (!string.Equals(
                    Path.GetPathRoot(source), Path.GetPathRoot(destination),
                    StringComparison.OrdinalIgnoreCase))
                {
                    // Cross-drive move — fall through to copy-then-delete.
                }
            }

            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file);
                string destFile = Path.Combine(destination, relative);
                string? destDir = Path.GetDirectoryName(destFile);
                if (destDir != null)
                    Directory.CreateDirectory(destDir);
                if (!File.Exists(destFile))
                    File.Copy(file, destFile);
            }
            Directory.Delete(source, recursive: true);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to move directory '{source}' to '{destination}'.", ex);
        }
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to delete directory '{path}'.", ex);
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to delete file '{path}'.", ex);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
