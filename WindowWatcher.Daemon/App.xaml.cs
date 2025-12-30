using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Velopack;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Daemon;

public partial class App : Application
{
    private const string GithubOwner = "natevoci";
    private const string GithubRepo = "WindowWatcher";

    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Initialize Velopack
        VelopackApp.Build().Run();

        base.OnStartup(e);

        // Check for updates
        CheckForUpdates();

        // Build DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Perform CSV migration if needed
        var migrationUtility = _serviceProvider.GetRequiredService<CsvMigrationUtility>();
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

        // Start window watcher service
        var windowWatcherService = _serviceProvider.GetRequiredService<WindowWatcherService>();
        windowWatcherService.Start();

        // Start screenshot capture service
        var screenshotCaptureService = _serviceProvider.GetRequiredService<ScreenshotCaptureService>();
        screenshotCaptureService.Start();

        // Start cleanup service
        var dataCleanupService = _serviceProvider.GetRequiredService<DataCleanupService>();
        dataCleanupService.Start();

        // Show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.GetService<DataCleanupService>()?.Stop();
        _serviceProvider?.GetService<ScreenshotCaptureService>()?.Stop();
        _serviceProvider?.GetService<WindowWatcherService>()?.Stop();
        _serviceProvider?.GetService<UserActivityService>()?.Stop();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        services.AddSingleton<SettingsService>();
        services.AddSingleton<CsvDataService>();
        services.AddSingleton<CsvMigrationUtility>();
        services.AddSingleton<ViewEventsViewModel>();
        services.AddSingleton<UserActivityService>();
        services.AddSingleton<WindowWatcherService>();
        services.AddSingleton<GraphicsCaptureService>();
        services.AddSingleton<ScreenshotCaptureService>();
        services.AddSingleton<DataCleanupService>();
        services.AddTransient<MainWindow>();
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
