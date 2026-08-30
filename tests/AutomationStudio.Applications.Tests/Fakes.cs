using AutomationStudio.Abstractions;
using AutomationStudio.Applications.VideoPsalm;
using AutomationStudio.Storage;
using AutomationStudio.Storage.Repositories;

namespace AutomationStudio.Applications.Tests;

/// <summary>Records calls instead of running real workflows — used to test TaskPlanner's dispatch logic in isolation from VideoPsalmPlugin's own (separately tested) internals.</summary>
internal sealed class FakeVideoPsalmPlugin : IVideoPsalmPlugin
{
    public List<string> Calls { get; } = [];
    public VideoPsalmActionResult NextResult { get; set; } = VideoPsalmActionResult.Ok();

    public Task<VideoPsalmActionResult> DisplayBibleVerseAsync(BibleReference reference, CancellationToken cancellationToken = default) =>
        Record($"DisplayBibleVerse({reference})");

    public Task<VideoPsalmActionResult> NextVerseAsync(CancellationToken cancellationToken = default) => Record("NextVerse");
    public Task<VideoPsalmActionResult> PreviousVerseAsync(CancellationToken cancellationToken = default) => Record("PreviousVerse");

    public Task<VideoPsalmActionResult> DisplaySongAsync(string title, CancellationToken cancellationToken = default) =>
        Record($"DisplaySong({title})");

    public Task<VideoPsalmActionResult> NextSongAsync(CancellationToken cancellationToken = default) => Record("NextSong");
    public Task<VideoPsalmActionResult> PreviousSongAsync(CancellationToken cancellationToken = default) => Record("PreviousSong");
    public Task<VideoPsalmActionResult> ClearScreenAsync(CancellationToken cancellationToken = default) => Record("ClearScreen");
    public Task<VideoPsalmActionResult> HideOutputAsync(CancellationToken cancellationToken = default) => Record("HideOutput");

    private Task<VideoPsalmActionResult> Record(string call)
    {
        Calls.Add(call);
        return Task.FromResult(NextResult);
    }
}

internal sealed class FakeDesktopController : IDesktopController
{
    public List<string> Calls { get; } = [];

    public Task MoveMouseAsync(ScreenPoint position, CancellationToken cancellationToken = default) => Record("MoveMouse");
    public Task ClickAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default) => Record("Click");
    public Task DoubleClickAsync(ScreenPoint? position = null, CancellationToken cancellationToken = default) => Record("DoubleClick");
    public Task MouseDownAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default) => Record("MouseDown");
    public Task MouseUpAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default) => Record("MouseUp");
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
    public Task<DetectedElement?> FindElementAsync(ElementQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult<DetectedElement?>(null);

    public Task<IReadOnlyList<DetectedElement>> FindAllElementsAsync(ElementQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DetectedElement>>([]);

    public Task InvokeAsync(DetectedElement element, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetTextAsync(DetectedElement element, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeOcrEngine : IOcrEngine
{
    public Task<IReadOnlyList<OcrResult>> RecognizeAsync(Screenshot screenshot, OcrOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OcrResult>>([]);
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
