using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Diagnostics;
using Microsoft.Win32;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Daemon;

public sealed class GraphicsCaptureService : IDisposable
{
    private readonly IDirect3DDevice _direct3DDevice;
    private readonly LoggingService _loggingService;

    // Creating a GraphicsCaptureItem (CreateForMonitor) is the dominant per-capture setup cost
    // (~150 ms per monitor). A monitor's capture item stays valid until the display configuration
    // changes, so items are cached and only rebuilt when Windows reports a display settings change.
    private readonly object _cacheLock = new();
    private readonly Dictionary<IntPtr, GraphicsCaptureItem> _monitorItemCache = new();
    private IGraphicsCaptureItemInterop? _captureItemInterop;
    private bool _disposed;

    public GraphicsCaptureService(LoggingService loggingService)
    {
        _loggingService = loggingService;
        var deviceTimer = Stopwatch.StartNew();
        _direct3DDevice = CreateDirect3DDevice();
        _loggingService.LogDebug($"Initialized the Direct3D capture device in {deviceTimer.ElapsedMilliseconds} ms.");

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        lock (_cacheLock)
        {
            if (_monitorItemCache.Count == 0)
            {
                return;
            }

            _monitorItemCache.Clear();
        }

        _loggingService.LogDebug("Display settings changed; cleared cached monitor capture items.");
    }

    public async Task<CapturedImage?> CaptureWindowAsync(IntPtr windowHandle, CancellationToken cancellationToken)
    {
        var captureTimer = Stopwatch.StartNew();
        if (windowHandle == IntPtr.Zero)
        {
            _loggingService.LogWarning("Window capture was requested with an invalid window handle.");
            return null;
        }

        if (!GraphicsCaptureSession.IsSupported())
        {
            _loggingService.LogWarning("Window capture is not supported on this version of Windows.");
            return null;
        }

        var item = CreateItemForWindow(windowHandle);
        if (item == null)
        {
            _loggingService.LogWarning("Window capture item could not be created.");
            return null;
        }

        _loggingService.LogTrace($"Created graphics capture item for window handle 0x{windowHandle.ToInt64():X} with size {item.Size.Width}x{item.Size.Height}.");
        CapturedImage? capture = await CaptureItemAsync(item, cancellationToken);
        _loggingService.LogDebug($"Window capture for handle 0x{windowHandle.ToInt64():X} {(capture == null ? "did not produce an image" : $"produced {capture.Width}x{capture.Height}")} in {captureTimer.ElapsedMilliseconds} ms.");
        return capture;
    }

    public async Task<CapturedImage?> CaptureMonitorAsync(IntPtr monitorHandle, CancellationToken cancellationToken)
    {
        var captureTimer = Stopwatch.StartNew();
        if (monitorHandle == IntPtr.Zero)
        {
            _loggingService.LogWarning("Monitor capture was requested with an invalid monitor handle.");
            return null;
        }

        if (!GraphicsCaptureSession.IsSupported())
        {
            _loggingService.LogWarning("Monitor capture is not supported on this version of Windows.");
            return null;
        }

        var item = CreateItemForMonitor(monitorHandle);
        if (item == null)
        {
            _loggingService.LogWarning("Monitor capture item could not be created.");
            return null;
        }

        _loggingService.LogTrace($"Created graphics capture item for monitor handle 0x{monitorHandle.ToInt64():X} with size {item.Size.Width}x{item.Size.Height}.");
        CapturedImage? capture = await CaptureItemAsync(item, cancellationToken);
        _loggingService.LogDebug($"Monitor capture for handle 0x{monitorHandle.ToInt64():X} {(capture == null ? "did not produce an image" : $"produced {capture.Width}x{capture.Height}")} in {captureTimer.ElapsedMilliseconds} ms.");
        return capture;
    }

    public async Task<CapturedImage?> CaptureDesktopAsync(CancellationToken cancellationToken)
    {
        IntPtr desktopWindow = GetDesktopWindow();
        if (desktopWindow == IntPtr.Zero)
        {
            _loggingService.LogWarning("Desktop capture is unavailable because the desktop window handle is invalid.");
            return null;
        }

        return await CaptureWindowAsync(desktopWindow, cancellationToken);
    }

