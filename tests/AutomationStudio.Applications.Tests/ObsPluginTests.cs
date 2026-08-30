using AutomationStudio.Abstractions;
using AutomationStudio.Applications.Obs;
using AutomationStudio.Core.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Applications.Tests;

/// <summary>Exercises ObsPlugin's button-text-based automation (design doc section 12 resolution chain) — OBS ships with no default recording/streaming hotkeys (confirmed via research), which is why this plugin clicks controls by their on-screen text instead of sending a keyboard shortcut, unlike every other plugin in this project.</summary>
public class ObsPluginTests
{
    private readonly FakeDesktopController _desktop = new();
    private readonly FakeWindowManager _windowManager = new();
    private readonly FakeElementFinder _elementFinder = new();
    private readonly ObsPlugin _plugin;

    public ObsPluginTests()
    {
        var engine = new WorkflowExecutionEngine(
            _desktop, new FakeScreenshotService(), _windowManager, _elementFinder, new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        _plugin = new ObsPlugin(engine);
    }

    [Fact]
    public async Task StartRecordingAsync_FocusesObsThenClicksTheStartRecordingButton()
    {
        _elementFinder.ElementsByText["Start Recording"] = new DetectedElement { Bounds = new ScreenRect(10, 500, 120, 30) };

        var result = await _plugin.StartRecordingAsync();

        Assert.True(result.Success, result.Message);
        Assert.Contains("FocusByProcess(obs)", _windowManager.Calls);
        Assert.Contains("Invoke", _elementFinder.Calls);
    }

    [Fact]
    public async Task StopRecordingAsync_ClicksTheStopRecordingButton()
    {
        _elementFinder.ElementsByText["Stop Recording"] = new DetectedElement { Bounds = new ScreenRect(10, 500, 120, 30) };

        var result = await _plugin.StopRecordingAsync();

        Assert.True(result.Success, result.Message);
        Assert.Contains("Invoke", _elementFinder.Calls);
    }

    [Fact]
    public async Task StartStreamingAsync_ClicksTheStartStreamingButton()
    {
        _elementFinder.ElementsByText["Start Streaming"] = new DetectedElement { Bounds = new ScreenRect(10, 460, 120, 30) };

        var result = await _plugin.StartStreamingAsync();

        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public async Task StopStreamingAsync_ClicksTheStopStreamingButton()
    {
        _elementFinder.ElementsByText["Stop Streaming"] = new DetectedElement { Bounds = new ScreenRect(10, 460, 120, 30) };

        var result = await _plugin.StopStreamingAsync();

        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public async Task SwitchSceneAsync_ClicksTheSceneByName()
    {
        _elementFinder.ElementsByText["Main Camera"] = new DetectedElement { Bounds = new ScreenRect(5, 200, 150, 25) };

        var result = await _plugin.SwitchSceneAsync("Main Camera");

        Assert.True(result.Success, result.Message);
        Assert.Contains("Invoke", _elementFinder.Calls);
    }

    [Fact]
    public async Task SwitchSceneAsync_WithNoSceneName_FailsWithoutTouchingTheDesktop()
    {
        var result = await _plugin.SwitchSceneAsync("");

        Assert.False(result.Success);
        Assert.Empty(_desktop.Calls);
        Assert.Empty(_windowManager.Calls);
    }

    [Fact]
    public async Task StartRecordingAsync_WhenTheButtonCannotBeFound_FailsClearly()
    {
        // No ElementsByText/OCR results configured — every resolution tier misses.
        var result = await _plugin.StartRecordingAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task StartRecordingAsync_FallsBackToOcr_WhenNativeElementFindingMisses()
    {
        var ocr = new FakeOcrEngine();
        ocr.Results.Add(new OcrResult { Text = "Start Recording", Bounds = new ScreenRect(10, 500, 120, 30), Confidence = 0.9 });
        var engine = new WorkflowExecutionEngine(
            _desktop, new FakeScreenshotService(), _windowManager, _elementFinder, ocr, new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        var plugin = new ObsPlugin(engine);

        var result = await plugin.StartRecordingAsync();

        Assert.True(result.Success, result.Message);
        Assert.Contains("Click(Left,70,515)", _desktop.Calls); // center of (10,500,120,30)
    }
}
