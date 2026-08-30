using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Core.Tests.Fakes;
using AutomationStudio.Core.Workflows;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Core.Tests.Execution;

/// <summary>ClickTarget/FindTarget/VerifyTarget: the unified action family built on the full design doc section 12 resolution chain, exercised through the real WorkflowExecutionEngine against fakes.</summary>
public class TargetActionsTests
{
    private static DetectedElement MakeElement(int x = 100, int y = 200, int width = 50, int height = 20) => new()
    {
        Text = "Export",
        Bounds = new ScreenRect(x, y, width, height),
        Type = ElementType.Button
    };

    private static (WorkflowExecutionEngine Engine, FakeDesktopController Desktop, FakeElementFinder ElementFinder, FakeOcrEngine Ocr, FakeTemplateMatcher TemplateMatcher) BuildEngine()
    {
        var desktop = new FakeDesktopController();
        var elementFinder = new FakeElementFinder();
        var ocr = new FakeOcrEngine();
        var templateMatcher = new FakeTemplateMatcher();
        var engine = new WorkflowExecutionEngine(
            desktop, new FakeScreenshotService(), new FakeWindowManager(), elementFinder, ocr, templateMatcher,
            NullLogger<WorkflowExecutionEngine>.Instance);
        return (engine, desktop, elementFinder, ocr, templateMatcher);
    }

    [Fact]
    public async Task FindTargetAction_ElementResolves_MovesMouseToItsCenter()
    {
        var (engine, desktop, elementFinder, _, _) = BuildEngine();
        elementFinder.ElementsByText["Export"] = MakeElement(); // center = (125, 210)
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new FindTargetAction { Target = new VisualTargetSpec { Text = "Export" } }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Contains("MoveMouse(125,210)", desktop.Calls);
    }

    [Fact]
    public async Task FindTargetAction_ElementMisses_FallsBackToOcr()
    {
        var (engine, desktop, elementFinder, ocr, _) = BuildEngine();
        // finder returns nothing for "Export"; OCR does.
        ocr.Results.Add(new OcrResult { Text = "Export", Bounds = new ScreenRect(300, 300, 60, 20), Confidence = 0.9 });
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new FindTargetAction { Target = new VisualTargetSpec { Text = "Export", OcrText = "Export" } }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Contains("MoveMouse(330,310)", desktop.Calls);
    }

    [Fact]
    public async Task FindTargetAction_NothingEverResolves_TimesOutRatherThanHanging()
    {
        var (engine, _, _, _, _) = BuildEngine();
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new FindTargetAction { Target = new VisualTargetSpec { Text = "Never Appears" }, TimeoutMilliseconds = 900 }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Timeout, result.ActionResults[0].Status);
    }

    [Fact]
    public async Task ClickTargetAction_ElementResolvesAndSupportsInvoke_UsesNativeInvoke_NotACoordinateClick()
    {
        var (engine, desktop, elementFinder, _, _) = BuildEngine();
        elementFinder.ElementsByText["Export"] = MakeElement();
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new ClickTargetAction { Target = new VisualTargetSpec { Text = "Export" } }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Contains("Invoke", elementFinder.Calls);
        Assert.Empty(desktop.Calls);
    }

    [Fact]
    public async Task ClickTargetAction_ElementResolvesButInvokeUnsupported_FallsBackToCoordinateClick()
    {
        var (engine, desktop, elementFinder, _, _) = BuildEngine();
        elementFinder.ElementsByText["Export"] = MakeElement(); // center = (125, 210)
        elementFinder.ThrowNotSupportedOnInvoke = true;
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new ClickTargetAction { Target = new VisualTargetSpec { Text = "Export" } }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Contains("Click(Left,125,210)", desktop.Calls);
    }

    [Fact]
    public async Task ClickTargetAction_ResolvedViaOcr_ClicksTheResolvedPositionWithTheConfiguredButton()
    {
        var (engine, desktop, _, ocr, _) = BuildEngine();
        ocr.Results.Add(new OcrResult { Text = "Export", Bounds = new ScreenRect(300, 300, 60, 20), Confidence = 0.9 });
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new ClickTargetAction { Target = new VisualTargetSpec { OcrText = "Export" }, Button = MouseButton.Right }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Contains("Click(Right,330,310)", desktop.Calls);
    }

    [Fact]
    public async Task ClickTargetAction_ResolvedViaTemplateMatching_ClicksTheResolvedPosition()
    {
        var (engine, desktop, _, _, templateMatcher) = BuildEngine();
        templateMatcher.Result = new TemplateMatchResult(new ScreenRect(400, 400, 40, 30), 0.95);
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, [137, 80, 78, 71]); // fake PNG bytes — never actually decoded, the matcher is faked.
            var workflow = new Workflow
            {
                Id = "wf", Name = "wf",
                Actions = [new ClickTargetAction { Target = new VisualTargetSpec { TemplateImagePath = tempFile } }]
            };

            var result = await engine.ExecuteAsync(workflow);

            Assert.Equal(ActionStatus.Success, result.Status);
            Assert.Contains("Click(Left,420,415)", desktop.Calls); // center of (400,400,40,30)
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ClickTargetAction_OnlyFallbackCoordinatesConfigured_ClicksThemDirectly()
    {
        var (engine, desktop, _, _, _) = BuildEngine();
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new ClickTargetAction { Target = new VisualTargetSpec { FallbackX = 742, FallbackY = 510 } }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Contains("Click(Left,742,510)", desktop.Calls);
    }

    [Fact]
    public async Task VerifyTargetAction_WhenResolved_Succeeds_WithoutTouchingTheMouse()
    {
        var (engine, desktop, elementFinder, _, _) = BuildEngine();
        elementFinder.ElementsByText["Export"] = MakeElement();
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new VerifyTargetAction { Target = new VisualTargetSpec { Text = "Export" } }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Empty(desktop.Calls);
    }

    [Fact]
    public async Task VerifyTargetAction_WhenNeverResolved_FailsRatherThanSilentlyPassing()
    {
        var (engine, _, _, _, _) = BuildEngine();
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new VerifyTargetAction { Target = new VisualTargetSpec { Text = "Missing" }, TimeoutMilliseconds = 200 }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Timeout, result.ActionResults[0].Status);
    }
}
