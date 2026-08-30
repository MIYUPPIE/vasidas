using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Core.Tests.Fakes;
using AutomationStudio.Core.Workflows;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Core.Tests.Execution;

public class ElementActionsTests
{
    private static DetectedElement MakeElement(int x = 100, int y = 200, int width = 50, int height = 20) => new()
    {
        Text = "Export",
        Bounds = new ScreenRect(x, y, width, height),
        Type = ElementType.Button
    };

    [Fact]
    public async Task FindElementAction_WhenFound_Succeeds()
    {
        var desktop = new FakeDesktopController();
        var finder = new FakeElementFinder { ElementsByText = { ["Export"] = MakeElement() } };
        var engine = new WorkflowExecutionEngine(
            desktop, new FakeScreenshotService(), new FakeWindowManager(), finder, new FakeOcrEngine(), new FakeTemplateMatcher(), NullLogger<WorkflowExecutionEngine>.Instance);
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new FindElementAction { Target = new ElementTarget { Text = "Export" } }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
    }

    [Fact]
    public async Task FindElementAction_WhenNotFound_Fails()
    {
        var desktop = new FakeDesktopController();
        var finder = new FakeElementFinder();
        var engine = new WorkflowExecutionEngine(
            desktop, new FakeScreenshotService(), new FakeWindowManager(), finder, new FakeOcrEngine(), new FakeTemplateMatcher(), NullLogger<WorkflowExecutionEngine>.Instance);
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new FindElementAction { Target = new ElementTarget { Text = "Missing" } }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Failed, result.Status);
    }

    [Fact]
    public async Task ClickElementAction_WhenInvokeSupported_UsesNativeInvoke_NotCoordinateClick()
    {
        var desktop = new FakeDesktopController();
        var finder = new FakeElementFinder { ElementsByText = { ["Export"] = MakeElement() } };
        var engine = new WorkflowExecutionEngine(
            desktop, new FakeScreenshotService(), new FakeWindowManager(), finder, new FakeOcrEngine(), new FakeTemplateMatcher(), NullLogger<WorkflowExecutionEngine>.Instance);
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new ClickElementAction { Target = new ElementTarget { Text = "Export" } }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Contains("Invoke", finder.Calls);
        Assert.Empty(desktop.Calls); // no coordinate fallback needed
    }

    [Fact]
    public async Task ClickElementAction_WhenInvokeNotSupported_FallsBackToCoordinateClick()
    {
        var desktop = new FakeDesktopController();
        var element = MakeElement(x: 100, y: 200, width: 50, height: 20); // center = (125, 210)
        var finder = new FakeElementFinder { ElementsByText = { ["Export"] = element }, ThrowNotSupportedOnInvoke = true };
        var engine = new WorkflowExecutionEngine(
            desktop, new FakeScreenshotService(), new FakeWindowManager(), finder, new FakeOcrEngine(), new FakeTemplateMatcher(), NullLogger<WorkflowExecutionEngine>.Instance);
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new ClickElementAction { Target = new ElementTarget { Text = "Export" } }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Contains("Click(Left,125,210)", desktop.Calls);
    }

    [Fact]
    public async Task TypeIntoElementAction_WhenSetTextSupported_UsesNativeValuePattern()
    {
        var desktop = new FakeDesktopController();
        var finder = new FakeElementFinder { ElementsByText = { ["Filename"] = MakeElement() } };
        var engine = new WorkflowExecutionEngine(
            desktop, new FakeScreenshotService(), new FakeWindowManager(), finder, new FakeOcrEngine(), new FakeTemplateMatcher(), NullLogger<WorkflowExecutionEngine>.Instance);
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new TypeIntoElementAction { Target = new ElementTarget { Text = "Filename" }, Text = "output.mp4" }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Contains("SetText(output.mp4)", finder.Calls);
        Assert.Empty(desktop.Calls);
    }

    [Fact]
    public async Task TypeIntoElementAction_WhenSetTextNotSupported_FallsBackToClickThenType()
    {
        var desktop = new FakeDesktopController();
        var element = MakeElement(x: 0, y: 0, width: 10, height: 10); // center = (5, 5)
        var finder = new FakeElementFinder { ElementsByText = { ["Filename"] = element }, ThrowNotSupportedOnSetText = true };
        var engine = new WorkflowExecutionEngine(
            desktop, new FakeScreenshotService(), new FakeWindowManager(), finder, new FakeOcrEngine(), new FakeTemplateMatcher(), NullLogger<WorkflowExecutionEngine>.Instance);
        var workflow = new Workflow
        {
            Id = "wf", Name = "wf",
            Actions = [new TypeIntoElementAction { Target = new ElementTarget { Text = "Filename" }, Text = "output.mp4" }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Equal(["Click(Left,5,5)", "TypeText(output.mp4)"], desktop.Calls);
    }
}
