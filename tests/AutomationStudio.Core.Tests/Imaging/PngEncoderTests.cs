using System.Buffers.Binary;
using System.IO.Compression;
using AutomationStudio.Abstractions.Imaging;

namespace AutomationStudio.Core.Tests.Imaging;

public class PngEncoderTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    public void EncodeRgba32_RejectsNonPositiveDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PngEncoder.EncodeRgba32(new byte[4], width, height));
    }

    [Fact]
    public void EncodeRgba32_RejectsBufferOfWrongLength()
    {
        Assert.Throws<ArgumentException>(() => PngEncoder.EncodeRgba32(new byte[3], 1, 1));
    }

    [Fact]
    public void EncodeRgba32_StartsWithThePngSignature()
    {
        var png = PngEncoder.EncodeRgba32(new byte[4], 1, 1);
        ReadOnlySpan<byte> expected = [137, 80, 78, 71, 13, 10, 26, 10];
        Assert.True(png.AsSpan(0, 8).SequenceEqual(expected));
    }

    [Fact]
    public void EncodeRgba32_IhdrDeclaresCorrectDimensionsAndFormat()
    {
        var png = PngEncoder.EncodeRgba32(new byte[3 * 5 * 4], 3, 5);

        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(5, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));

        var bitDepth = png[24];
        var colorType = png[25];
        Assert.Equal(8, bitDepth);
        Assert.Equal(6, colorType); // RGBA
    }

    [Fact]
    public void EncodeRgba32_PixelDataRoundTripsThroughZlibInflate()
    {
        const int width = 2;
        const int height = 2;
        byte[] pixels =
        [
            255, 0, 0, 255,   0, 255, 0, 255,   // row 0: red, green
            0, 0, 255, 255,   10, 20, 30, 40,   // row 1: blue, arbitrary
        ];

        var png = PngEncoder.EncodeRgba32(pixels, width, height);
        var idat = ExtractIdatPayload(png);

        using var compressed = new MemoryStream(idat);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        using var inflated = new MemoryStream();
        zlib.CopyTo(inflated);
        var scanlines = inflated.ToArray();

        var stride = width * 4;
        Assert.Equal((stride + 1) * height, scanlines.Length);

        for (var y = 0; y < height; y++)
        {
            var rowStart = y * (stride + 1);
            Assert.Equal(0, scanlines[rowStart]); // filter type: None
            var rowPixels = scanlines.AsSpan(rowStart + 1, stride);
            Assert.True(rowPixels.SequenceEqual(pixels.AsSpan(y * stride, stride)));
        }
    }

    /// <summary>Assumes a single IDAT chunk, which is what <see cref="PngEncoder"/> always emits.</summary>
    private static byte[] ExtractIdatPayload(byte[] png)
    {
        var offset = 8; // past the signature
        while (offset < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            var type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
            var dataStart = offset + 8;

            if (type == "IDAT")
            {
                return png.AsSpan(dataStart, length).ToArray();
            }

            offset = dataStart + length + 4; // +4 for the trailing CRC
        }

        throw new InvalidOperationException("No IDAT chunk found.");
    }
}