    private async Task<CapturedImage?> CaptureItemAsync(GraphicsCaptureItem item, CancellationToken cancellationToken)
    {
        var captureTimer = Stopwatch.StartNew();
        _loggingService.LogTrace($"Creating frame pool and capture session for a {item.Size.Width}x{item.Size.Height} item.");
        using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _direct3DDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            1,
            item.Size);

        using var session = framePool.CreateCaptureSession(item);
        var tcs = new TaskCompletionSource<Direct3D11CaptureFrame>(TaskCreationOptions.RunContinuationsAsynchronously);

        TypedEventHandler<Direct3D11CaptureFramePool, object>? handler = null;
        handler = (sender, _) =>
        {
            try
            {
                var frame = sender.TryGetNextFrame();
                if (frame != null)
                {
                    _loggingService.LogTrace($"Graphics capture frame arrived after {captureTimer.ElapsedMilliseconds} ms.");
                    tcs.TrySetResult(frame);
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        };

        framePool.FrameArrived += handler;
        session.StartCapture();
        _loggingService.LogTrace("Graphics capture session started; waiting for the first frame.");

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            using var cancellationRegistration = linkedCts.Token.Register(() => tcs.TrySetCanceled(linkedCts.Token));

            using var frame = await tcs.Task;
            CapturedImage? bitmap = await ConvertFrameToCapturedImageAsync(frame);
            _loggingService.LogTrace($"Graphics capture frame processing completed in {captureTimer.ElapsedMilliseconds} ms.");
            return bitmap;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _loggingService.LogTrace($"Graphics capture was cancelled after {captureTimer.ElapsedMilliseconds} ms.");
            return null;
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogWarning("Graphics capture timed out while waiting for a frame.");
            return null;
        }
        finally
        {
            framePool.FrameArrived -= handler;
        }
    }

