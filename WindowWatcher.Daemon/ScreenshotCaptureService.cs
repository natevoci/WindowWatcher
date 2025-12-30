using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowWatcher.Common.Models;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Daemon;

public sealed class ScreenshotCaptureService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly UserActivityService _userActivityService;
    private readonly WindowWatcherService _windowWatcherService;
    private readonly GraphicsCaptureService _graphicsCaptureService;
    private readonly object _sync = new();

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _captureTask;
    private bool _isCapturing = true;
    private bool _windowChangedSinceLastCapture;
    private WindowChangedEventArgs? _lastWindowChangedEvent;
    private DateTime _lastSavedUtc = DateTime.MinValue;

    public ScreenshotCaptureService(
        SettingsService settingsService,
        UserActivityService userActivityService,
        WindowWatcherService windowWatcherService,
        GraphicsCaptureService graphicsCaptureService)
    {
        _settingsService = settingsService;
        _userActivityService = userActivityService;
        _windowWatcherService = windowWatcherService;
        _graphicsCaptureService = graphicsCaptureService;
        _lastWindowChangedEvent = _windowWatcherService.LastWindowChanged;

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

                await SaveScreenshotAsync(settings, cancellationToken);
                lock (_sync)
                {
                    _lastSavedUtc = DateTime.UtcNow;
                    _windowChangedSinceLastCapture = false;
                }
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

    private async Task SaveScreenshotAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        BitmapSource? bitmap = await CaptureScreenshotAsync(settings.ScreenshotCaptureTarget, cancellationToken);
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

        using var fileStream = File.Create(outputPath);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(fileStream);
    }

    private async Task<BitmapSource?> CaptureScreenshotAsync(string captureTarget, CancellationToken cancellationToken)
    {
        if (string.Equals(captureTarget, "ActiveApplication", StringComparison.OrdinalIgnoreCase))
        {
            return await CaptureActiveApplicationAsync(cancellationToken);
        }

        if (captureTarget.StartsWith("Screen:", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(captureTarget["Screen:".Length..], out int screenIndex))
        {
            return await CaptureSingleScreenAsync(screenIndex, cancellationToken);
        }

        return await CaptureAllScreensAsync(cancellationToken);
    }

    private async Task<BitmapSource?> CaptureAllScreensAsync(CancellationToken cancellationToken)
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
        {
            return null;
        }

        if (screens.Length == 1)
        {
            return await CaptureSingleScreenAsync(0, cancellationToken);
        }

        var captures = new List<(System.Drawing.Rectangle bounds, BitmapSource bitmap)>();
        foreach (var screen in screens)
        {
            IntPtr monitorHandle = GetMonitorHandleForScreen(screen);
            BitmapSource? bitmap = await _graphicsCaptureService.CaptureMonitorAsync(monitorHandle, cancellationToken);
            if (bitmap != null)
            {
                captures.Add((screen.Bounds, bitmap));
            }
        }

        if (captures.Count == 0)
        {
            return null;
        }

        var union = captures[0].bounds;
        for (int i = 1; i < captures.Count; i++)
        {
            union = System.Drawing.Rectangle.Union(union, captures[i].bounds);
        }

        var drawingVisual = new DrawingVisual();
        using (DrawingContext drawingContext = drawingVisual.RenderOpen())
        {
            foreach (var capture in captures)
            {
                double x = capture.bounds.Left - union.Left;
                double y = capture.bounds.Top - union.Top;
                drawingContext.DrawImage(capture.bitmap, new Rect(x, y, capture.bounds.Width, capture.bounds.Height));
            }
        }

        var output = new RenderTargetBitmap(union.Width, union.Height, 96, 96, PixelFormats.Pbgra32);
        output.Render(drawingVisual);
        output.Freeze();
        return output;
    }

    private async Task<BitmapSource?> CaptureSingleScreenAsync(int screenIndex, CancellationToken cancellationToken)
    {
        var screens = Screen.AllScreens;
        if (screenIndex < 0 || screenIndex >= screens.Length)
        {
            return null;
        }

        IntPtr monitorHandle = GetMonitorHandleForScreen(screens[screenIndex]);
        return await _graphicsCaptureService.CaptureMonitorAsync(monitorHandle, cancellationToken);
    }

    private async Task<BitmapSource?> CaptureActiveApplicationAsync(CancellationToken cancellationToken)
    {
        WindowChangedEventArgs? windowChanged;
        lock (_sync)
        {
            windowChanged = _lastWindowChangedEvent;
        }

        if (windowChanged == null || windowChanged.WindowHandle == IntPtr.Zero)
        {
            return null;
        }

        return await _graphicsCaptureService.CaptureWindowAsync(windowChanged.WindowHandle, cancellationToken);
    }

    private static IntPtr GetMonitorHandleForScreen(Screen screen)
    {
        int centerX = screen.Bounds.Left + (screen.Bounds.Width / 2);
        int centerY = screen.Bounds.Top + (screen.Bounds.Height / 2);
        return MonitorFromPoint(new POINT { X = centerX, Y = centerY }, MONITOR_DEFAULTTONEAREST);
    }

    private void WindowWatcherServiceOnWindowChanged(object? sender, WindowChangedEventArgs e)
    {
        lock (_sync)
        {
            _lastWindowChangedEvent = e;
            _windowChangedSinceLastCapture = true;
        }
    }

    public void Dispose()
    {
        _windowWatcherService.WindowChanged -= WindowWatcherServiceOnWindowChanged;
        Stop();
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT point, uint dwFlags);

    private struct POINT
    {
        public int X;
        public int Y;
    }
}
