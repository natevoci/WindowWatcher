using System.Windows;
using Velopack;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Daemon;

public partial class App : Application
{
    private const string GithubOwner = "natevoci";
    private const string GithubRepo = "WindowWatcher";

    private WindowWatcherService? _windowWatcherService;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Initialize Velopack
        VelopackApp.Build().Run();

        base.OnStartup(e);

        // Check for updates
        CheckForUpdates();

        // Initialize services
        var settingsService = new SettingsService();
        var csvDataService = new CsvDataService();
        var migrationUtility = new CsvMigrationUtility(settingsService);

        // Perform CSV migration if needed
        try
        {
            migrationUtility.MigrateCsvFilesIfNeeded();
        }
        catch (Exception ex)
        {
            var tempWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Width = 0,
                Height = 0,
                Left = -10000,
                Top = -10000,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent
            };
            tempWindow.Show();
            MessageBox.Show(tempWindow, $"Error during CSV file migration: {ex.Message}", "Migration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            tempWindow.Close();
        }

        // Create main window
        _mainWindow = new MainWindow();

        // Start window watcher service
        _windowWatcherService = new WindowWatcherService(
            csvDataService,
            settingsService,
            evt => _mainWindow.ViewEventsViewModel.AddActivity(evt));
        _windowWatcherService.Start();

        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _windowWatcherService?.Stop();
        base.OnExit(e);
    }

    private async void CheckForUpdates()
    {
        try
        {
            var updateSource = new Velopack.Sources.GithubSource($"https://github.com/{GithubOwner}/{GithubRepo}", null, false);
            var updateManager = new UpdateManager(updateSource);

            var updateInfo = await updateManager.CheckForUpdatesAsync();
            if (updateInfo == null)
                return;

            // Create a temporary window to make MessageBox topmost
            var tempWindow = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Topmost = true,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent
            };
            tempWindow.Show();

            var result = MessageBox.Show(
                tempWindow,
                $"A new version {updateInfo.TargetFullRelease.Version} is available. Would you like to update now?",
                "Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            tempWindow.Close();

            if (result == MessageBoxResult.Yes)
            {
                await updateManager.DownloadUpdatesAsync(updateInfo);
                updateManager.ApplyUpdatesAndRestart(updateInfo);
            }
        }
        catch
        {
            // Silently ignore update failures
        }
    }
}
