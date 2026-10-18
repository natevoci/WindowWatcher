using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;
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

    public event Action? RestartRequested;

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
                _loggingService.LogTrace("Screenshot capture service start was ignored because the capture loop is already running.");
                return;
            }

            _isCapturing = true;
            _windowChangedSinceLastCapture = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _captureTask = Task.Run(() => CaptureLoopAsync(_cancellationTokenSource.Token));
        }

        _loggingService.LogDebug("Screenshot capture service started.");
    }

    public void Stop()
    {
        var stopTimer = Stopwatch.StartNew();
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
            _loggingService.LogTrace("Screenshot capture service cancellation requested.");
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
        _loggingService.LogDebug($"Screenshot capture service stopped in {stopTimer.ElapsedMilliseconds} ms.");
    }

    public bool ToggleCapturing()
    {
        lock (_sync)
        {
            _isCapturing = !_isCapturing;
            _loggingService.LogDebug($"Screenshot capture was {(_isCapturing ? "enabled" : "disabled")}.");
            return _isCapturing;
        }
    }

    public async Task<bool> CaptureSingleScreenshotAsync(string? captureTargetOverride = null, CancellationToken cancellationToken = default)
    {
        var captureTimer = Stopwatch.StartNew();
        var settings = _settingsService.LoadSettings();
        if (!string.IsNullOrWhiteSpace(captureTargetOverride))
        {
            settings = settings.Clone();
            settings.ScreenshotCaptureTarget = captureTargetOverride;
        }

        _loggingService.LogDebug($"Single screenshot capture started for target '{settings.ScreenshotCaptureTarget}'.");
        bool saved = await SaveScreenshotAsync(settings, cancellationToken);
        _loggingService.LogDebug($"Single screenshot capture for target '{settings.ScreenshotCaptureTarget}' {(saved ? "completed" : "did not save an image")} in {captureTimer.ElapsedMilliseconds} ms.");
        return saved;
    }

    private async Task CaptureLoopAsync(CancellationToken cancellationToken)
    {
        int consecutiveRecoveryFailures = 0;
        _loggingService.LogTrace("Screenshot capture loop started.");
        while (!cancellationToken.IsCancellationRequested)
        {
            AppSettings settings = _settingsService.LoadSettings();
            int intervalSeconds = Math.Max(1, settings.ScreenshotIntervalSeconds);

            try
            {
                _loggingService.LogTrace($"Screenshot capture loop waiting {intervalSeconds} second(s).");
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
                _loggingService.LogTrace("Screenshot capture loop skipped a tick because capture is disabled.");
                continue;
            }

            try
            {
                if (!ShouldCapture())
                {
                    _loggingService.LogTrace("Screenshot capture loop skipped a tick because no activity or window change occurred.");
                    continue;
                }

                var captureTimer = Stopwatch.StartNew();
                bool saved = await SaveScreenshotAsync(settings, cancellationToken);
                if (saved)
                {
                    consecutiveRecoveryFailures = 0;
                    lock (_sync)
                    {
                        _lastSavedUtc = DateTime.UtcNow;
                        _windowChangedSinceLastCapture = false;
                    }

                    _loggingService.LogDebug($"Scheduled screenshot capture saved in {captureTimer.ElapsedMilliseconds} ms.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (GraphicsCaptureRecoveryException ex)
            {
                consecutiveRecoveryFailures++;
                _loggingService.LogError($"Screenshot device recovery failed ({consecutiveRecoveryFailures}/3 consecutive attempts).", ex);
                if (consecutiveRecoveryFailures >= 3 && !cancellationToken.IsCancellationRequested)
                {
                    consecutiveRecoveryFailures = 0;
                    _loggingService.LogWarning("Requesting a daemon restart after repeated screenshot device recovery failures.");
                    RestartRequested?.Invoke();
                }
            }
            catch (Exception ex)
            {
                consecutiveRecoveryFailures = 0;
                _loggingService.LogError("Screenshot capture failed.", ex);
            }
        }

        _loggingService.LogTrace("Screenshot capture loop stopped.");
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
            _loggingService.LogTrace("Screenshot capture is required because no screenshot has been saved yet.");
            return true;
        }

        bool activitySinceLastCapture = _userActivityService.HasActivitySince(lastSavedUtc);
        _loggingService.LogTrace($"Screenshot capture decision: activitySinceLastCapture={activitySinceLastCapture}, windowChanged={windowChanged}.");
        return activitySinceLastCapture || windowChanged;
    }

    private async Task<bool> SaveScreenshotAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var totalTimer = Stopwatch.StartNew();
        _loggingService.LogDebug($"Capturing screenshot for target '{settings.ScreenshotCaptureTarget}'.");
        CapturedImage? image = await CaptureScreenshotAsync(settings.ScreenshotCaptureTarget, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            _loggingService.LogTrace($"Screenshot capture for target '{settings.ScreenshotCaptureTarget}' was cancelled before persistence.");
            return false;
        }

        if (image == null)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _loggingService.LogWarning($"Screenshot capture returned no image for target '{settings.ScreenshotCaptureTarget}'.");
            }

            return false;
        }

        DateTime now = DateTime.Now;
        string dayFolder = Path.Combine(settings.ScreenshotsPath, now.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dayFolder);

        bool useJpeg = string.Equals(settings.ScreenshotImageFormat, "Jpeg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(settings.ScreenshotImageFormat, "Jpg", StringComparison.OrdinalIgnoreCase);
        string extension = useJpeg ? "jpg" : "png";

        // Keep a 4-digit fractional second suffix compatible with viewer parsing.
        string filename = $"{now:HH.mm.ss.ffff}.{extension}";
        string outputPath = Path.Combine(dayFolder, filename);

        var encodeTimer = Stopwatch.StartNew();
        using var fileStream = File.Create(outputPath);
        if (useJpeg)
        {
            JpegImageEncoder.Encode(image.Pixels, image.Width, image.Height, image.Stride, fileStream);
        }
        else
        {
            FastPngEncoder.Encode(image.Pixels, image.Width, image.Height, image.Stride, fileStream);
        }

        _loggingService.LogDebug($"Saved {image.Width}x{image.Height} {extension.ToUpperInvariant()} screenshot to '{outputPath}' in {encodeTimer.ElapsedMilliseconds} ms; total capture time was {totalTimer.ElapsedMilliseconds} ms.");
        return true;
    }

    private async Task<CapturedImage?> CaptureScreenshotAsync(string captureTarget, CancellationToken cancellationToken)
    {
        if (string.Equals(captureTarget, "ActiveApplication", StringComparison.OrdinalIgnoreCase))
        {
            _loggingService.LogTrace("Routing screenshot capture to the active application.");
            return await CaptureActiveApplicationAsync(cancellationToken);
        }

        if (captureTarget.StartsWith("Screen:", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(captureTarget["Screen:".Length..], out int screenIndex))
        {
            _loggingService.LogTrace($"Routing screenshot capture to screen index {screenIndex}.");
            return await CaptureSingleScreenAsync(screenIndex, cancellationToken);
        }

        _loggingService.LogTrace("Routing screenshot capture to all screens.");
        return await CaptureAllScreensAsync(cancellationToken);
    }

    private async Task<CapturedImage?> CaptureAllScreensAsync(CancellationToken cancellationToken)
    {
        var captureTimer = Stopwatch.StartNew();
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

        _loggingService.LogDebug($"Capturing {screens.Length} screen(s) into a {union.Width}x{union.Height} virtual-screen union.");

        var captures = new List<(System.Drawing.Rectangle bounds, CapturedImage image)>();
        foreach (var screen in screens)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            IntPtr monitorHandle = GetMonitorHandleForScreen(screen);
            var monitorTimer = Stopwatch.StartNew();
            _loggingService.LogTrace($"Capturing monitor '{screen.DeviceName}' (handle 0x{monitorHandle.ToInt64():X}, bounds {screen.Bounds}).");
            CapturedImage? bitmap = await _graphicsCaptureService.CaptureMonitorAsync(monitorHandle, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            if (bitmap != null)
            {
                captures.Add((screen.Bounds, bitmap));
                _loggingService.LogDebug($"Captured monitor '{screen.DeviceName}' as {bitmap.Width}x{bitmap.Height} in {monitorTimer.ElapsedMilliseconds} ms.");
            }
            else
            {
                _loggingService.LogWarning(
                    $"Monitor capture failed for '{screen.DeviceName}' with bounds {screen.Bounds} and monitor handle 0x{monitorHandle.ToInt64():X}. Using a black placeholder.");
                captures.Add((screen.Bounds, CreateBlackImage(screen.Bounds.Width, screen.Bounds.Height)));
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        int unionStride = checked(union.Width * 4);
        byte[] unionPixels = new byte[checked(unionStride * union.Height)];
        // Fill with opaque black so any gap between non-adjacent monitors is not left transparent.
        for (int alpha = 3; alpha < unionPixels.Length; alpha += 4)
        {
            unionPixels[alpha] = byte.MaxValue;
        }

        var compositionTimer = Stopwatch.StartNew();
        foreach (var capture in captures)
        {
            int destX = capture.bounds.Left - union.Left;
            int destY = capture.bounds.Top - union.Top;
            BlitInto(unionPixels, unionStride, union.Width, union.Height, capture.image, destX, destY);
        }

        _loggingService.LogDebug($"Composed {captures.Count} screen image(s) into {union.Width}x{union.Height} in {compositionTimer.ElapsedMilliseconds} ms; all-screen capture took {captureTimer.ElapsedMilliseconds} ms.");
        return new CapturedImage(union.Width, union.Height, unionStride, unionPixels);
    }

    private static void BlitInto(byte[] dest, int destStride, int destWidth, int destHeight, CapturedImage src, int destX, int destY)
    {
        if (destX >= destWidth || destY >= destHeight)
        {
            return;
        }

        int copyWidth = Math.Min(src.Width, destWidth - destX);
        int copyHeight = Math.Min(src.Height, destHeight - destY);
        if (copyWidth <= 0 || copyHeight <= 0)
        {
            return;
        }

        int copyBytes = copyWidth * 4;
        for (int row = 0; row < copyHeight; row++)
        {
            int srcOffset = row * src.Stride;
            int destOffset = ((destY + row) * destStride) + (destX * 4);
            Buffer.BlockCopy(src.Pixels, srcOffset, dest, destOffset, copyBytes);
        }
    }

    private static CapturedImage CreateBlackImage(int width, int height)
    {
        int stride = checked(width * 4);
        byte[] pixels = new byte[checked(stride * height)];
        for (int alphaOffset = 3; alphaOffset < pixels.Length; alphaOffset += 4)
        {
            pixels[alphaOffset] = byte.MaxValue;
        }

        return new CapturedImage(width, height, stride, pixels);
    }

    private async Task<CapturedImage?> CaptureSingleScreenAsync(int screenIndex, CancellationToken cancellationToken)
    {
        var screens = Screen.AllScreens;
        if (screenIndex < 0 || screenIndex >= screens.Length)
        {
            _loggingService.LogWarning($"Screenshot capture requested invalid screen index {screenIndex}; {screens.Length} screen(s) are available.");
            return null;
        }

        var screen = screens[screenIndex];
        IntPtr monitorHandle = GetMonitorHandleForScreen(screen);
        var captureTimer = Stopwatch.StartNew();
        _loggingService.LogDebug($"Capturing screen index {screenIndex} ('{screen.DeviceName}', handle 0x{monitorHandle.ToInt64():X}, bounds {screen.Bounds}).");
        CapturedImage? capture = await _graphicsCaptureService.CaptureMonitorAsync(monitorHandle, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (capture != null)
        {
            _loggingService.LogDebug($"Captured screen index {screenIndex} as {capture.Width}x{capture.Height} in {captureTimer.ElapsedMilliseconds} ms.");
            return capture;
        }

        _loggingService.LogWarning(
            $"Falling back to desktop crop for '{screen.DeviceName}' after monitor capture failure (handle 0x{monitorHandle.ToInt64():X}).");

        return await CaptureDesktopFallbackAsync(cancellationToken, screen.Bounds);
    }

    private async Task<CapturedImage?> CaptureActiveApplicationAsync(CancellationToken cancellationToken)
    {
        WindowChangedEventArgs? windowChanged;
        lock (_sync)
        {
            windowChanged = _lastWindowChangedEvent;
        }

        IntPtr windowHandle = windowChanged?.WindowHandle ?? IntPtr.Zero;
        bool usedForegroundFallback = false;
        if (windowHandle == IntPtr.Zero)
        {
            windowHandle = _windowWatcherService.GetForegroundWindow();
            usedForegroundFallback = true;
        }

        if (windowHandle == IntPtr.Zero)
        {
            _loggingService.LogWarning("Screenshot capture requested the active application, but no active window is available.");
            return null;
        }

        var captureTimer = Stopwatch.StartNew();
        _loggingService.LogDebug($"Capturing active application window handle 0x{windowHandle.ToInt64():X} using {(usedForegroundFallback ? "the foreground-window fallback" : "the last window-change event")}.");
        CapturedImage? capture = await _graphicsCaptureService.CaptureWindowAsync(windowHandle, cancellationToken);
        _loggingService.LogDebug($"Active application capture {(capture == null ? "did not produce an image" : $"produced a {capture.Width}x{capture.Height} image")} in {captureTimer.ElapsedMilliseconds} ms.");
        return capture;
    }

    private static IntPtr GetMonitorHandleForScreen(Screen screen)
    {
        int centerX = screen.Bounds.Left + (screen.Bounds.Width / 2);
        int centerY = screen.Bounds.Top + (screen.Bounds.Height / 2);
        return MonitorFromPoint(new POINT { X = centerX, Y = centerY }, MONITOR_DEFAULTTONEAREST);
    }

    private async Task<CapturedImage?> CaptureDesktopFallbackAsync(CancellationToken cancellationToken, System.Drawing.Rectangle? cropBounds = null)
    {
        var fallbackTimer = Stopwatch.StartNew();
        _loggingService.LogDebug($"Starting desktop fallback capture{(cropBounds == null ? string.Empty : $" for bounds {cropBounds.Value}")}.");
        CapturedImage? desktopCapture = await _graphicsCaptureService.CaptureDesktopAsync(cancellationToken);
        if (desktopCapture == null)
        {
            _loggingService.LogWarning("Desktop fallback capture did not return an image.");
            return null;
        }

        if (cropBounds == null)
        {
            _loggingService.LogDebug($"Desktop fallback produced {desktopCapture.Width}x{desktopCapture.Height} in {fallbackTimer.ElapsedMilliseconds} ms.");
            return desktopCapture;
        }

        var virtualScreen = SystemInformation.VirtualScreen;
        int x = cropBounds.Value.Left - virtualScreen.Left;
        int y = cropBounds.Value.Top - virtualScreen.Top;
        int width = cropBounds.Value.Width;
        int height = cropBounds.Value.Height;

        if (x < 0 || y < 0 || width <= 0 || height <= 0
            || x + width > desktopCapture.Width
            || y + height > desktopCapture.Height)
        {
            _loggingService.LogWarning(
                $"Desktop fallback crop is out of range. VirtualScreen={virtualScreen}, Crop={cropBounds.Value}, ImageSize={desktopCapture.Width}x{desktopCapture.Height}.");
            return null;
        }

        var cropped = CropImage(desktopCapture, x, y, width, height);
        _loggingService.LogDebug($"Desktop fallback crop produced {cropped.Width}x{cropped.Height} in {fallbackTimer.ElapsedMilliseconds} ms.");
        return cropped;
    }

    private static CapturedImage CropImage(CapturedImage source, int x, int y, int width, int height)
    {
        int stride = checked(width * 4);
        byte[] pixels = new byte[checked(stride * height)];
        int rowBytes = width * 4;
        for (int row = 0; row < height; row++)
        {
            int srcOffset = ((y + row) * source.Stride) + (x * 4);
            Buffer.BlockCopy(source.Pixels, srcOffset, pixels, row * stride, rowBytes);
        }

        return new CapturedImage(width, height, stride, pixels);
    }

    private void WindowWatcherServiceOnWindowChanged(object? sender, WindowChangedEventArgs e)
    {
        lock (_sync)
        {
            _lastWindowChangedEvent = e;
            _windowChangedSinceLastCapture = true;
        }

        _loggingService.LogTrace($"Screenshot capture received a window change for handle 0x{e.WindowHandle.ToInt64():X}.");
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
