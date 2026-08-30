using AutomationStudio.Abstractions;
using AutomationStudio.Applications.Chrome;
using AutomationStudio.Applications.Excel;
using AutomationStudio.Applications.FileManager;
using AutomationStudio.Applications.Obs;
using AutomationStudio.Applications.PowerPoint;
using AutomationStudio.Applications.Vlc;
using AutomationStudio.Applications.VideoPsalm;
using AutomationStudio.Applications.Word;
using AutomationStudio.Storage;
using AutomationStudio.Storage.Repositories;

namespace AutomationStudio.Applications.Tests;

/// <summary>Records calls instead of running real workflows — used to test TaskPlanner's dispatch logic in isolation from each plugin's own (separately tested) internals.</summary>
internal sealed class FakeObsPlugin : IObsPlugin
{
    public List<string> Calls { get; } = [];
    public PluginActionResult NextResult { get; set; } = PluginActionResult.Ok();

    public Task<PluginActionResult> StartRecordingAsync(CancellationToken cancellationToken = default) => Record("StartRecording");
    public Task<PluginActionResult> StopRecordingAsync(CancellationToken cancellationToken = default) => Record("StopRecording");
    public Task<PluginActionResult> StartStreamingAsync(CancellationToken cancellationToken = default) => Record("StartStreaming");
    public Task<PluginActionResult> StopStreamingAsync(CancellationToken cancellationToken = default) => Record("StopStreaming");
    public Task<PluginActionResult> SwitchSceneAsync(string sceneName, CancellationToken cancellationToken = default) => Record($"SwitchScene({sceneName})");

    private Task<PluginActionResult> Record(string call)
    {
        Calls.Add(call);
        return Task.FromResult(NextResult);
    }
}

internal sealed class FakePowerPointPlugin : IPowerPointPlugin
{
    public List<string> Calls { get; } = [];
    public PluginActionResult NextResult { get; set; } = PluginActionResult.Ok();

    public Task<PluginActionResult> NextSlideAsync(CancellationToken cancellationToken = default) => Record("NextSlide");
    public Task<PluginActionResult> PreviousSlideAsync(CancellationToken cancellationToken = default) => Record("PreviousSlide");
    public Task<PluginActionResult> GoToSlideAsync(int slideNumber, CancellationToken cancellationToken = default) => Record($"GoToSlide({slideNumber})");
    public Task<PluginActionResult> StartPresentationAsync(CancellationToken cancellationToken = default) => Record("StartPresentation");
    public Task<PluginActionResult> EndPresentationAsync(CancellationToken cancellationToken = default) => Record("EndPresentation");

    private Task<PluginActionResult> Record(string call)
    {
        Calls.Add(call);
        return Task.FromResult(NextResult);
    }
}

internal sealed class FakeVlcPlugin : IVlcPlugin
{
    public List<string> Calls { get; } = [];
    public PluginActionResult NextResult { get; set; } = PluginActionResult.Ok();

    public Task<PluginActionResult> PlayAsync(CancellationToken cancellationToken = default) => Record("Play");
    public Task<PluginActionResult> PauseAsync(CancellationToken cancellationToken = default) => Record("Pause");
    public Task<PluginActionResult> StopAsync(CancellationToken cancellationToken = default) => Record("Stop");
    public Task<PluginActionResult> NextTrackAsync(CancellationToken cancellationToken = default) => Record("NextTrack");
    public Task<PluginActionResult> PreviousTrackAsync(CancellationToken cancellationToken = default) => Record("PreviousTrack");
    public Task<PluginActionResult> ToggleMuteAsync(CancellationToken cancellationToken = default) => Record("ToggleMute");
    public Task<PluginActionResult> ToggleFullscreenAsync(CancellationToken cancellationToken = default) => Record("ToggleFullscreen");

    private Task<PluginActionResult> Record(string call)
    {
        Calls.Add(call);
        return Task.FromResult(NextResult);
    }
}

internal sealed class FakeChromePlugin : IChromePlugin
{
    public List<string> Calls { get; } = [];
    public PluginActionResult NextResult { get; set; } = PluginActionResult.Ok();

    public Task<PluginActionResult> NewTabAsync(CancellationToken cancellationToken = default) => Record("NewTab");
    public Task<PluginActionResult> CloseTabAsync(CancellationToken cancellationToken = default) => Record("CloseTab");
    public Task<PluginActionResult> ReloadAsync(CancellationToken cancellationToken = default) => Record("Reload");
    public Task<PluginActionResult> GoBackAsync(CancellationToken cancellationToken = default) => Record("GoBack");
    public Task<PluginActionResult> GoForwardAsync(CancellationToken cancellationToken = default) => Record("GoForward");
    public Task<PluginActionResult> OpenUrlAsync(string url, CancellationToken cancellationToken = default) => Record($"OpenUrl({url})");
    public Task<PluginActionResult> FindOnPageAsync(string text, CancellationToken cancellationToken = default) => Record($"FindOnPage({text})");