    private async Task<CapturedImage?> ConvertFrameToCapturedImageAsync(Direct3D11CaptureFrame frame)
    {
        var conversionTimer = Stopwatch.StartNew();
        SoftwareBitmap? softwareBitmap = null;
        SoftwareBitmap? normalizedBitmap = null;

        try
        {
            softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
            _loggingService.LogTrace($"Copied capture surface to a software bitmap in {conversionTimer.ElapsedMilliseconds} ms.");
            // Use Ignore (not Premultiplied): Windows.Graphics.Capture often reports alpha = 0 for
            // opaque desktop content, and premultiplying by that zero alpha would blacken every
            // pixel. Ignore leaves the RGB channels untouched; the PNG encoder writes opaque RGB.
            normalizedBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);

            int width = normalizedBitmap.PixelWidth;
            int height = normalizedBitmap.PixelHeight;
            if (width <= 0 || height <= 0)
            {
                _loggingService.LogWarning($"Graphics capture returned an invalid frame size of {width}x{height}.");
                return null;
            }

            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            normalizedBitmap.CopyToBuffer(pixels.AsBuffer());

            _loggingService.LogDebug($"Converted graphics capture frame to {width}x{height} BGRA buffer in {conversionTimer.ElapsedMilliseconds} ms ({pixels.Length:N0} bytes).");
            return new CapturedImage(width, height, stride, pixels);
        }
        finally
        {
            normalizedBitmap?.Dispose();
            softwareBitmap?.Dispose();
        }
    }

    private GraphicsCaptureItem? CreateItemForWindow(IntPtr windowHandle)
    {
        var itemTimer = Stopwatch.StartNew();
        var interop = GetCaptureItemInterop();
        Guid iid = IID_IGraphicsCaptureItem;

        int hr = interop.CreateForWindow(windowHandle, ref iid, out IntPtr itemPointer);
        _loggingService.LogTrace($"CreateForWindow for handle 0x{windowHandle.ToInt64():X} returned HRESULT 0x{hr:X8} in {itemTimer.ElapsedMilliseconds} ms.");
        if (hr == E_NOINTERFACE || hr == E_NOTIMPL || hr == E_ACCESSDENIED || hr == E_INVALIDARG)
        {
            _loggingService.LogWarning($"Windows could not create a capture item for the active window (HRESULT 0x{hr:X8}).");
            return null;
        }
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        if (itemPointer == IntPtr.Zero)
        {
            _loggingService.LogWarning("Windows returned an empty capture item for the active window.");
            return null;
        }

        return CreateProjectedCaptureItem(itemPointer, "active window");
    }

    private GraphicsCaptureItem? CreateItemForMonitor(IntPtr monitorHandle)
    {
        lock (_cacheLock)
        {
            if (_monitorItemCache.TryGetValue(monitorHandle, out GraphicsCaptureItem? cached))
            {
                _loggingService.LogTrace($"Reusing cached capture item for monitor handle 0x{monitorHandle.ToInt64():X}.");
                return cached;
            }
        }

        var itemTimer = Stopwatch.StartNew();
        var interop = GetCaptureItemInterop();
        Guid iid = IID_IGraphicsCaptureItem;

        int hr = interop.CreateForMonitor(monitorHandle, ref iid, out IntPtr itemPointer);
        _loggingService.LogTrace($"CreateForMonitor for handle 0x{monitorHandle.ToInt64():X} returned HRESULT 0x{hr:X8} in {itemTimer.ElapsedMilliseconds} ms.");
        if (hr == E_NOINTERFACE || hr == E_NOTIMPL || hr == E_ACCESSDENIED || hr == E_INVALIDARG)
        {
            _loggingService.LogWarning($"Windows could not create a capture item for the monitor (HRESULT 0x{hr:X8}).");
            return null;
        }
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        if (itemPointer == IntPtr.Zero)
        {
            _loggingService.LogWarning("Windows returned an empty capture item for the monitor.");
            return null;
        }

        GraphicsCaptureItem? item = CreateProjectedCaptureItem(itemPointer, $"monitor handle 0x{monitorHandle.ToInt64():X}");
        if (item != null)
        {
            lock (_cacheLock)
            {
                _monitorItemCache[monitorHandle] = item;
            }
        }

        return item;
    }

    private GraphicsCaptureItem? CreateProjectedCaptureItem(IntPtr itemPointer, string sourceDescription)
    {
        var projectionTimer = Stopwatch.StartNew();
        try
        {
            // The interop APIs hand back a raw ABI pointer to IGraphicsCaptureItem. Classic COM
            // interop (Marshal.GetObjectForIUnknown) produces a __ComObject that cannot be cast to
            // the CsWinRT projected type, so use the WinRT marshaller to build a proper projection.
            var captureItem = WinRT.MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer);
            if (captureItem == null)
            {
                _loggingService.LogWarning($"WinRT projection returned null for capture item from {sourceDescription}.");
            }

            _loggingService.LogTrace($"Projected graphics capture item for {sourceDescription} in {projectionTimer.ElapsedMilliseconds} ms.");

            return captureItem;
        }
        catch (Exception ex)
        {
            _loggingService.LogWarning($"Capture item projection failed for {sourceDescription}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
        finally
        {
            // FromAbi takes its own reference, so release the one handed back by the interop call.
            Marshal.Release(itemPointer);
        }
    }

    private static IDirect3DDevice CreateDirect3DDevice()
    {
        int hr = D3D11CreateDevice(
            IntPtr.Zero,
            D3D_DRIVER_TYPE_HARDWARE,
            IntPtr.Zero,
            D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            IntPtr.Zero,
            0,
            D3D11_SDK_VERSION,
            out IntPtr d3dDevice,
            out _,
            out IntPtr d3dContext);

        if (hr < 0)
        {
            hr = D3D11CreateDevice(
                IntPtr.Zero,
                D3D_DRIVER_TYPE_WARP,
                IntPtr.Zero,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                IntPtr.Zero,
                0,
                D3D11_SDK_VERSION,
                out d3dDevice,
                out _,
                out d3dContext);
        }

        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        IntPtr dxgiDevice = IntPtr.Zero;
        IntPtr inspectable = IntPtr.Zero;

        try
        {
            Guid idxgiDeviceGuid = IID_IDXGIDevice;
            int qiHr = Marshal.QueryInterface(d3dDevice, ref idxgiDeviceGuid, out dxgiDevice);
            if (qiHr < 0)
            {
                Marshal.ThrowExceptionForHR(qiHr);
            }

            int createHr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out inspectable);
            if (createHr < 0)
            {
                Marshal.ThrowExceptionForHR(createHr);
            }

            // Project the ABI pointer into the WinRT type. Classic COM interop yields a __ComObject
            // that CsWinRT cannot marshal back across the projected Direct3D11CaptureFramePool APIs.
            return WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
        }
        finally
        {
            if (inspectable != IntPtr.Zero)
            {
                Marshal.Release(inspectable);
            }

            if (dxgiDevice != IntPtr.Zero)
            {
                Marshal.Release(dxgiDevice);
            }

            if (d3dContext != IntPtr.Zero)
            {
                Marshal.Release(d3dContext);
            }

            if (d3dDevice != IntPtr.Zero)
            {
                Marshal.Release(d3dDevice);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

        lock (_cacheLock)
        {
            _monitorItemCache.Clear();
            _captureItemInterop = null;
        }

        if (_direct3DDevice is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private IGraphicsCaptureItemInterop GetCaptureItemInterop()
    {
        lock (_cacheLock)
        {
            if (_captureItemInterop != null)
            {
                return _captureItemInterop;
            }
        }

        IntPtr classId = IntPtr.Zero;
        IntPtr factory = IntPtr.Zero;
        try
        {
            int hStringResult = WindowsCreateString(
                "Windows.Graphics.Capture.GraphicsCaptureItem",
                "Windows.Graphics.Capture.GraphicsCaptureItem".Length,
                out classId);
            if (hStringResult < 0)
            {
                Marshal.ThrowExceptionForHR(hStringResult);
            }

            Guid interopGuid = typeof(IGraphicsCaptureItemInterop).GUID;
            int hr = RoGetActivationFactory(classId, ref interopGuid, out factory);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factory);
            lock (_cacheLock)
            {
                _captureItemInterop ??= interop;
                return _captureItemInterop;
            }
        }
        finally
        {
            if (factory != IntPtr.Zero)
            {
                Marshal.Release(factory);
            }

            if (classId != IntPtr.Zero)
            {
                WindowsDeleteString(classId);
            }
        }
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig]
        int CreateForWindow(IntPtr window, [In] ref Guid riid, out IntPtr result);

        [PreserveSig]
        int CreateForMonitor(IntPtr monitor, [In] ref Guid riid, out IntPtr result);
    }

    private const int D3D_DRIVER_TYPE_HARDWARE = 1;
    private const int D3D_DRIVER_TYPE_WARP = 5;
    private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
    private const uint D3D11_SDK_VERSION = 7;
    private const int E_NOINTERFACE = unchecked((int)0x80004002);
    private const int E_NOTIMPL = unchecked((int)0x80004001);
    private const int E_ACCESSDENIED = unchecked((int)0x80070005);
    private const int E_INVALIDARG = unchecked((int)0x80070057);

    private static readonly Guid IID_IDXGIDevice = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
    private static readonly Guid IID_IGraphicsCaptureItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    [DllImport("d3d11.dll", SetLastError = true)]
    private static extern int D3D11CreateDevice(
        IntPtr pAdapter,
        int driverType,
        IntPtr software,
        uint flags,
        IntPtr pFeatureLevels,
        uint featureLevels,
        uint sdkVersion,
        out IntPtr ppDevice,
        out int pFeatureLevel,
        out IntPtr ppImmediateContext);

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

    [DllImport("combase.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string sourceString, int length, out IntPtr hString);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(IntPtr hString);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();
}
