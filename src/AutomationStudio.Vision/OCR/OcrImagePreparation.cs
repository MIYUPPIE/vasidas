using AutomationStudio.Abstractions;
using SkiaSharp;

namespace AutomationStudio.Vision.OCR;

/// <summary>
/// Applies <see cref="OcrOptions.Region"/> (crop) and <see cref="OcrOptions.Scale"/> (upscale) before
/// handing pixels to an OCR engine, and translates results back to full-screenshot coordinates
/// afterward — shared so every <see cref="IOcrEngine"/> implementation behaves identically regardless
/// of which recognizer does the actual text detection.
/// </summary>
internal static class OcrImagePreparation
{
    public readonly record struct PreparedImage(SKBitmap Bitmap, int OffsetX, int OffsetY, double Scale) : IDisposable
    {
        public void Dispose() => Bitmap.Dispose();
    }

    public static PreparedImage Prepare(byte[] pngData, OcrOptions options)
    {
        using var full = SKBitmap.Decode(pngData) ?? throw new InvalidOperationException("Could not decode screenshot PNG for OCR.");

        SKBitmap cropped;
        int offsetX = 0, offsetY = 0;
        if (options.Region is { } region)
        {
            var bounds = SKRectI.Create(0, 0, full.Width, full.Height);
            var requested = SKRectI.Create(region.X, region.Y, region.Width, region.Height);
            if (!requested.IntersectsWith(bounds))
            {
                throw new InvalidOperationException($"OCR region {region} does not intersect the {full.Width}x{full.Height} screenshot.");
            }

            requested.Intersect(bounds);
            cropped = new SKBitmap(requested.Width, requested.Height);
            using (var canvas = new SKCanvas(cropped))
            {
                canvas.DrawBitmap(full, requested, SKRect.Create(0, 0, requested.Width, requested.Height));
            }

            offsetX = requested.Left;
            offsetY = requested.Top;
        }
        else
        {
            cropped = full.Copy();
        }

        if (Math.Abs(options.Scale - 1.0) < 0.001)
        {
            return new PreparedImage(cropped, offsetX, offsetY, 1.0);
        }

        var scaledWidth = Math.Max(1, (int)Math.Round(cropped.Width * options.Scale));
        var scaledHeight = Math.Max(1, (int)Math.Round(cropped.Height * options.Scale));
        var scaledInfo = new SKImageInfo(scaledWidth, scaledHeight, cropped.ColorType, cropped.AlphaType);
        var scaled = cropped.Resize(scaledInfo, SKSamplingOptions.Default);
        cropped.Dispose();

        return scaled is null
            ? new PreparedImage(SKBitmap.Decode(pngData)!, offsetX, offsetY, 1.0)
            : new PreparedImage(scaled, offsetX, offsetY, options.Scale);
    }

    /// <summary>Maps a bounding box from the prepared (cropped + scaled) image back to full-screenshot coordinates.</summary>
    public static ScreenRect ToScreenRect(this PreparedImage image, float minX, float minY, float maxX, float maxY)
    {
        var x = (int)Math.Round(minX / image.Scale) + image.OffsetX;
        var y = (int)Math.Round(minY / image.Scale) + image.OffsetY;
        var width = Math.Max(1, (int)Math.Round((maxX - minX) / image.Scale));
        var height = Math.Max(1, (int)Math.Round((maxY - minY) / image.Scale));
        return new ScreenRect(x, y, width, height);
    }
}