    private Task<PluginActionResult> Record(string call)
    {
        Calls.Add(call);
        return Task.FromResult(NextResult);
    }
}

internal sealed class FakeWordPlugin : IWordPlugin
{
    public List<string> Calls { get; } = [];
    public PluginActionResult NextResult { get; set; } = PluginActionResult.Ok();

    public Task<PluginActionResult> NewDocumentAsync(CancellationToken cancellationToken = default) => Record("NewDocument");
    public Task<PluginActionResult> SaveAsync(CancellationToken cancellationToken = default) => Record("Save");
    public Task<PluginActionResult> PrintAsync(CancellationToken cancellationToken = default) => Record("Print");
    public Task<PluginActionResult> ToggleBoldAsync(CancellationToken cancellationToken = default) => Record("ToggleBold");
    public Task<PluginActionResult> ToggleItalicAsync(CancellationToken cancellationToken = default) => Record("ToggleItalic");
    public Task<PluginActionResult> ToggleUnderlineAsync(CancellationToken cancellationToken = default) => Record("ToggleUnderline");
    public Task<PluginActionResult> FindAsync(string text, CancellationToken cancellationToken = default) => Record($"Find({text})");

    private Task<PluginActionResult> Record(string call)
    {
        Calls.Add(call);
        return Task.FromResult(NextResult);
    }
}

internal sealed class FakeExcelPlugin : IExcelPlugin
{
    public List<string> Calls { get; } = [];
    public PluginActionResult NextResult { get; set; } = PluginActionResult.Ok();

    public Task<PluginActionResult> NewWorkbookAsync(CancellationToken cancellationToken = default) => Record("NewWorkbook");
    public Task<PluginActionResult> SaveAsync(CancellationToken cancellationToken = default) => Record("Save");
    public Task<PluginActionResult> GoToCellAsync(string cellReference, CancellationToken cancellationToken = default) => Record($"GoToCell({cellReference})");
    public Task<PluginActionResult> InsertAutoSumAsync(CancellationToken cancellationToken = default) => Record("InsertAutoSum");
    public Task<PluginActionResult> ToggleBoldAsync(CancellationToken cancellationToken = default) => Record("ToggleBold");
    public Task<PluginActionResult> FindAsync(string text, CancellationToken cancellationToken = default) => Record($"Find({text})");

    private Task<PluginActionResult> Record(string call)
    {
        Calls.Add(call);
        return Task.FromResult(NextResult);
    }
}

internal sealed class FakeFileManagerPlugin : IFileManagerPlugin
{
    public List<string> Calls { get; } = [];
    public PluginActionResult NextResult { get; set; } = PluginActionResult.Ok();

    public Task<PluginActionResult> NewFolderAsync(CancellationToken cancellationToken = default) => Record("NewFolder");
    public Task<PluginActionResult> CopyAsync(CancellationToken cancellationToken = default) => Record("Copy");
    public Task<PluginActionResult> CutAsync(CancellationToken cancellationToken = default) => Record("Cut");
    public Task<PluginActionResult> PasteAsync(CancellationToken cancellationToken = default) => Record("Paste");
    public Task<PluginActionResult> RenameAsync(string newName, CancellationToken cancellationToken = default) => Record($"Rename({newName})");
    public Task<PluginActionResult> DeleteAsync(CancellationToken cancellationToken = default) => Record("Delete");
    public Task<PluginActionResult> SearchAsync(string query, CancellationToken cancellationToken = default) => Record($"Search({query})");

    private Task<PluginActionResult> Record(string call)
    {
        Calls.Add(call);
        return Task.FromResult(NextResult);
    }
}

/// <summary>Records calls instead of running real workflows — used to test TaskPlanner's dispatch logic in isolation from VideoPsalmPlugin's own (separately tested) internals.</summary>
internal sealed class FakeVideoPsalmPlugin : IVideoPsalmPlugin
{
    public List<string> Calls { get; } = [];
    public PluginActionResult NextResult { get; set; } = PluginActionResult.Ok();

    public Task<PluginActionResult> DisplayBibleVerseAsync(BibleReference reference, CancellationToken cancellationToken = default) =>
        Record($"DisplayBibleVerse({reference})");

