namespace WindowWatcher.Daemon;

/// <summary>
/// A captured, top-down BGRA32 image held as a raw pixel buffer. Keeping captures as plain
/// buffers avoids WPF imaging objects on the capture hot path, so composition and PNG encoding
/// can run as simple memory operations instead of the software rasterizer.
/// </summary>
public sealed class CapturedImage
{
    public CapturedImage(int width, int height, int stride, byte[] pixels)
    {
        Width = width;
        Height = height;
        Stride = stride;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public byte[] Pixels { get; }
}
