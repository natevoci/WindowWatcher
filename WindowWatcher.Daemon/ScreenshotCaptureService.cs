using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowWatcher.Common.Models;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Daemon;

public sealed class ScreenshotCaptureService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly UserActivityService _userActivityService;
    private readonly WindowWatcherService _windowWatcherService;
    private readonly object _sync = new();

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _captureTask;
    private bool _isCapturing = true;
    private bool _windowChangedSinceLastCapture;
    private DateTime _lastSavedUtc = DateTime.MinValue;
    private DateTime _lastRetentionSweepUtc = DateTime.MinValue;

    public ScreenshotCaptureService(
        SettingsService settingsService,
        UserActivityService userActivityService,
        WindowWatcherService windowWatcherService)
    {
        _settingsService = settingsService;
        _userActivityService = userActivityService;
        _windowWatcherService = windowWatcherService;

        _windowWatcherService.WindowChanged += WindowWatcherServiceOnWindowChanged;
    }

    public bool IsCapturing
    {
        get
        {
            lock (_sync)
            {
                return _isCapturing;
            }
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_captureTask != null)
            {
                return;
            }

            _isCapturing = true;
            _windowChangedSinceLastCapture = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _captureTask = Task.Run(() => CaptureLoopAsync(_cancellationTokenSource.Token));
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? captureTask;

        lock (_sync)
        {
            cts = _cancellationTokenSource;
            captureTask = _captureTask;
            _cancellationTokenSource = null;
            _captureTask = null;
        }

        if (cts != null)
        {
            cts.Cancel();
        }

        if (captureTask != null)
        {
            try
            {
                captureTask.Wait();
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is TaskCanceledException or OperationCanceledException))
            {
                // Ignore cancellation exceptions.
            }
            catch (TaskCanceledException)
            {
                // Ignore cancellation exceptions.
            }
        }

        cts?.Dispose();
    }

    public bool ToggleCapturing()
    {
        lock (_sync)
        {
            _isCapturing = !_isCapturing;
            return _isCapturing;
        }
    }

    private async Task CaptureLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            AppSettings settings = _settingsService.LoadSettings();
            int intervalSeconds = Math.Max(1, settings.ScreenshotIntervalSeconds);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            bool isCapturing;
            lock (_sync)
            {
                isCapturing = _isCapturing;
            }

            if (!isCapturing)
            {
                continue;
            }

            try
            {
                if (!ShouldCapture())
                {
                    continue;
                }

                SaveScreenshot(settings);
                lock (_sync)
                {
                    _lastSavedUtc = DateTime.UtcNow;
                    _windowChangedSinceLastCapture = false;
                }

                CleanupExpiredSnapshots(settings);
            }
            catch
            {
                // Keep running if capture fails.
            }
        }
    }

    private bool ShouldCapture()
    {
        DateTime lastSavedUtc;
        bool windowChanged;

        lock (_sync)
        {
            lastSavedUtc = _lastSavedUtc;
            windowChanged = _windowChangedSinceLastCapture;
        }

        if (lastSavedUtc == DateTime.MinValue)
        {
            return true;
        }

        bool activitySinceLastCapture = _userActivityService.HasActivitySince(lastSavedUtc);
        return activitySinceLastCapture || windowChanged;
    }

    private void SaveScreenshot(AppSettings settings)
    {
        using Bitmap? bitmap = CaptureScreenshot(settings.ScreenshotCaptureTarget);
        if (bitmap == null)
        {
            return;
        }

        DateTime now = DateTime.Now;
        string dayFolder = Path.Combine(settings.SnapshotsPath, now.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dayFolder);

        // Keep a 4-digit fractional second suffix compatible with viewer parsing.
        string filename = $"{now:HH.mm.ss.ffff}.png";
        string outputPath = Path.Combine(dayFolder, filename);

        bitmap.Save(outputPath, ImageFormat.Png);
    }

    private Bitmap? CaptureScreenshot(string captureTarget)
    {
        if (string.Equals(captureTarget, "ActiveApplication", StringComparison.OrdinalIgnoreCase))
        {
            return CaptureActiveApplication();
        }

        if (captureTarget.StartsWith("Screen:", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(captureTarget["Screen:".Length..], out int screenIndex))
        {
            var screens = Screen.AllScreens;
            if (screenIndex >= 0 && screenIndex < screens.Length)
            {
                return CaptureBounds(screens[screenIndex].Bounds);
            }

            return null;
        }

        return CaptureAllScreens();
    }

    private static Bitmap? CaptureAllScreens()
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
        {
            return null;
        }

        Rectangle bounds = screens[0].Bounds;
        for (int i = 1; i < screens.Length; i++)
        {
            bounds = Rectangle.Union(bounds, screens[i].Bounds);
        }

        return CaptureBounds(bounds);
    }

    private static Bitmap? CaptureActiveApplication()
    {
        IntPtr handle = WindowsApiService.GetForegroundWindowHandle();
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        if (!GetWindowRect(handle, out RECT rect))
        {
            return null;
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        return CaptureBounds(new Rectangle(rect.Left, rect.Top, width, height));
    }

    private static Bitmap? CaptureBounds(Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    private void CleanupExpiredSnapshots(AppSettings settings)
    {
        DateTime nowUtc = DateTime.UtcNow;
        if ((nowUtc - _lastRetentionSweepUtc) < TimeSpan.FromHours(1))
        {
            return;
        }

        _lastRetentionSweepUtc = nowUtc;

        int retentionDays = Math.Max(1, settings.ScreenshotRetentionDays);
        string snapshotsPath = settings.SnapshotsPath;
        if (!Directory.Exists(snapshotsPath))
        {
            return;
        }

        DateTime cutoffDate = DateTime.Today.AddDays(-retentionDays);
        foreach (string directoryPath in Directory.GetDirectories(snapshotsPath))
        {
            string folderName = Path.GetFileName(directoryPath);
            if (!DateTime.TryParseExact(
                    folderName,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime folderDate))
            {
                continue;
            }

            if (folderDate < cutoffDate)
            {
                try
                {
                    Directory.Delete(directoryPath, true);
                }
                catch
                {
                    // Keep retention cleanup best-effort.
                }
            }
        }
    }

    private void WindowWatcherServiceOnWindowChanged(object? sender, DateTime e)
    {
        lock (_sync)
        {
            _windowChangedSinceLastCapture = true;
        }
    }

    public void Dispose()
    {
        _windowWatcherService.WindowChanged -= WindowWatcherServiceOnWindowChanged;
        Stop();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