    public Task<PluginActionResult> NextVerseAsync(CancellationToken cancellationToken = default) => Record("NextVerse");
    public Task<PluginActionResult> PreviousVerseAsync(CancellationToken cancellationToken = default) => Record("PreviousVerse");

    public Task<PluginActionResult> DisplaySongAsync(string title, CancellationToken cancellationToken = default) =>
        Record($"DisplaySong({title})");

    public Task<PluginActionResult> NextSongAsync(CancellationToken cancellationToken = default) => Record("NextSong");
    public Task<PluginActionResult> PreviousSongAsync(CancellationToken cancellationToken = default) => Record("PreviousSong");
    public Task<PluginActionResult> ClearScreenAsync(CancellationToken cancellationToken = default) => Record("ClearScreen");
    public Task<PluginActionResult> HideOutputAsync(CancellationToken cancellationToken = default) => Record("HideOutput");

    private Task<PluginActionResult> Record(string call)
    {
        Calls.Add(call);
        return Task.FromResult(NextResult);
    }
}

internal sealed class FakeDesktopController : IDesktopController
{
    public List<string> Calls { get; } = [];

    private static string FormatPoint(ScreenPoint? position) => position is { } p ? $"{p.X},{p.Y}" : "null";

    public Task MoveMouseAsync(ScreenPoint position, CancellationToken cancellationToken = default)
    {
        Calls.Add($"MoveMouse({position.X},{position.Y})");
        return Task.CompletedTask;
    }

    public Task ClickAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Click({button},{FormatPoint(position)})");
        return Task.CompletedTask;
    }

    public Task DoubleClickAsync(ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        Calls.Add($"DoubleClick({FormatPoint(position)})");
        return Task.CompletedTask;
    }

    public Task MouseDownAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        Calls.Add($"MouseDown({button},{FormatPoint(position)})");
        return Task.CompletedTask;
    }

    public Task MouseUpAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        Calls.Add($"MouseUp({button},{FormatPoint(position)})");
        return Task.CompletedTask;
    }
    public Task ScrollAsync(int deltaX, int deltaY, CancellationToken cancellationToken = default) => Record("Scroll");
    public Task TypeTextAsync(string text, CancellationToken cancellationToken = default) { Calls.Add($"TypeText({text})"); return Task.CompletedTask; }
    public Task PressKeyAsync(Key key, CancellationToken cancellationToken = default) { Calls.Add($"PressKey({key})"); return Task.CompletedTask; }
    public Task KeyCombinationAsync(IReadOnlyList<Key> keys, CancellationToken cancellationToken = default) { Calls.Add($"KeyCombination({string.Join('+', keys)})"); return Task.CompletedTask; }
    public Task<int> OpenApplicationAsync(string path, string? arguments = null, CancellationToken cancellationToken = default) { Calls.Add("OpenApplication"); return Task.FromResult(1234); }
    public Task CloseApplicationAsync(string processName, CancellationToken cancellationToken = default) => Record("CloseApplication");
    public Task<bool> IsApplicationRunningAsync(string processName, CancellationToken cancellationToken = default) => Task.FromResult(true);

    private Task Record(string name)
    {
        Calls.Add(name);
        return Task.CompletedTask;
    }
}

internal sealed class FakeScreenshotService : IScreenshotService
{
    public Task<Screenshot> CaptureScreenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new Screenshot { PngData = [137, 80, 78, 71, 13, 10, 26, 10], Width = 1, Height = 1 });

    public Task<Screenshot> CaptureRegionAsync(ScreenRect region, CancellationToken cancellationToken = default) =>
        Task.FromResult(new Screenshot { PngData = [137, 80, 78, 71, 13, 10, 26, 10], Width = region.Width, Height = region.Height });
}

internal sealed class FakeWindowManager : IWindowManager
{
    public List<string> Calls { get; } = [];
    public bool FocusByProcessResult { get; set; } = true;

    public Task<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WindowInfo>>([]);

    public Task<bool> FocusWindowByTitleAsync(string titleContains, CancellationToken cancellationToken = default)
    {
        Calls.Add($"FocusByTitle({titleContains})");
        return Task.FromResult(true);
    }

    public Task<bool> FocusWindowByProcessAsync(string processName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"FocusByProcess({processName})");
        return Task.FromResult(FocusByProcessResult);
    }

    public WindowInfo? ActiveWindow { get; set; }

    public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(ActiveWindow);
}

internal sealed class FakeElementFinder : IElementFinder
{
    public List<string> Calls { get; } = [];

