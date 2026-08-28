using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Workflows;

namespace AutomationStudio.Core.Tests.Workflows;

/// <summary>
/// Loads the actual sample workflow shipped under /workflows so the file the repo hands users as
/// a worked example is guaranteed to stay valid — if a future change to the action schema breaks
/// it, this fails loudly instead of the file silently rotting.
/// </summary>
public class SampleWorkflowFileTests
{
    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "workflows", "obs-recording-linux.json");

    [Fact]
    public async Task ObsRecordingSample_LoadsAndMatchesThePhaseOneActionSet()
    {
        var workflow = await WorkflowSerializer.LoadFromFileAsync(SamplePath);

        Assert.Equal("obs-recording-linux", workflow.Id);
        Assert.True(workflow.StopOnFailure);

        Assert.Collection(
            workflow.Actions,
            a => Assert.IsType<OpenApplicationAction>(a),
            a => Assert.IsType<WaitAction>(a),
            a => Assert.IsType<ClickAction>(a),
            a => Assert.IsType<WaitAction>(a),
            a => Assert.IsType<TakeScreenshotAction>(a),
            a => Assert.IsType<ClickAction>(a),
            a => Assert.IsType<CloseApplicationAction>(a));

        var openApp = Assert.IsType<OpenApplicationAction>(workflow.Actions[0]);
        Assert.Equal("obs", openApp.Path);

        var closeApp = Assert.IsType<CloseApplicationAction>(workflow.Actions[^1]);
        Assert.Equal("obs", closeApp.ProcessName);
    }
}
