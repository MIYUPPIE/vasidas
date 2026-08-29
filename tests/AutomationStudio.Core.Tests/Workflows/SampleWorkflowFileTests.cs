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
    private static string PathTo(string fileName) => Path.Combine(AppContext.BaseDirectory, "workflows", fileName);

    [Fact]
    public async Task ObsRecordingSample_LoadsAndMatchesThePhaseOneActionSet()
    {
        var workflow = await WorkflowSerializer.LoadFromFileAsync(PathTo("obs-recording-linux.json"));

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

    /// <summary>Matches section 9's canonical JSON example, verbatim (FindText positions the mouse, a bare Click acts on it).</summary>
    [Fact]
    public async Task ObsRecordingOcrSample_LoadsAndMatchesSectionNinesShape()
    {
        var workflow = await WorkflowSerializer.LoadFromFileAsync(PathTo("obs-recording-ocr.json"));

        Assert.Equal("obs-recording-ocr", workflow.Id);
        Assert.True(workflow.StopOnFailure);

        Assert.Collection(
            workflow.Actions,
            a => Assert.IsType<OpenApplicationAction>(a),
            a => Assert.IsType<WaitAction>(a),
            a => Assert.IsType<FindTextAction>(a),
            a => Assert.IsType<ClickAction>(a),
            a => Assert.IsType<WaitAction>(a),
            a => Assert.IsType<FindTextAction>(a),
            a => Assert.IsType<ClickAction>(a),
            a => Assert.IsType<CloseApplicationAction>(a));

        var firstFind = Assert.IsType<FindTextAction>(workflow.Actions[2]);
        Assert.Equal("Start Recording", firstFind.Text);

        var firstClick = Assert.IsType<ClickAction>(workflow.Actions[3]);
        Assert.Null(firstClick.X);
        Assert.Null(firstClick.Y);
    }
}
