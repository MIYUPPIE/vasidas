using AutomationStudio.Abstractions;
using AutomationStudio.Vision.TemplateMatching;

namespace AutomationStudio.Vision.Tests;

/// <summary>Exercises real OpenCvSharp4 template matching against real rendered pixels — the live proof that the package choice documented in AutomationStudio.Vision.csproj actually works, not just that it compiles.</summary>
public class OpenCvTemplateMatcherTests
{
    private readonly OpenCvTemplateMatcher _matcher = new();

    [Fact]
    public async Task FindAsync_TemplatePastedAtAKnownPosition_LocatesItExactly()
    {
        var template = TestImages.RenderTemplate();
        var screenshot = TestImages.RenderWithPastedTemplate(template, 220, 150);

        var result = await _matcher.FindAsync(screenshot, template);

        Assert.NotNull(result);
        Assert.Equal(220, result!.Bounds.X);
        Assert.Equal(150, result.Bounds.Y);
        Assert.Equal(48, result.Bounds.Width);
        Assert.Equal(32, result.Bounds.Height);
        Assert.True(result.Confidence > 0.95, $"Expected high confidence, got {result.Confidence}");
    }

    [Fact]
    public async Task FindAsync_TemplateNotPresent_ReturnsNull()
    {
        var template = TestImages.RenderTemplate();
        var screenshot = TestImages.Render("nothing here", width: 400, height: 300);

        var result = await _matcher.FindAsync(screenshot, template, minConfidence: 0.9);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindAsync_ConfidenceBelowMinimum_ReturnsNull()
    {
        var template = TestImages.RenderTemplate();
        var screenshot = TestImages.Render("nothing here", width: 400, height: 300);

        // Even a poor match somewhere has *some* correlation score; requiring near-perfection should
        // still reject it.
        var result = await _matcher.FindAsync(screenshot, template, minConfidence: 0.999);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindAsync_SearchRegionContainingTheTemplate_FindsItWithCorrectAbsoluteCoordinates()
    {
        var template = TestImages.RenderTemplate();
        var screenshot = TestImages.RenderWithPastedTemplate(template, 220, 150);

        var result = await _matcher.FindAsync(screenshot, template, searchRegion: new ScreenRect(200, 130, 100, 80));

        Assert.NotNull(result);
        Assert.Equal(220, result!.Bounds.X);
        Assert.Equal(150, result.Bounds.Y);
    }

    [Fact]
    public async Task FindAsync_SearchRegionExcludingTheTemplate_ReturnsNull()
    {
        var template = TestImages.RenderTemplate();
        var screenshot = TestImages.RenderWithPastedTemplate(template, 220, 150);

        var result = await _matcher.FindAsync(screenshot, template, searchRegion: new ScreenRect(0, 0, 100, 80));

        Assert.Null(result);
    }

    [Fact]
    public async Task FindAsync_SearchRegionHangingOffTheScreenEdge_ClampsRatherThanThrows()
    {
        var template = TestImages.RenderTemplate();
        var screenshot = TestImages.RenderWithPastedTemplate(template, 220, 150, screenWidth: 300, screenHeight: 200);

        // Region extends past the 300x200 screenshot's bounds.
        var result = await _matcher.FindAsync(screenshot, template, searchRegion: new ScreenRect(200, 130, 500, 500));

        Assert.NotNull(result);
        Assert.Equal(220, result!.Bounds.X);
        Assert.Equal(150, result.Bounds.Y);
    }

    [Fact]
    public async Task FindAsync_TemplateLargerThanTheScreenshot_ReturnsNullRatherThanThrows()
    {
        var template = TestImages.RenderTemplate(width: 500, height: 500);
        var screenshot = TestImages.Render("small screenshot", width: 200, height: 150);

        var result = await _matcher.FindAsync(screenshot, template);

        Assert.Null(result);
    }
}
