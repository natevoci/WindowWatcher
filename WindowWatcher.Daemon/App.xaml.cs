using System.Diagnostics;
using System.Windows;
using WindowWatcher.Common.Models;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Daemon;

public partial class App : Application
{
    private SettingsService? _settingsService;
    private CsvMigrationUtility? _migrationUtility;
    private UserActivityHook? _activityHook;
    private DateTime _lastActivity = DateTime.MinValue;
    private readonly ManualResetEvent _exitWindowWatchingThread = new(false);
    private readonly Dictionary<int, string> _processNamesCache = new();
    private CsvDataService? _csvDataService;
    private MainWindow? _mainWindow;
    private Thread? _wwThread;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize services
        _settingsService = new SettingsService();
        _csvDataService = new CsvDataService();
        _migrationUtility = new CsvMigrationUtility(_settingsService);

        // Perform CSV migration if needed
        try
        {
            _migrationUtility.MigrateCsvFilesIfNeeded();
        }
        catch (Exception ex)
        {
            // Show error with TopMost message box
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

        // Setup activity hook
        _activityHook = new UserActivityHook(true, true);
        _activityHook.KeyPress += ActivityHook_KeyPress;
        _activityHook.OnMouseActivity += ActivityHook_OnMouseActivity;

        // Start background monitoring thread
        _wwThread = new Thread(WindowWatcherProc)
        {
            Name = "Window Watcher Thread",
            IsBackground = true
        };
        _wwThread.Start();

        // Create main window
        _mainWindow = new MainWindow();
        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activityHook?.Stop();
        _exitWindowWatchingThread.Set();
        _wwThread?.Join();
        base.OnExit(e);
    }

    private void ActivityHook_KeyPress(object? sender, System.Windows.Forms.KeyPressEventArgs e)
    {
        _lastActivity = DateTime.UtcNow;
    }

    private void ActivityHook_OnMouseActivity(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        _lastActivity = DateTime.UtcNow;
    }

    private void WindowWatcherProc()
    {
        if (_csvDataService == null || _settingsService == null)
            return;

        int lastAppPid = -1;
        string lastAppName = string.Empty;
        string lastAppTitle = string.Empty;
        DateTime lastAppActivated = DateTime.MinValue;
        _lastActivity = DateTime.UtcNow;
        bool inactiveWritten = false;

        var settings = _settingsService.LoadSettings();

        while (!_exitWindowWatchingThread.WaitOne(100))
        {
            try
            {
                var elapsed = DateTime.UtcNow - _lastActivity;
                int idleTimeout = settings.IdleTimeoutSeconds;
                
                if (elapsed.TotalSeconds > idleTimeout && !inactiveWritten)
                {
                    var evt = new WindowActivityEvent
                    {
                        Date = DateTime.Now,
                        EventType = WindowActivityEventType.Inactive
                    };
                    _csvDataService.WriteEvent(evt, settings.DataDirectory);
                    _mainWindow?.ViewEventsViewModel.AddActivity(evt);

                    inactiveWritten = true;
                    _lastActivity = DateTime.UtcNow;
                }

                IntPtr hwnd = GetHwnd();
                int pid = GetPid(hwnd);
                string appTitle = GetAppTitle();
                string appName = GetProcessNameFromProcessId(pid);

                if (lastAppPid != pid || lastAppTitle != appTitle)
                {
                    lastAppActivated = DateTime.Now;
                    lastAppPid = pid;
                    lastAppName = appName;
                    lastAppTitle = appTitle;

                    var evt = new WindowActivityEvent
                    {
                        Date = lastAppActivated,
                        EventType = WindowActivityEventType.WindowActivated,
                        ProcessName = lastAppName,
                        WindowTitle = lastAppTitle
                    };
                    _csvDataService.WriteEvent(evt, settings.DataDirectory);
                    _mainWindow?.ViewEventsViewModel.AddActivity(evt);

                    inactiveWritten = false;
                    _lastActivity = DateTime.UtcNow;
                }
            }
            catch (Exception)
            {
                // Silently continue on errors
            }
        }

        var exitEvent = new WindowActivityEvent
        {
            Date = DateTime.Now,
            EventType = WindowActivityEventType.Exiting
        };
        _csvDataService.WriteEvent(exitEvent, settings.DataDirectory);
    }

    private string GetProcessNameFromProcessId(int pid)
    {
        if (_processNamesCache.TryGetValue(pid, out string? cachedName))
        {
            return cachedName;
        }

        try
        {
            var process = Process.GetProcessById(pid);
            string appName = process.ProcessName;
            _processNamesCache[pid] = appName;
            return appName;
        }
        catch
        {
            return "unknown";
        }
    }

    private static string GetAppTitle()
    {
        return WindowsApiService.GetActiveWindowTitle().Trim().Replace("\0", string.Empty);
    }

    private static int GetPid(IntPtr hwnd)
    {
        return WindowsApiService.GetWindowProcessId(hwnd);
    }

    private static IntPtr GetHwnd()
    {
        return WindowsApiService.GetForegroundWindowHandle();
    }
}
