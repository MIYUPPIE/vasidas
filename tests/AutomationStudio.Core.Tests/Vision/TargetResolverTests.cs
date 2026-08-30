using AutomationStudio.Abstractions;
using AutomationStudio.Core.Tests.Fakes;
using AutomationStudio.Core.Vision;

namespace AutomationStudio.Core.Tests.Vision;

/// <summary>Design doc section 12's Target Resolution Engine: pure orchestration over fakes, no real vision/automation backend needed to verify the priority order and fallback behavior.</summary>
public class TargetResolverTests
{
    private readonly FakeElementFinder _elementFinder = new();
    private readonly FakeOcrEngine _ocrEngine = new();
    private readonly FakeTemplateMatcher _templateMatcher = new();
    private readonly FakeScreenshotService _screenshotService = new();
    private readonly TargetResolver _resolver;

    public TargetResolverTests()
    {
        _resolver = new TargetResolver(_elementFinder, _ocrEngine, _templateMatcher, _screenshotService);
    }

    [Fact]
    public async Task ResolveAsync_ElementFound_ReturnsElementFinderStrategy_AndNeverCapturesAScreenshot()
    {
        var element = new DetectedElement { Bounds = new ScreenRect(10, 10, 50, 20), Confidence = 0.98 };
        _elementFinder.ElementsByText["Export"] = element;
        var target = new VisualTarget(Element: new ElementQuery(Text: "Export"), OcrText: "Export");

        var resolution = await _resolver.ResolveAsync(target);

        Assert.NotNull(resolution);
        Assert.Equal(TargetStrategy.ElementFinder, resolution!.Strategy);
        Assert.Equal(element, resolution.Element);
        Assert.Equal(new ScreenPoint(35, 20), resolution.Position); // element.Bounds.Center
        Assert.Equal(0, _screenshotService.CaptureCount); // OCR tier never needed to run.
    }

    [Fact]
    public async Task ResolveAsync_ElementNotFound_FallsBackToOcr()
    {
        _ocrEngine.Results.Add(new OcrResult { Text = "Export", Bounds = new ScreenRect(100, 100, 60, 20), Confidence = 0.9 });
        var target = new VisualTarget(Element: new ElementQuery(Text: "Export"), OcrText: "Export");

        var resolution = await _resolver.ResolveAsync(target);

        Assert.NotNull(resolution);
        Assert.Equal(TargetStrategy.Ocr, resolution!.Strategy);
        Assert.Equal(new ScreenPoint(130, 110), resolution.Position);
    }

    [Fact]
    public async Task ResolveAsync_ElementAndOcrBothMiss_FallsBackToTemplateMatching()
    {
        _templateMatcher.Result = new TemplateMatchResult(new ScreenRect(200, 200, 40, 30), 0.92);
        var target = new VisualTarget(Element: new ElementQuery(Text: "Export"), OcrText: "Export", TemplateImage: [1, 2, 3]);

        var resolution = await _resolver.ResolveAsync(target);

        Assert.NotNull(resolution);
        Assert.Equal(TargetStrategy.TemplateMatching, resolution!.Strategy);
        Assert.Equal(new ScreenPoint(220, 215), resolution.Position);
    }

    [Fact]
    public async Task ResolveAsync_EveryVisualTierMisses_FallsBackToCoordinates()
    {
        var target = new VisualTarget(
            Element: new ElementQuery(Text: "Export"), OcrText: "Export", TemplateImage: [1, 2, 3],
            Coordinates: new ScreenPoint(500, 400));

        var resolution = await _resolver.ResolveAsync(target);

        Assert.NotNull(resolution);
        Assert.Equal(TargetStrategy.Coordinates, resolution!.Strategy);
        Assert.Equal(new ScreenPoint(500, 400), resolution.Position);
        Assert.Equal(1.0, resolution.Confidence);
    }

    [Fact]
    public async Task ResolveAsync_NothingConfiguredResolves_ReturnsNull()
    {
        var target = new VisualTarget(Element: new ElementQuery(Text: "DoesNotExist"));

        var resolution = await _resolver.ResolveAsync(target);

        Assert.Null(resolution);
    }

    [Fact]
    public async Task ResolveAsync_OnlyCoordinatesConfigured_SkipsEveryVisualTierEntirely()
    {
        var target = new VisualTarget(Coordinates: new ScreenPoint(50, 60));

        var resolution = await _resolver.ResolveAsync(target);

        Assert.Equal(TargetStrategy.Coordinates, resolution!.Strategy);
        Assert.Empty(_elementFinder.Calls);
        Assert.Equal(0, _screenshotService.CaptureCount);
    }

    [Fact]
    public async Task ResolveAsync_BothOcrAndTemplateTiersConfigured_CapturesOnlyOneSharedScreenshot()
    {
        // Neither OCR nor template matching finds anything, so both tiers actually run — the
        // screenshot they share should still only be captured once.
        var target = new VisualTarget(OcrText: "Export", TemplateImage: [1, 2, 3]);

        await _resolver.ResolveAsync(target);

        Assert.Equal(1, _screenshotService.CaptureCount);
    }
}
