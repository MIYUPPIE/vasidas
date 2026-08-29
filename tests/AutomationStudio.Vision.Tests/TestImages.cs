using AutomationStudio.Abstractions;
using SkiaSharp;

namespace AutomationStudio.Vision.Tests;

/// <summary>Renders real PNG screenshots with known text at a known position, in-process — no external image tool or fixture file needed to exercise OCR against real pixels.</summary>
internal static class TestImages
{
    public static Screenshot Render(string text, int width = 400, int height = 120, int fontSize = 32)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using var font = new SKFont(SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold), fontSize);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        canvas.DrawText(text, 20, height / 2f + fontSize / 3f, font, paint);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return new Screenshot { PngData = data.ToArray(), Width = width, Height = height };
    }
}
