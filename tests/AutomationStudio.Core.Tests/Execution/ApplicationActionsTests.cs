using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Core.Tests.Fakes;
using AutomationStudio.Core.Workflows;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Core.Tests.Execution;

public class ApplicationActionsTests
{
    private static WorkflowExecutionEngine BuildEngine(FakeDesktopController desktop, FakeWindowManager windowManager) => new(
        desktop, new FakeScreenshotService(), windowManager, new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(), NullLogger<WorkflowExecutionEngine>.Instance);

    [Fact]
    public async Task FocusWindowAction_WhenFound_Succeeds()
    {
        var desktop = new FakeDesktopController();
        var windowManager = new FakeWindowManager { FocusByTitleResult = true };
        var engine = BuildEngine(desktop, windowManager);
        var workflow = new Workflow { Id = "wf", Name = "wf", Actions = [new FocusWindowAction { TitleContains = "VideoPsalm" }] };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.ActionResults[0].Status);
        Assert.Contains("FocusByTitle(VideoPsalm)", windowManager.Calls);
    }

    [Fact]
    public async Task FocusWindowAction_WhenNotFound_Fails()
    {
        var desktop = new FakeDesktopController();
        var windowManager = new FakeWindowManager { FocusByTitleResult = false };
        var engine = BuildEngine(desktop, windowManager);
        var workflow = new Workflow { Id = "wf", Name = "wf", Actions = [new FocusWindowAction { TitleContains = "Nonexistent" }] };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Failed, result.ActionResults[0].Status);
    }

    [Fact]
    public async Task FocusApplicationAction_WhenFound_Succeeds()
    {
        var desktop = new FakeDesktopController();
        var windowManager = new FakeWindowManager { FocusByProcessResult = true };
        var engine = BuildEngine(desktop, windowManager);
        var workflow = new Workflow { Id = "wf", Name = "wf", Actions = [new FocusApplicationAction { ProcessName = "videopsalm" }] };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.ActionResults[0].Status);
        Assert.Contains("FocusByProcess(videopsalm)", windowManager.Calls);
    }

    [Fact]
    public async Task FocusApplicationAction_WhenNotFound_Fails()
    {
        var desktop = new FakeDesktopController();
        var windowManager = new FakeWindowManager { FocusByProcessResult = false };
        var engine = BuildEngine(desktop, windowManager);
        var workflow = new Workflow { Id = "wf", Name = "wf", Actions = [new FocusApplicationAction { ProcessName = "nope" }] };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Failed, result.ActionResults[0].Status);
    }
}
