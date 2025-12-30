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
    private readonly LoggingService _loggingService;
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
        GraphicsCaptureService graphicsCaptureService,
        LoggingService loggingService)
    {
        _settingsService = settingsService;
        _userActivityService = userActivityService;
        _windowWatcherService = windowWatcherService;
        _graphicsCaptureService = graphicsCaptureService;
        _loggingService = loggingService;
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

    public async Task<bool> CaptureSingleScreenshotAsync(string? captureTargetOverride = null, CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.LoadSettings();
        if (!string.IsNullOrWhiteSpace(captureTargetOverride))
        {
            settings = new AppSettings
            {
                SnapshotsPath = settings.SnapshotsPath,
                DataDirectory = settings.DataDirectory,
                MinimizeOnStartup = settings.MinimizeOnStartup,
                IdleTimeoutSeconds = settings.IdleTimeoutSeconds,
                LogRetentionDays = settings.LogRetentionDays,
                ScreenshotIntervalSeconds = settings.ScreenshotIntervalSeconds,
                ScreenshotCaptureTarget = captureTargetOverride,
                ScreenshotRetentionDays = settings.ScreenshotRetentionDays
            };
        }

        return await SaveScreenshotAsync(settings, cancellationToken);
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

                bool saved = await SaveScreenshotAsync(settings, cancellationToken);
                if (saved)
                {
                    lock (_sync)
                    {
                        _lastSavedUtc = DateTime.UtcNow;
                        _windowChangedSinceLastCapture = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Screenshot capture failed.", ex);
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

    private async Task<bool> SaveScreenshotAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        BitmapSource? bitmap = await CaptureScreenshotAsync(settings.ScreenshotCaptureTarget, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        if (bitmap == null)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _loggingService.LogWarning($"Screenshot capture returned no image for target '{settings.ScreenshotCaptureTarget}'.");
            }

            return false;
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
        return true;
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
            _loggingService.LogWarning("Screenshot capture could not find any connected screens.");
            return null;
        }

        if (screens.Length == 1)
        {
            return await CaptureSingleScreenAsync(0, cancellationToken);
        }

        var union = screens[0].Bounds;
        for (int i = 1; i < screens.Length; i++)
        {
            union = System.Drawing.Rectangle.Union(union, screens[i].Bounds);
        }

        var captures = new List<(System.Drawing.Rectangle bounds, BitmapSource bitmap)>();
        foreach (var screen in screens)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            IntPtr monitorHandle = GetMonitorHandleForScreen(screen);
            BitmapSource? bitmap = await _graphicsCaptureService.CaptureMonitorAsync(monitorHandle, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            if (bitmap != null)
            {
                captures.Add((screen.Bounds, bitmap));
            }
            else
            {
                _loggingService.LogWarning(
                    $"Monitor capture failed for '{screen.DeviceName}' with bounds {screen.Bounds} and monitor handle 0x{monitorHandle.ToInt64():X}. Using a black placeholder.");
                captures.Add((screen.Bounds, CreateBlackBitmap(screen.Bounds.Width, screen.Bounds.Height)));
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return null;
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

    private static BitmapSource CreateBlackBitmap(int width, int height)
    {
        int stride = checked(width * 4);
        byte[] pixels = new byte[checked(stride * height)];
        for (int alphaOffset = 3; alphaOffset < pixels.Length; alphaOffset += 4)
        {
            pixels[alphaOffset] = byte.MaxValue;
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private async Task<BitmapSource?> CaptureSingleScreenAsync(int screenIndex, CancellationToken cancellationToken)
    {
        var screens = Screen.AllScreens;
        if (screenIndex < 0 || screenIndex >= screens.Length)
        {
            _loggingService.LogWarning($"Screenshot capture requested invalid screen index {screenIndex}; {screens.Length} screen(s) are available.");
            return null;
        }

        var screen = screens[screenIndex];
        IntPtr monitorHandle = GetMonitorHandleForScreen(screen);
        BitmapSource? capture = await _graphicsCaptureService.CaptureMonitorAsync(monitorHandle, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (capture != null)
        {
            return capture;
        }

        _loggingService.LogWarning(
            $"Falling back to desktop crop for '{screen.DeviceName}' after monitor capture failure (handle 0x{monitorHandle.ToInt64():X}).");

        return await CaptureDesktopFallbackAsync(cancellationToken, screen.Bounds);
    }

    private async Task<BitmapSource?> CaptureActiveApplicationAsync(CancellationToken cancellationToken)
    {
        WindowChangedEventArgs? windowChanged;
        lock (_sync)
        {
            windowChanged = _lastWindowChangedEvent;
        }

        IntPtr windowHandle = windowChanged?.WindowHandle ?? IntPtr.Zero;
        if (windowHandle == IntPtr.Zero)
        {
            windowHandle = _windowWatcherService.GetForegroundWindow();
        }

        if (windowHandle == IntPtr.Zero)
        {
            _loggingService.LogWarning("Screenshot capture requested the active application, but no active window is available.");
            return null;
        }

        return await _graphicsCaptureService.CaptureWindowAsync(windowHandle, cancellationToken);
    }

    private static IntPtr GetMonitorHandleForScreen(Screen screen)
    {
        int centerX = screen.Bounds.Left + (screen.Bounds.Width / 2);
        int centerY = screen.Bounds.Top + (screen.Bounds.Height / 2);
        return MonitorFromPoint(new POINT { X = centerX, Y = centerY }, MONITOR_DEFAULTTONEAREST);
    }

    private async Task<BitmapSource?> CaptureDesktopFallbackAsync(CancellationToken cancellationToken, System.Drawing.Rectangle? cropBounds = null)
    {
        BitmapSource? desktopCapture = await _graphicsCaptureService.CaptureDesktopAsync(cancellationToken);
        if (desktopCapture == null)
        {
            _loggingService.LogWarning("Desktop fallback capture did not return an image.");
            return null;
        }

        if (cropBounds == null)
        {
            return desktopCapture;
        }

        var virtualScreen = SystemInformation.VirtualScreen;
        int x = cropBounds.Value.Left - virtualScreen.Left;
        int y = cropBounds.Value.Top - virtualScreen.Top;
        int width = cropBounds.Value.Width;
        int height = cropBounds.Value.Height;

        if (x < 0 || y < 0 || width <= 0 || height <= 0
            || x + width > desktopCapture.PixelWidth
            || y + height > desktopCapture.PixelHeight)
        {
            _loggingService.LogWarning(
                $"Desktop fallback crop is out of range. VirtualScreen={virtualScreen}, Crop={cropBounds.Value}, ImageSize={desktopCapture.PixelWidth}x{desktopCapture.PixelHeight}.");
            return null;
        }

        var cropped = new CroppedBitmap(desktopCapture, new Int32Rect(x, y, width, height));
        cropped.Freeze();
        return cropped;
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
