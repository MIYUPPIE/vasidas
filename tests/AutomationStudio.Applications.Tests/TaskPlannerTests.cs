using AutomationStudio.Abstractions;
using AutomationStudio.Applications.Planning;
using AutomationStudio.Applications.VideoPsalm;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Core.Workflows;
using AutomationStudio.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Applications.Tests;

/// <summary>DAS.md section 10: "Intent → Task Planner → Task Plan → Workflow Engine" — the intent never touches the mouse/keyboard directly. TaskPlanner's dispatch logic is tested against a fake plugin (VideoPsalmPlugin's own internals are covered separately); OPEN/CLOSE_APPLICATION exercise the real WorkflowExecutionEngine since those bypass the plugin layer entirely.</summary>
public class TaskPlannerTests
{
    private readonly FakeVideoPsalmPlugin _plugin = new();
    private readonly FakeDesktopController _desktop = new();
    private readonly FakeWindowManager _windowManager = new();
    private readonly ApplicationLaunchRegistry _launchRegistry = new();
    private readonly FakeWorkflowRepository _workflowRepository = new();
    private readonly TaskPlanner _planner;

    public TaskPlannerTests()
    {
        var engine = new WorkflowExecutionEngine(
            _desktop, new FakeScreenshotService(), _windowManager, new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        _planner = new TaskPlanner(_plugin, engine, _launchRegistry, _workflowRepository);
    }

    [Fact]
    public async Task ExecuteAsync_DisplayBibleVerse_DispatchesToThePluginWithTheParsedReference()
    {
        var intent = new Intent("DISPLAY_BIBLE_VERSE", new Dictionary<string, string>
        {
            ["book"] = "John", ["chapter"] = "3", ["startVerse"] = "16", ["endVerse"] = "16"
        });

        var result = await _planner.ExecuteAsync(intent);

        Assert.True(result.Success);
        Assert.Contains("DisplayBibleVerse(John 3:16)", _plugin.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_DisplayBibleVerse_MissingChapter_FailsWithoutCallingThePlugin()
    {
        var intent = new Intent("DISPLAY_BIBLE_VERSE", new Dictionary<string, string> { ["book"] = "John" });

        var result = await _planner.ExecuteAsync(intent);

        Assert.False(result.Success);
        Assert.Empty(_plugin.Calls);
    }

    [Theory]
    [InlineData("NEXT_SONG", "NextSong")]
    [InlineData("PREVIOUS_SONG", "PreviousSong")]
    [InlineData("CLEAR_SCREEN", "ClearScreen")]
    [InlineData("HIDE_OUTPUT", "HideOutput")]
    public async Task ExecuteAsync_SimpleIntents_DispatchToTheMatchingPluginMethod(string intentName, string expectedCall)
    {
        var result = await _planner.ExecuteAsync(Intent.Simple(intentName));

        Assert.True(result.Success);
        Assert.Contains(expectedCall, _plugin.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_DisplaySong_DispatchesWithTheTitle()
    {
        var intent = new Intent("DISPLAY_SONG", new Dictionary<string, string> { ["title"] = "Amazing Grace" });

        var result = await _planner.ExecuteAsync(intent);

        Assert.True(result.Success);
        Assert.Contains("DisplaySong(Amazing Grace)", _plugin.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_DisplaySong_MissingTitle_FailsWithoutCallingThePlugin()
    {
        var result = await _planner.ExecuteAsync(Intent.Simple("DISPLAY_SONG"));

        Assert.False(result.Success);
        Assert.Empty(_plugin.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenThePluginReportsFailure_PropagatesIt()
    {
        _plugin.NextResult = VideoPsalmActionResult.Fail("VideoPsalm could not be focused.");

        var result = await _planner.ExecuteAsync(Intent.Simple("CLEAR_SCREEN"));

        Assert.False(result.Success);
        Assert.Equal("VideoPsalm could not be focused.", result.Message);
    }

    [Theory]
    [InlineData("NEXT_VERSE")]
    [InlineData("PREVIOUS_VERSE")]
    public async Task ExecuteAsync_UnresolvedRelativeVerseIntent_FailsClearly(string intentName)
    {
        // These must be resolved to DISPLAY_BIBLE_VERSE upstream (IntentContextResolver) before
        // reaching the planner; if one arrives here unresolved, fail loudly rather than guess.
        var result = await _planner.ExecuteAsync(Intent.Simple(intentName));

        Assert.False(result.Success);
        Assert.Contains("resolved", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownIntent_FailsClearly()
    {
        var result = await _planner.ExecuteAsync(Intent.Simple("START_RECORDING"));

        Assert.False(result.Success);
        Assert.Contains("No task plan", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_OpenApplication_WithARegisteredPath_LaunchesIt()
    {
        _launchRegistry.Register("obs", "/usr/bin/obs");
        var intent = new Intent("OPEN_APPLICATION", new Dictionary<string, string> { ["application"] = "obs" });

        var result = await _planner.ExecuteAsync(intent);

        Assert.True(result.Success, result.Message);
        Assert.Contains("OpenApplication", _desktop.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_OpenApplication_WithNoRegisteredPath_FailsClearly()
    {
        var intent = new Intent("OPEN_APPLICATION", new Dictionary<string, string> { ["application"] = "some-unknown-app" });

        var result = await _planner.ExecuteAsync(intent);

        Assert.False(result.Success);
        Assert.Contains("No known launch path", result.Message);
        Assert.Empty(_desktop.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_CloseApplication_UsesTheApplicationNameAsTheProcessName()
    {
        var intent = new Intent("CLOSE_APPLICATION", new Dictionary<string, string> { ["application"] = "obs" });

        var result = await _planner.ExecuteAsync(intent);

        Assert.True(result.Success, result.Message);
        Assert.Contains("CloseApplication", _desktop.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_CloseApplication_WhenFocusFails_ReportsFailure()
    {
        // CloseApplicationAction doesn't need window focus at all, but exercising a real failure
        // path end to end (not just the plugin's) matters — swap in a fake desktop that always fails.
        var failingEngine = new WorkflowExecutionEngine(
            new AlwaysFailingDesktopController(), new FakeScreenshotService(), _windowManager, new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        var planner = new TaskPlanner(_plugin, failingEngine, _launchRegistry);
        var intent = new Intent("CLOSE_APPLICATION", new Dictionary<string, string> { ["application"] = "obs" });

        var result = await planner.ExecuteAsync(intent);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_RunWorkflow_ForAStoredWorkflow_ExecutesItThroughTheRealEngine()
    {
        _workflowRepository.RecordsById["StartChurchService"] = new WorkflowRecord
        {
            Workflow = new Workflow
            {
                Id = "StartChurchService",
                Name = "Start church service",
                Actions = [new CloseApplicationAction { ProcessName = "obs" }]
            },
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var intent = new Intent("RUN_WORKFLOW", new Dictionary<string, string> { ["workflowId"] = "StartChurchService" });

        var result = await _planner.ExecuteAsync(intent);

        Assert.True(result.Success, result.Message);
        Assert.Contains("CloseApplication", _desktop.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_RunWorkflow_ForAnUnknownId_FailsClearly()
    {
        var intent = new Intent("RUN_WORKFLOW", new Dictionary<string, string> { ["workflowId"] = "does-not-exist" });

        var result = await _planner.ExecuteAsync(intent);

        Assert.False(result.Success);
        Assert.Contains("does-not-exist", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_RunWorkflow_MissingWorkflowId_FailsClearly()
    {
        var result = await _planner.ExecuteAsync(Intent.Simple("RUN_WORKFLOW"));

        Assert.False(result.Success);
        Assert.Contains("workflowId", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_RunWorkflow_WithNoRepositoryConfigured_FailsClearly()
    {
        var engine = new WorkflowExecutionEngine(
            _desktop, new FakeScreenshotService(), _windowManager, new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        var planner = new TaskPlanner(_plugin, engine, _launchRegistry); // no workflowRepository
        var intent = new Intent("RUN_WORKFLOW", new Dictionary<string, string> { ["workflowId"] = "anything" });

        var result = await planner.ExecuteAsync(intent);

        Assert.False(result.Success);
        Assert.Contains("No workflow repository", result.Message);
    }

    private sealed class AlwaysFailingDesktopController : IDesktopController
    {
        public Task MoveMouseAsync(ScreenPoint position, CancellationToken cancellationToken = default) => Fail();
        public Task ClickAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default) => Fail();
        public Task DoubleClickAsync(ScreenPoint? position = null, CancellationToken cancellationToken = default) => Fail();
        public Task MouseDownAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default) => Fail();
        public Task MouseUpAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default) => Fail();
        public Task ScrollAsync(int deltaX, int deltaY, CancellationToken cancellationToken = default) => Fail();
        public Task TypeTextAsync(string text, CancellationToken cancellationToken = default) => Fail();
        public Task PressKeyAsync(Key key, CancellationToken cancellationToken = default) => Fail();
        public Task KeyCombinationAsync(IReadOnlyList<Key> keys, CancellationToken cancellationToken = default) => Fail();
        public Task<int> OpenApplicationAsync(string path, string? arguments = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("simulated failure");
        public Task CloseApplicationAsync(string processName, CancellationToken cancellationToken = default) => Fail();
        public Task<bool> IsApplicationRunningAsync(string processName, CancellationToken cancellationToken = default) => Task.FromResult(false);

        private static Task Fail() => throw new InvalidOperationException("simulated failure");
    }
}
