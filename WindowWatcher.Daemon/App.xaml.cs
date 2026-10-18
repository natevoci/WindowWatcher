using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Velopack;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Daemon;

public partial class App : Application
{
    private const string GithubOwner = "natevoci";
    private const string GithubRepo = "WindowWatcher";
    private const string CaptureRestartArgument = "--capture-restart-utc=";
    private const string WaitForProcessArgument = "--wait-for-process=";

    private ServiceProvider? _serviceProvider;
    private DateTime _lastCaptureRestartUtc = DateTime.MinValue;
    private bool _captureRestartPending;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!WaitForPreviousProcess(e.Args))
        {
            Shutdown(1);
            return;
        }

        string? restartArgument = e.Args.FirstOrDefault(arg => arg.StartsWith(CaptureRestartArgument, StringComparison.Ordinal));
        if (restartArgument != null
            && long.TryParse(restartArgument[CaptureRestartArgument.Length..], out long restartTicks)
            && restartTicks >= DateTime.MinValue.Ticks && restartTicks <= DateTime.MaxValue.Ticks)
        {
            _lastCaptureRestartUtc = new DateTime(restartTicks, DateTimeKind.Utc);
        }

        // Initialize Velopack
        VelopackApp.Build().Run();

        base.OnStartup(e);

        // Build DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        var loggingService = _serviceProvider.GetRequiredService<LoggingService>();
        loggingService.LogInformation("Daemon starting.");

        // Check for updates only for installed production builds.
        if (ShouldCheckForUpdates())
        {
            CheckForUpdates();
        }
        else
        {
            loggingService.LogInformation("Skipping update check for non-installed or dotnet-hosted execution.");
        }

        // Perform CSV migration if needed
        var migrationUtility = _serviceProvider.GetRequiredService<CsvMigrationUtility>();
        try
        {
            migrationUtility.MigrateCsvFilesIfNeeded();
        }
        catch (Exception ex)
        {
            loggingService.LogError("CSV file migration failed.", ex);
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

        bool captureSingleScreenshot = e.Args.Any(arg =>
            string.Equals(arg, "CaptureSingleScreenshot", StringComparison.OrdinalIgnoreCase));
        if (captureSingleScreenshot)
        {
            string? captureTargetOverride = e.Args
                .FirstOrDefault(arg =>
                    !string.Equals(arg, "CaptureSingleScreenshot", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(arg));

            loggingService.LogInformation("CaptureSingleScreenshot argument detected. Capturing one screenshot and exiting.");
            if (!string.IsNullOrWhiteSpace(captureTargetOverride))
            {
                loggingService.LogInformation($"Using one-shot capture target override: {captureTargetOverride}");
            }

            var singleCaptureService = _serviceProvider.GetRequiredService<ScreenshotCaptureService>();

            // The capture pipeline relies on WPF imaging (RenderTargetBitmap) which needs the STA
            // UI thread with a running dispatcher. Blocking OnStartup with GetResult() would deadlock
            // because the message pump has not started yet, so schedule the work on the dispatcher and
            // return, then shut down once the asynchronous capture finishes.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = Dispatcher.InvokeAsync(async () =>
            {
                bool captured;
                try
                {
                    captured = await singleCaptureService.CaptureSingleScreenshotAsync(captureTargetOverride);
                }
                catch (Exception ex)
                {
                    loggingService.LogError("Single screenshot capture failed.", ex);
                    captured = false;
                }

                if (captured)
                {
                    loggingService.LogInformation("Single screenshot capture completed.");
                }
                else
                {
                    loggingService.LogWarning("Single screenshot capture did not produce an image.");
                }

                Shutdown(captured ? 0 : 1);
            });

            return;
        }

        // Start window watcher service
        var windowWatcherService = _serviceProvider.GetRequiredService<WindowWatcherService>();
        windowWatcherService.Start();

        // Start screenshot capture service
        var screenshotCaptureService = _serviceProvider.GetRequiredService<ScreenshotCaptureService>();
        screenshotCaptureService.RestartRequested += OnCaptureRestartRequested;
        screenshotCaptureService.Start();

        // Start cleanup service
        var dataCleanupService = _serviceProvider.GetRequiredService<DataCleanupService>();
        dataCleanupService.Start();

        // Show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static bool WaitForPreviousProcess(string[] arguments)
    {
        string? waitArgument = arguments.FirstOrDefault(arg => arg.StartsWith(WaitForProcessArgument, StringComparison.Ordinal));
        if (waitArgument == null || !int.TryParse(waitArgument[WaitForProcessArgument.Length..], out int processId))
        {
            return true;
        }

        try
        {
            using var previousProcess = Process.GetProcessById(processId);
            return previousProcess.WaitForExit(30_000);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private void OnCaptureRestartRequested()
    {
        _ = Dispatcher.InvokeAsync(RestartAfterCaptureFailure);
    }

    private void RestartAfterCaptureFailure()
    {
        var loggingService = _serviceProvider?.GetService<LoggingService>();
        if (_captureRestartPending || Dispatcher.HasShutdownStarted)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        if (now - _lastCaptureRestartUtc < TimeSpan.FromMinutes(15))
        {
            loggingService?.LogWarning("Automatic screenshot recovery restart suppressed by the 15-minute cooldown; in-process recovery will continue.");
            return;
        }

        _lastCaptureRestartUtc = now;
        try
        {
            string processPath = Environment.ProcessPath
                ?? throw new InvalidOperationException("The daemon executable path is unavailable.");
            var startInfo = new ProcessStartInfo(processPath)
            {
                UseShellExecute = false,
                WorkingDirectory = Environment.CurrentDirectory
            };

            if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.ArgumentList.Add(typeof(App).Assembly.Location);
            }

            foreach (string argument in Environment.GetCommandLineArgs().Skip(1))
            {
                if (!argument.StartsWith(CaptureRestartArgument, StringComparison.Ordinal)
                    && !argument.StartsWith(WaitForProcessArgument, StringComparison.Ordinal))
                {
                    startInfo.ArgumentList.Add(argument);
                }
            }

            startInfo.ArgumentList.Add($"{CaptureRestartArgument}{now.Ticks}");
            startInfo.ArgumentList.Add($"{WaitForProcessArgument}{Environment.ProcessId}");
            using var replacement = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The replacement daemon process could not be started.");
            _captureRestartPending = true;
            loggingService?.LogWarning("Restarting the daemon after repeated screenshot recovery failures; the replacement will wait for this process to exit.");
        }
        catch (Exception ex)
        {
            loggingService?.LogError("Automatic daemon restart failed; the current daemon will keep running and attempting device recovery.", ex);
            return;
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.GetService<LoggingService>()?.LogInformation("Daemon shutting down.");
        _serviceProvider?.GetService<DataCleanupService>()?.Stop();
        _serviceProvider?.GetService<ScreenshotCaptureService>()?.Stop();
        _serviceProvider?.GetService<WindowWatcherService>()?.Stop();
        _serviceProvider?.GetService<UserActivityService>()?.Stop();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        services.AddSingleton<LoggingService>();
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
        catch (Velopack.Exceptions.NotInstalledException ex)
        {
            _serviceProvider?.GetService<LoggingService>()?.LogInformation($"Skipping update check because the app is not installed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _serviceProvider?.GetService<LoggingService>()?.LogError("Update check failed.", ex);
        }
    }

    private static bool ShouldCheckForUpdates()
    {
        string baseDir = AppContext.BaseDirectory;
        string updateExePath = Path.Combine(baseDir, "Update.exe");
        if (!File.Exists(updateExePath))
        {
            return false;
        }

        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath)
            && string.Equals(Path.GetFileName(processPath), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string moduleFileName = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(moduleFileName)
            && string.Equals(Path.GetFileName(moduleFileName), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
