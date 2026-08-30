using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Core.Tests.Fakes;
using AutomationStudio.Core.Workflows;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Core.Tests.Execution;

public class OcrActionsTests
{
    private static OcrResult MakeResult(string text = "Start Recording", int x = 100, int y = 200, int width = 80, int height = 30) => new()
    {
        Text = text,
        Bounds = new ScreenRect(x, y, width, height),
        Confidence = 0.95
    };

    private static WorkflowExecutionEngine BuildEngine(FakeDesktopController desktop, FakeOcrEngine ocr) => new(
        desktop, new FakeScreenshotService(), new FakeWindowManager(), new FakeElementFinder(), ocr, new FakeTemplateMatcher(), NullLogger<WorkflowExecutionEngine>.Instance);

    [Fact]
    public async Task FindTextAction_WhenFound_MovesMouseToCenter_Succeeds()
    {
        var desktop = new FakeDesktopController();
        var ocr = new FakeOcrEngine();
        ocr.Results.Add(MakeResult(x: 100, y: 200, width: 80, height: 30)); // center = (140, 215)
        var engine = BuildEngine(desktop, ocr);
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new FindTextAction { Text = "Start Recording" }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Contains("MoveMouse(140,215)", desktop.Calls);
    }

    [Fact]
    public async Task FindTextAction_MatchesSubstring_CaseInsensitive()
    {
        var desktop = new FakeDesktopController();
        var ocr = new FakeOcrEngine();
        ocr.Results.Add(MakeResult(text: "start recording now"));
        var engine = BuildEngine(desktop, ocr);
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new FindTextAction { Text = "Start Recording" }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
    }

    [Fact]
    public async Task FindTextAction_WhenNeverFound_TimesOutRatherThanHanging()
    {
        var desktop = new FakeDesktopController();
        var ocr = new FakeOcrEngine(); // no results queued
        var engine = BuildEngine(desktop, ocr);
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new FindTextAction { Text = "Never Appears", TimeoutMilliseconds = 900 }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Timeout, result.ActionResults[0].Status);
        Assert.True(ocr.Calls.Count > 1, "expected the poll loop to retry OCR at least once before timing out");
    }

    [Fact]
    public async Task FindTextAction_PassesRegionAndMinConfidenceToTheEngine()
    {
        var desktop = new FakeDesktopController();
        var ocr = new FakeOcrEngine();
        ocr.Results.Add(MakeResult());
        var engine = BuildEngine(desktop, ocr);
        var region = new ScreenRect(10, 20, 300, 100);
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new FindTextAction { Text = "Start Recording", Region = region, MinConfidence = 0.7 }]
        };

        await engine.ExecuteAsync(workflow);

        var options = Assert.Single(ocr.OptionsUsed);
        Assert.Equal(region, options!.Region);
        Assert.Equal(0.7, options.MinConfidence);
    }

    [Fact]
    public async Task ClickTextAction_WhenFound_ClicksCenterWithConfiguredButton()
    {
        var desktop = new FakeDesktopController();
        var ocr = new FakeOcrEngine();
        ocr.Results.Add(MakeResult(x: 100, y: 200, width: 80, height: 30)); // center = (140, 215)
        var engine = BuildEngine(desktop, ocr);
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new ClickTextAction { Text = "Start Recording", Button = MouseButton.Right }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Contains("Click(Right,140,215)", desktop.Calls);
    }

    [Fact]
    public async Task VerifyTextAction_WhenFound_Succeeds_WithoutTouchingTheMouse()
    {
        var desktop = new FakeDesktopController();
        var ocr = new FakeOcrEngine();
        ocr.Results.Add(MakeResult());
        var engine = BuildEngine(desktop, ocr);
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new VerifyTextAction { Text = "Start Recording" }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Empty(desktop.Calls);
    }

    [Fact]
    public async Task VerifyTextAction_WhenNotFound_FailsRatherThanSilentlyPassing()
    {
        var desktop = new FakeDesktopController();
        var ocr = new FakeOcrEngine(); // no results queued
        var engine = BuildEngine(desktop, ocr);
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new VerifyTextAction { Text = "Missing", TimeoutMilliseconds = 200 }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Timeout, result.ActionResults[0].Status);
    }
}