    /// <summary>Queued result for the next FindElementAsync call (by Text match). Empty by default — every text query returns "not found" unless configured.</summary>
    public Dictionary<string, DetectedElement?> ElementsByText { get; } = [];

    public bool ThrowNotSupportedOnInvoke { get; set; }

    public Task<DetectedElement?> FindElementAsync(ElementQuery query, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Find({query})");
        var found = query.Text is not null && ElementsByText.TryGetValue(query.Text, out var element) ? element : null;
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<DetectedElement>> FindAllElementsAsync(ElementQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DetectedElement>>([]);

    public Task InvokeAsync(DetectedElement element, CancellationToken cancellationToken = default)
    {
        Calls.Add("Invoke");
        if (ThrowNotSupportedOnInvoke)
        {
            throw new ElementPatternNotSupportedException("Invoke", "test element");
        }

        return Task.CompletedTask;
    }

    public Task SetTextAsync(DetectedElement element, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeOcrEngine : IOcrEngine
{
    /// <summary>Results returned by every RecognizeAsync call, regardless of the screenshot passed in. Empty by default.</summary>
    public List<OcrResult> Results { get; } = [];

    public Task<IReadOnlyList<OcrResult>> RecognizeAsync(Screenshot screenshot, OcrOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OcrResult>>(Results);
}

internal sealed class FakeTemplateMatcher : ITemplateMatcher
{
    public TemplateMatchResult? Result { get; set; }

    public Task<TemplateMatchResult?> FindAsync(
        Screenshot screenshot, byte[] templatePngData, double minConfidence = 0.8,
        ScreenRect? searchRegion = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result);
}

/// <summary>Yields no real frames — paired with FakeVoiceActivityDetector below, which never actually reads them.</summary>
internal sealed class FakeMicrophoneCapture : IMicrophoneCapture
{
    public int SampleRate => 16000;

    public async IAsyncEnumerable<float[]> CaptureAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public void Dispose() { }
}

/// <summary>Returns a queued canned "utterance" (or null) regardless of the real frame stream.</summary>
internal sealed class FakeVoiceActivityDetector : IVoiceActivityDetector
{
    public AudioSamples? NextUtterance { get; set; } = new AudioSamples { Samples = [0.1f], SampleRate = 16000 };

    public Task<AudioSamples?> ListenForUtteranceAsync(IAsyncEnumerable<float[]> frames, int sampleRate, TimeSpan timeout, CancellationToken cancellationToken = default) =>
        Task.FromResult(NextUtterance);
}

/// <summary>Returns a queued canned transcription regardless of the audio passed in.</summary>
internal sealed class FakeSpeechToText : ISpeechToText
{
    public string NextTranscription { get; set; } = "";

    public Task<string> TranscribeAsync(AudioSamples audio, CancellationToken cancellationToken = default) =>
        Task.FromResult(NextTranscription);
}

/// <summary>In-memory stand-in for IWorkflowRepository — used to test RUN_WORKFLOW dispatch and TeachingCoordinator without touching SQLite (that round trip is already covered live in AutomationStudio.Storage.Tests).</summary>
internal sealed class FakeWorkflowRepository : IWorkflowRepository
{
    public Dictionary<string, WorkflowRecord> RecordsById { get; } = [];
    public List<string> SavedIds { get; } = [];

    public Task<IReadOnlyList<WorkflowSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkflowSummary>>(RecordsById.Values
            .Select(r => new WorkflowSummary(r.Workflow.Id, r.Workflow.Name, r.Workflow.Description, r.Platform, r.Enabled, r.UpdatedAtUtc))
            .ToList());

    public Task<WorkflowRecord?> GetAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(RecordsById.TryGetValue(id, out var record) ? record : null);

    public Task SaveAsync(WorkflowRecord record, CancellationToken cancellationToken = default)
    {
        RecordsById[record.Workflow.Id] = record;
        SavedIds.Add(record.Workflow.Id);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        RecordsById.Remove(id);
        return Task.CompletedTask;
    }
}

/// <summary>Records Start/Stop calls instead of touching real input hardware — TeachingSession's own logic (normalization, focus-action prepending) is covered separately in AutomationStudio.Recorder.Tests.</summary>
internal sealed class FakeInputRecorder : IInputRecorder
{
    public bool Started { get; private set; }
    public IReadOnlyList<RecordedInputEvent> EventsToReturn { get; set; } = [];

    public void Start() => Started = true;

    public IReadOnlyList<RecordedInputEvent> Stop()
    {
        Started = false;
        return EventsToReturn;
    }

    public void Dispose()
    {
    }
}
