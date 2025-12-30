using System.Diagnostics;
using WindowWatcher.Common.Models;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Daemon;

public class WindowWatcherService : IDisposable
{
    private readonly CsvDataService _csvDataService;
    private readonly SettingsService _settingsService;
    private readonly ViewEventsViewModel _viewEventsViewModel;
    private readonly UserActivityService _userActivityService;
    private readonly LoggingService _loggingService;

    private readonly ManualResetEvent _exitEvent = new(false);
    private readonly Dictionary<int, string> _processNamesCache = new();
    private readonly object _windowChangedSync = new();
    private Thread? _wwThread;
    private DateTime _lastWindowChangedUtc = DateTime.MinValue;
    private WindowChangedEventArgs? _lastWindowChanged;

    public WindowWatcherService(
        CsvDataService csvDataService,
        SettingsService settingsService,
        ViewEventsViewModel viewEventsViewModel,
        UserActivityService userActivityService,
        LoggingService loggingService)
    {
        _csvDataService = csvDataService;
        _settingsService = settingsService;
        _viewEventsViewModel = viewEventsViewModel;
        _userActivityService = userActivityService;
        _loggingService = loggingService;
    }

    public DateTime LastWindowChangedUtc
    {
        get
        {
            lock (_windowChangedSync)
            {
                return _lastWindowChangedUtc;
            }
        }
    }

    public WindowChangedEventArgs? LastWindowChanged
    {
        get
        {
            lock (_windowChangedSync)
            {
                return _lastWindowChanged;
            }
        }
    }

    public event EventHandler<WindowChangedEventArgs>? WindowChanged;

    public void Start()
    {
        _userActivityService.Start();

        _wwThread = new Thread(WindowWatcherProc)
        {
            Name = "Window Watcher Thread",
            IsBackground = true
        };
        _wwThread.Start();
    }

    public void Stop()
    {
        _exitEvent.Set();
        _wwThread?.Join();
    }

    public void Dispose()
    {
        Stop();
        _exitEvent.Dispose();
    }

    private void WindowWatcherProc()
    {
        int lastAppPid = -1;
        string lastAppName = string.Empty;
        string lastAppTitle = string.Empty;
        DateTime lastAppActivated = DateTime.MinValue;
        bool inactiveWritten = false;

        var settings = _settingsService.LoadSettings();

        while (!_exitEvent.WaitOne(100))
        {
            try
            {
                var elapsed = DateTime.UtcNow - _userActivityService.LastActivityUtc;
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

                    var changedEvent = new WindowChangedEventArgs
                    {
                        WindowHandle = hwnd,
                        ProcessId = pid,
                        ProcessName = appName,
                        WindowTitle = appTitle,
                        ChangedUtc = DateTime.UtcNow
                    };

                    lock (_windowChangedSync)
                    {
                        _lastWindowChangedUtc = changedEvent.ChangedUtc;
                        _lastWindowChanged = changedEvent;
                    }
                    WindowChanged?.Invoke(this, changedEvent);

                    inactiveWritten = false;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Window watcher iteration failed.", ex);
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
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to get process name for process ID {pid}.", ex);
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
