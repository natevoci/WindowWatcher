using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;

namespace WindowWatcher.Daemon;

public sealed class GraphicsCaptureService : IDisposable
{
    private readonly IDirect3DDevice _direct3DDevice;

    public GraphicsCaptureService()
    {
        _direct3DDevice = CreateDirect3DDevice();
    }

    public async Task<BitmapSource?> CaptureWindowAsync(IntPtr windowHandle, CancellationToken cancellationToken)
    {
        if (windowHandle == IntPtr.Zero || !GraphicsCaptureSession.IsSupported())
        {
            return null;
        }

        var item = CreateItemForWindow(windowHandle);
        if (item == null)
        {
            return null;
        }

        return await CaptureItemAsync(item, cancellationToken);
    }

    public async Task<BitmapSource?> CaptureMonitorAsync(IntPtr monitorHandle, CancellationToken cancellationToken)
    {
        if (monitorHandle == IntPtr.Zero || !GraphicsCaptureSession.IsSupported())
        {
            return null;
        }

        var item = CreateItemForMonitor(monitorHandle);
        if (item == null)
        {
            return null;
        }

        return await CaptureItemAsync(item, cancellationToken);
    }

    private async Task<BitmapSource?> CaptureItemAsync(GraphicsCaptureItem item, CancellationToken cancellationToken)
    {
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

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            using var cancellationRegistration = linkedCts.Token.Register(() => tcs.TrySetCanceled(linkedCts.Token));

            using var frame = await tcs.Task;
            return await ConvertFrameToBitmapSourceAsync(frame);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            framePool.FrameArrived -= handler;
        }
    }

    private static async Task<BitmapSource?> ConvertFrameToBitmapSourceAsync(Direct3D11CaptureFrame frame)
    {
        SoftwareBitmap? softwareBitmap = null;
        SoftwareBitmap? normalizedBitmap = null;

        try
        {
            softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
            normalizedBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            int width = normalizedBitmap.PixelWidth;
            int height = normalizedBitmap.PixelHeight;
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            normalizedBitmap.CopyToBuffer(pixels.AsBuffer());

            var bitmapSource = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            bitmapSource.Freeze();
            return bitmapSource;
        }
        finally
        {
            normalizedBitmap?.Dispose();
            softwareBitmap?.Dispose();
        }
    }

    private static GraphicsCaptureItem? CreateItemForWindow(IntPtr windowHandle)
    {
        var interop = GetCaptureItemInterop();
        Guid iid = typeof(GraphicsCaptureItem).GUID;

        IntPtr itemPointer = interop.CreateForWindow(windowHandle, iid);
        if (itemPointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.GetObjectForIUnknown(itemPointer) as GraphicsCaptureItem;
        }
        finally
        {
            Marshal.Release(itemPointer);
        }
    }

    private static GraphicsCaptureItem? CreateItemForMonitor(IntPtr monitorHandle)
    {
        var interop = GetCaptureItemInterop();
        Guid iid = typeof(GraphicsCaptureItem).GUID;

        IntPtr itemPointer = interop.CreateForMonitor(monitorHandle, iid);
        if (itemPointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.GetObjectForIUnknown(itemPointer) as GraphicsCaptureItem;
        }
        finally
        {
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

            return (IDirect3DDevice)Marshal.GetObjectForIUnknown(inspectable);
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
        if (_direct3DDevice is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static IGraphicsCaptureItemInterop GetCaptureItemInterop()
    {
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

            return (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factory);
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
        IntPtr CreateForWindow(IntPtr window, [In] Guid riid);
        IntPtr CreateForMonitor(IntPtr monitor, [In] Guid riid);
    }

    private const int D3D_DRIVER_TYPE_HARDWARE = 1;
    private const int D3D_DRIVER_TYPE_WARP = 5;
    private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
    private const uint D3D11_SDK_VERSION = 7;

    private static readonly Guid IID_IDXGIDevice = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");

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
}
