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

    // Phase 9's "More Applications" plugins — a second planner instance with all of them configured,
    // used by the dispatch tests below. _planner (above) deliberately leaves them null, to keep
    // testing the "no plugin configured" failure path against a realistic planner shape.
    private readonly FakeObsPlugin _obs = new();
    private readonly FakePowerPointPlugin _powerPoint = new();
    private readonly FakeVlcPlugin _vlc = new();
    private readonly FakeChromePlugin _chrome = new();
    private readonly FakeWordPlugin _word = new();
    private readonly FakeExcelPlugin _excel = new();
    private readonly FakeFileManagerPlugin _fileManager = new();
    private readonly TaskPlanner _fullPlanner;

    public TaskPlannerTests()
    {
        var engine = new WorkflowExecutionEngine(
            _desktop, new FakeScreenshotService(), _windowManager, new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        _planner = new TaskPlanner(_plugin, engine, _launchRegistry, _workflowRepository);
        _fullPlanner = new TaskPlanner(
            _plugin, engine, _launchRegistry, _workflowRepository,
            _obs, _powerPoint, _vlc, _chrome, _word, _excel, _fileManager);
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
        _plugin.NextResult = PluginActionResult.Fail("VideoPsalm could not be focused.");

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
        var result = await _planner.ExecuteAsync(Intent.Simple("THIS_INTENT_DOES_NOT_EXIST"));

        Assert.False(result.Success);
        Assert.Contains("No task plan", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_KnownIntentWithNoConfiguredPlugin_FailsClearly_NotWithNoTaskPlanRegistered()
    {
        // START_RECORDING (and every OBS/PowerPoint/VLC/Chrome/Word/Excel/File Manager intent) IS
        // a registered task plan now (Phase 9) — it fails because no plugin instance was supplied to
        // this planner, a distinctly different, more specific failure than "unknown intent".
        var result = await _planner.ExecuteAsync(Intent.Simple("START_RECORDING"));

        Assert.False(result.Success);
        Assert.Contains("No OBS plugin is configured", result.Message);
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

    [Theory]
    [InlineData("START_RECORDING", "StartRecording")]
    [InlineData("STOP_RECORDING", "StopRecording")]
    [InlineData("START_STREAM", "StartStreaming")]
    [InlineData("STOP_STREAM", "StopStreaming")]
    public async Task ExecuteAsync_ObsIntents_DispatchToTheMatchingPluginMethod(string intentName, string expectedCall)
    {
        var result = await _fullPlanner.ExecuteAsync(Intent.Simple(intentName));

        Assert.True(result.Success, result.Message);
        Assert.Contains(expectedCall, _obs.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_SwitchScene_PassesTheSceneNameThrough()
    {
        var intent = new Intent("SWITCH_SCENE", new Dictionary<string, string> { ["scene"] = "Main Camera" });

        var result = await _fullPlanner.ExecuteAsync(intent);

        Assert.True(result.Success, result.Message);
        Assert.Contains("SwitchScene(Main Camera)", _obs.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_SwitchScene_MissingSceneName_FailsWithoutCallingThePlugin()
    {
        var result = await _fullPlanner.ExecuteAsync(Intent.Simple("SWITCH_SCENE"));

        Assert.False(result.Success);
        Assert.Empty(_obs.Calls);
    }

    [Theory]
    [InlineData("NEXT_SLIDE", "NextSlide")]
    [InlineData("PREVIOUS_SLIDE", "PreviousSlide")]
    [InlineData("START_PRESENTATION", "StartPresentation")]
    [InlineData("END_PRESENTATION", "EndPresentation")]
    public async Task ExecuteAsync_PowerPointIntents_DispatchToTheMatchingPluginMethod(string intentName, string expectedCall)
    {
        var result = await _fullPlanner.ExecuteAsync(Intent.Simple(intentName));

        Assert.True(result.Success, result.Message);
        Assert.Contains(expectedCall, _powerPoint.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_GoToSlide_PassesTheSlideNumberThrough()
    {
        var intent = new Intent("GO_TO_SLIDE", new Dictionary<string, string> { ["slideNumber"] = "5" });

        var result = await _fullPlanner.ExecuteAsync(intent);

        Assert.True(result.Success, result.Message);
        Assert.Contains("GoToSlide(5)", _powerPoint.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_GoToSlide_InvalidSlideNumber_FailsWithoutCallingThePlugin()
    {
        var intent = new Intent("GO_TO_SLIDE", new Dictionary<string, string> { ["slideNumber"] = "not-a-number" });

        var result = await _fullPlanner.ExecuteAsync(intent);

        Assert.False(result.Success);
        Assert.Empty(_powerPoint.Calls);
    }

    [Theory]
    [InlineData("PLAY_MEDIA", "Play")]
    [InlineData("PAUSE_MEDIA", "Pause")]
    [InlineData("STOP_MEDIA", "Stop")]
    [InlineData("NEXT_TRACK", "NextTrack")]
    [InlineData("PREVIOUS_TRACK", "PreviousTrack")]
    [InlineData("TOGGLE_MUTE", "ToggleMute")]
    [InlineData("TOGGLE_FULLSCREEN", "ToggleFullscreen")]
    public async Task ExecuteAsync_VlcIntents_DispatchToTheMatchingPluginMethod(string intentName, string expectedCall)
    {
        var result = await _fullPlanner.ExecuteAsync(Intent.Simple(intentName));

        Assert.True(result.Success, result.Message);
        Assert.Contains(expectedCall, _vlc.Calls);
    }

    [Theory]
    [InlineData("NEW_TAB", "NewTab")]
    [InlineData("CLOSE_TAB", "CloseTab")]
    [InlineData("RELOAD_PAGE", "Reload")]
    [InlineData("GO_BACK", "GoBack")]
    [InlineData("GO_FORWARD", "GoForward")]
    public async Task ExecuteAsync_ChromeIntents_DispatchToTheMatchingPluginMethod(string intentName, string expectedCall)
    {
        var result = await _fullPlanner.ExecuteAsync(Intent.Simple(intentName));

        Assert.True(result.Success, result.Message);
        Assert.Contains(expectedCall, _chrome.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_OpenUrl_PassesTheUrlThrough()
    {
        var intent = new Intent("OPEN_URL", new Dictionary<string, string> { ["url"] = "example.com" });

        var result = await _fullPlanner.ExecuteAsync(intent);

        Assert.True(result.Success, result.Message);
        Assert.Contains("OpenUrl(example.com)", _chrome.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_FindOnPage_PassesTheTextThrough()
    {
        var intent = new Intent("FIND_ON_PAGE", new Dictionary<string, string> { ["text"] = "settings" });

        var result = await _fullPlanner.ExecuteAsync(intent);

        Assert.True(result.Success, result.Message);
        Assert.Contains("FindOnPage(settings)", _chrome.Calls);
    }

    [Theory]
    [InlineData("NEW_FOLDER", "NewFolder")]
    [InlineData("COPY_FILE", "Copy")]
    [InlineData("CUT_FILE", "Cut")]
    [InlineData("PASTE_FILE", "Paste")]
    [InlineData("DELETE_FILE", "Delete")]
    public async Task ExecuteAsync_FileManagerIntents_DispatchToTheMatchingPluginMethod(string intentName, string expectedCall)
    {
        var result = await _fullPlanner.ExecuteAsync(Intent.Simple(intentName));

        Assert.True(result.Success, result.Message);
        Assert.Contains(expectedCall, _fileManager.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_RenameFile_PassesTheNewNameThrough()
    {
        var intent = new Intent("RENAME_FILE", new Dictionary<string, string> { ["name"] = "vacation photos" });

        var result = await _fullPlanner.ExecuteAsync(intent);

        Assert.True(result.Success, result.Message);
        Assert.Contains("Rename(vacation photos)", _fileManager.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_SearchFiles_PassesTheQueryThrough()
    {
        var intent = new Intent("SEARCH_FILES", new Dictionary<string, string> { ["query"] = "budget" });

        var result = await _fullPlanner.ExecuteAsync(intent);

        Assert.True(result.Success, result.Message);
        Assert.Contains("Search(budget)", _fileManager.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_SaveDocument_ForWord_DispatchesToWord()
    {
        var intent = new Intent("SAVE_DOCUMENT", new Dictionary<string, string> { ["application"] = "word" });

        var result = await _fullPlanner.ExecuteAsync(intent);

        Assert.True(result.Success, result.Message);
        Assert.Contains("Save", _word.Calls);
        Assert.Empty(_excel.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_SaveDocument_ForExcel_DispatchesToExcel()
    {
        var intent = new Intent("SAVE_DOCUMENT", new Dictionary<string, string> { ["application"] = "excel" });

        var result = await _fullPlanner.ExecuteAsync(intent);

        Assert.True(result.Success, result.Message);
        Assert.Contains("Save", _excel.Calls);
        Assert.Empty(_word.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_SaveDocument_WithNoOrUnknownApplication_FailsClearly()
    {
        var result = await _fullPlanner.ExecuteAsync(Intent.Simple("SAVE_DOCUMENT"));

        Assert.False(result.Success);
        Assert.Contains("which application", result.Message);
        Assert.Empty(_word.Calls);
        Assert.Empty(_excel.Calls);
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
