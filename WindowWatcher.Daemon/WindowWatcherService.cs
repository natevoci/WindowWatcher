using System.Diagnostics;
using WindowWatcher.Common.Models;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Daemon;

public class WindowWatcherService : IDisposable
{
    private readonly CsvDataService _csvDataService;
    private readonly SettingsService _settingsService;
    private readonly ViewEventsViewModel _viewEventsViewModel;

    private UserActivityHook? _activityHook;
    private DateTime _lastActivity = DateTime.MinValue;
    private readonly ManualResetEvent _exitEvent = new(false);
    private readonly Dictionary<int, string> _processNamesCache = new();
    private Thread? _wwThread;

    public WindowWatcherService(
        CsvDataService csvDataService,
        SettingsService settingsService,
        ViewEventsViewModel viewEventsViewModel)
    {
        _csvDataService = csvDataService;
        _settingsService = settingsService;
        _viewEventsViewModel = viewEventsViewModel;
    }

    public void Start()
    {
        _activityHook = new UserActivityHook(true, true);
        _activityHook.KeyPress += ActivityHook_KeyPress;
        _activityHook.OnMouseActivity += ActivityHook_OnMouseActivity;

        _wwThread = new Thread(WindowWatcherProc)
        {
            Name = "Window Watcher Thread",
            IsBackground = true
        };
        _wwThread.Start();
    }

    public void Stop()
    {
        _activityHook?.Stop();
        _exitEvent.Set();
        _wwThread?.Join();
    }

    public void Dispose()
    {
        Stop();
        _exitEvent.Dispose();
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
        int lastAppPid = -1;
        string lastAppName = string.Empty;
        string lastAppTitle = string.Empty;
        DateTime lastAppActivated = DateTime.MinValue;
        _lastActivity = DateTime.UtcNow;
        bool inactiveWritten = false;

        var settings = _settingsService.LoadSettings();

        while (!_exitEvent.WaitOne(100))
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
                    _viewEventsViewModel.AddActivity(evt);

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
                    _viewEventsViewModel.AddActivity(evt);

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
