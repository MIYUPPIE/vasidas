using AutomationStudio.Abstractions;
using AutomationStudio.Vision.OCR;

namespace AutomationStudio.Vision.Tests;

/// <summary>
/// Exercises the real ONNX-backed OCR pipeline against real rendered pixels — no fake recognizer.
/// Confirmed live before writing this class that RapidOcrNet (not Sdcb.PaddleSharp) is the working
/// choice on Linux; see <c>AutomationStudio.Vision.csproj</c>'s comment for why.
/// </summary>
public class RapidOcrEngineTests : IDisposable
{
    private readonly RapidOcrEngine _engine = new();

    [Fact]
    public async Task RecognizeAsync_FindsRealTextInARenderedScreenshot()
    {
        var screenshot = TestImages.Render("Start Recording");

        var results = await _engine.RecognizeAsync(screenshot);

        var match = Assert.Single(results);
        Assert.Contains("Start Recording", match.Text, StringComparison.OrdinalIgnoreCase);
        Assert.True(match.Confidence > 0.5, $"expected reasonable confidence, got {match.Confidence}");
        Assert.True(match.Bounds.Width > 0 && match.Bounds.Height > 0);
    }

    [Fact]
    public async Task RecognizeAsync_OnABlankImage_FindsNothing()
    {
        var screenshot = TestImages.Render(""); // blank white canvas, no text drawn

        var results = await _engine.RecognizeAsync(screenshot);

        Assert.Empty(results);
    }

    [Fact]
    public async Task RecognizeAsync_RespectsMinConfidence_ByReturningNothingAboveOne()
    {
        var screenshot = TestImages.Render("Start Recording");

        // No real recognition scores 100%; a threshold of 1.01 must filter everything out without throwing.
        var results = await _engine.RecognizeAsync(screenshot, new OcrOptions { MinConfidence = 1.01 });

        Assert.Empty(results);
    }

    [Fact]
    public async Task RecognizeAsync_WithARegion_FindsTextAndReturnsFullScreenshotCoordinates()
    {
        // Draw "Start Recording" inside a larger 800x600 canvas, offset at (50, 400), then search
        // only the region containing it — bounds returned must be in full-image coordinates, not
        // region-relative ones (otherwise a caller clicking match.Bounds.Center would click the
        // wrong place on the real screen).
        var full = RenderInLargerCanvas("Start Recording", textX: 50, textY: 400, canvasWidth: 800, canvasHeight: 600);
        var region = new ScreenRect(0, 350, 400, 120);

        var results = await _engine.RecognizeAsync(full, new OcrOptions { Region = region });

        var match = Assert.Single(results);
        Assert.Contains("Start Recording", match.Text, StringComparison.OrdinalIgnoreCase);
        Assert.True(match.Bounds.Y > 350, $"expected a Y coordinate translated back into the full image (>350), got {match.Bounds.Y}");
    }

    private static Screenshot RenderInLargerCanvas(string text, int textX, int textY, int canvasWidth, int canvasHeight)
    {
        using var bitmap = new SkiaSharp.SKBitmap(canvasWidth, canvasHeight);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.White);
        using var font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.FromFamilyName("DejaVu Sans", SkiaSharp.SKFontStyle.Bold), 32);
        using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Black, IsAntialias = true };
        canvas.DrawText(text, textX, textY, font, paint);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return new Screenshot { PngData = data.ToArray(), Width = canvasWidth, Height = canvasHeight };
    }

    public void Dispose() => _engine.Dispose();
}
