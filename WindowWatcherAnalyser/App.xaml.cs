using System.Windows;
using WindowWatcher.Common.Services;

namespace WindowWatcherAnalyser;

public partial class App : Application
{
    private SettingsService? _settingsService;
    private CsvMigrationUtility? _migrationUtility;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize services
        _settingsService = new SettingsService();
        _migrationUtility = new CsvMigrationUtility(_settingsService);

        // Perform CSV migration if needed
        try
        {
            _migrationUtility.MigrateCsvFilesIfNeeded();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during CSV file migration: {ex.Message}", "Migration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
