using System.IO;
using System.IO.Compression;

namespace WindowWatcher.Daemon;

/// <summary>
/// A lightweight PNG encoder tuned for low CPU usage. It writes a single "None" filter per
/// scanline and compresses with <see cref="CompressionLevel.Fastest"/>, which is dramatically
/// cheaper than <c>PngBitmapEncoder</c>'s adaptive filtering plus high-effort DEFLATE. The trade
/// off is a larger file, which is an acceptable exchange for reduced CPU on frequent captures.
/// PNG (DEFLATE) cannot be hardware accelerated, so reducing compression effort is the practical
/// lever for CPU cost.
///
/// Input is BGRA32; output is opaque 24-bit RGB (PNG colour type 2). Screenshots are always
/// opaque, so dropping the alpha channel both guarantees a visible image and trims a quarter of
/// the pixel bytes fed to DEFLATE.
/// </summary>
internal static class FastPngEncoder
{
    private static readonly uint[] CrcTable = BuildCrcTable();
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static void Encode(byte[] bgra, int width, int height, int stride, Stream output)
        => Encode(bgra, width, height, stride, output, CompressionLevel.Fastest);

    public static void Encode(byte[] bgra, int width, int height, int stride, Stream output, CompressionLevel compressionLevel)
    {
        output.Write(Signature, 0, Signature.Length);

        byte[] header = new byte[13];
        WriteBigEndian(header, 0, (uint)width);
        WriteBigEndian(header, 4, (uint)height);
        header[8] = 8;  // bit depth
        header[9] = 2;  // color type: truecolour (RGB)
        header[10] = 0; // compression method
        header[11] = 0; // filter method
        header[12] = 0; // interlace method
        WriteChunk(output, "IHDR", header, header.Length);

        byte[] idat = BuildImageData(bgra, width, height, stride, compressionLevel);
        WriteChunk(output, "IDAT", idat, idat.Length);

        WriteChunk(output, "IEND", Array.Empty<byte>(), 0);
    }

    private static byte[] BuildImageData(byte[] bgra, int width, int height, int stride, CompressionLevel level)
    {
        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, level, leaveOpen: true))
        {
            byte[] row = new byte[width * 3 + 1];
            row[0] = 0; // filter type: None

            for (int y = 0; y < height; y++)
            {
                int srcOffset = y * stride;
                int dst = 1;
                for (int x = 0; x < width; x++)
                {
                    int p = srcOffset + (x * 4);
                    // Source is BGRA; PNG expects RGB.
                    row[dst++] = bgra[p + 2];
                    row[dst++] = bgra[p + 1];
                    row[dst++] = bgra[p];
                }

                deflate.Write(row, 0, row.Length);
            }
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data, int length)
    {
        byte[] lengthBytes = new byte[4];
        WriteBigEndian(lengthBytes, 0, (uint)length);
        output.Write(lengthBytes, 0, lengthBytes.Length);

        byte[] typeBytes = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            typeBytes[i] = (byte)type[i];
        }

        output.Write(typeBytes, 0, typeBytes.Length);

        if (length > 0)
        {
            output.Write(data, 0, length);
        }

        uint crc = Crc32(typeBytes, data, length);
        byte[] crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, crc);
        output.Write(crcBytes, 0, crcBytes.Length);
    }

    private static uint Crc32(byte[] type, byte[] data, int length)
    {
        uint crc = 0xFFFFFFFFu;
        for (int i = 0; i < type.Length; i++)
        {
            crc = CrcTable[(crc ^ type[i]) & 0xFF] ^ (crc >> 8);
        }

        for (int i = 0; i < length; i++)
        {
            crc = CrcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        uint[] table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private static void WriteBigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
