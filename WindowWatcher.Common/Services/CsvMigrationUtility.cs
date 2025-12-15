using WindowWatcher.Common.Models;

namespace WindowWatcher.Common.Services;

public class CsvMigrationUtility
{
    private readonly SettingsService _settingsService;

    public CsvMigrationUtility(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void MigrateCsvFilesIfNeeded()
    {
        var settings = _settingsService.LoadSettings();
        var executableDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var targetDirectory = Path.GetFullPath(settings.DataDirectory);

        // Normalize paths for comparison
        var normalizedExecDir = Path.GetFullPath(executableDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var normalizedTargetDir = targetDirectory.TrimEnd(Path.DirectorySeparatorChar);

        // Find all CSV files in the executable directory
        var csvFiles = Directory.GetFiles(executableDirectory, "stats-*.csv");

        if (csvFiles.Length == 0)
            return; // No CSV files to migrate

        // If executable directory is the same as target directory, no migration needed
        if (string.Equals(normalizedExecDir, normalizedTargetDir, StringComparison.OrdinalIgnoreCase))
            return;

        // Ensure target directory exists
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        int movedCount = 0;
        int skippedCount = 0;

        foreach (var csvFile in csvFiles)
        {
            var fileName = Path.GetFileName(csvFile);
            var targetPath = Path.Combine(targetDirectory, fileName);

            try
            {
                if (File.Exists(targetPath))
                {
                    // Target file already exists, skip to avoid overwriting
                    skippedCount++;
                    continue;
                }

                // Move the file to the target directory
                File.Move(csvFile, targetPath);
                movedCount++;
            }
            catch (IOException)
            {
                // If move fails, skip this file
                skippedCount++;
            }
        }

        // Log migration results (could be extended to show to user if needed)
        if (movedCount > 0)
        {
            System.Diagnostics.Debug.WriteLine($"CSV Migration: Moved {movedCount} files from {executableDirectory} to {targetDirectory}");
        }

        if (skippedCount > 0)
        {
            System.Diagnostics.Debug.WriteLine($"CSV Migration: Skipped {skippedCount} files (already exist or error)");
        }
    }
}
