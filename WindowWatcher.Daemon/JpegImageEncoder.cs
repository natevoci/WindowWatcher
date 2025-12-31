using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowWatcher.Daemon;

/// <summary>
/// Encodes a raw BGRA32 buffer to JPEG using WIC (via WPF imaging). WIC's JPEG encoder is
/// significantly faster than DEFLATE-based PNG encoding and produces much smaller files, and the
/// viewer decodes JPEG through WIC/Direct2D with hardware acceleration. JPEG is lossy, so PNG
/// remains available for callers that need a lossless format.
/// </summary>
internal static class JpegImageEncoder
{
    // Balances visual quality against encode speed and file size for activity screenshots.
    private const int DefaultQuality = 85;

    public static void Encode(byte[] bgra, int width, int height, int stride, Stream output)
    {
        // Bgr32 ignores the alpha byte and treats the image as fully opaque, which matches the
        // PNG path (WGC frames report alpha=0 for opaque desktop content).
        BitmapSource source = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgr32,
            null,
            bgra,
            stride);
        source.Freeze();

        var encoder = new JpegBitmapEncoder { QualityLevel = DefaultQuality };
        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(output);
    }
}
