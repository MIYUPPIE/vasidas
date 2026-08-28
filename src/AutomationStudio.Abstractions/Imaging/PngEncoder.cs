using System.Buffers.Binary;
using System.IO.Compression;

namespace AutomationStudio.Abstractions.Imaging;

/// <summary>
/// Minimal dependency-free PNG encoder (BCL only — <see cref="ZLibStream"/> covers the DEFLATE
/// layer PNG needs). Both the Windows (GDI) and Linux (XGetImage) screenshot backends capture raw
/// pixels in very different ways but must produce the same <see cref="Screenshot.PngData"/>
/// format, so the encoder lives here rather than being duplicated per platform.
/// </summary>
public static class PngEncoder
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Encodes a top-down, 4-bytes-per-pixel RGBA buffer as a PNG.</summary>
    public static byte[] EncodeRgba32(ReadOnlySpan<byte> rgba, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive.");
        }

        var expectedLength = (long)width * height * 4;
        if (rgba.Length != expectedLength)
        {
            throw new ArgumentException(
                $"Expected {expectedLength} bytes for a {width}x{height} RGBA32 image, got {rgba.Length}.",
                nameof(rgba));
        }

        using var output = new MemoryStream();
        output.Write(Signature);

        WriteChunk(output, "IHDR", BuildIhdr(width, height));
        WriteChunk(output, "IDAT", DeflateScanlines(rgba, width, height));
        WriteChunk(output, "IEND", []);

        return output.ToArray();
    }

    private static byte[] BuildIhdr(int width, int height)
    {
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // color type: RGBA
        ihdr[10] = 0; // compression method: deflate
        ihdr[11] = 0; // filter method: adaptive (per-scanline filter byte)
        ihdr[12] = 0; // interlace method: none
        return ihdr;
    }

    private static byte[] DeflateScanlines(ReadOnlySpan<byte> rgba, int width, int height)
    {
        var stride = width * 4;
        using var raw = new MemoryStream();
        using (var zlib = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
        {
            var row = new byte[stride + 1]; // leading filter-type byte, 0 = None
            for (var y = 0; y < height; y++)
            {
                rgba.Slice(y * stride, stride).CopyTo(row.AsSpan(1));
                zlib.Write(row);
            }
        }

        return raw.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> lengthBuffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, data.Length);
        output.Write(lengthBuffer);

        var typeBytes = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            typeBytes[i] = (byte)type[i];
        }

        output.Write(typeBytes);
        output.Write(data);

        var crc = Crc32.Compute(typeBytes, data);
        Span<byte> crcBuffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBuffer, crc);
        output.Write(crcBuffer);
    }

    private static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                var c = n;
                for (var k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                }

                table[n] = c;
            }

            return table;
        }

        public static uint Compute(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var t in a)
            {
                crc = Table[(crc ^ t) & 0xFF] ^ (crc >> 8);
            }

            foreach (var t in b)
            {
                crc = Table[(crc ^ t) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFFFFFFu;
        }
    }
}
