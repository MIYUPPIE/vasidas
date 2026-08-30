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

    /// <summary>A small, visually distinct PNG (a solid circle on a contrasting background) to use as a template-matching target — real pixels, not a fixture file.</summary>
    public static byte[] RenderTemplate(int width = 48, int height = 32)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.DarkSlateGray);

        using var paint = new SKPaint { Color = SKColors.OrangeRed, IsAntialias = true };
        canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 2f - 2, paint);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>A "screenshot" with <paramref name="templatePng"/> pasted at exactly (<paramref name="x"/>, <paramref name="y"/>) — the ground truth a template-matching test asserts against.</summary>
    public static Screenshot RenderWithPastedTemplate(byte[] templatePng, int x, int y, int screenWidth = 400, int screenHeight = 300)
    {
        using var full = new SKBitmap(screenWidth, screenHeight);
        using var canvas = new SKCanvas(full);
        canvas.Clear(SKColors.LightGray);

        using var templateBitmap = SKBitmap.Decode(templatePng);
        canvas.DrawBitmap(templateBitmap, x, y);

        using var image = SKImage.FromBitmap(full);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return new Screenshot { PngData = data.ToArray(), Width = screenWidth, Height = screenHeight };
    }
}
