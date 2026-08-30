using AutomationStudio.Abstractions;

namespace AutomationStudio.Ai.Tests;

/// <summary>Configurable stand-in for the real LLamaSharp engine — canned completions or a thrown exception, never real inference (that's <c>LlamaSharpEngineTests</c>' job, against the real model).</summary>
internal sealed class FakeLocalAiEngine : ILocalAiEngine
{
    public string NextCompletion { get; set; } = "";
    public Exception? ThrowOnComplete { get; set; }
    public List<(string SystemPrompt, string UserPrompt)> Calls { get; } = [];

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        Calls.Add((systemPrompt, userPrompt));
        return ThrowOnComplete is { } ex ? Throw(ex) : Task.FromResult(NextCompletion);
    }

    private static Task<string> Throw(Exception ex) => Task.FromException<string>(ex);
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

    public Task DoubleClickAsync(ScreenPoint? position = null, CancellationToken cancellationToken = default) => Record($"DoubleClick({FormatPoint(position)})");
    public Task MouseDownAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default) => Record($"MouseDown({button},{FormatPoint(position)})");
    public Task MouseUpAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default) => Record($"MouseUp({button},{FormatPoint(position)})");
    public Task ScrollAsync(int deltaX, int deltaY, CancellationToken cancellationToken = default) => Record("Scroll");
    public Task TypeTextAsync(string text, CancellationToken cancellationToken = default) => Record($"TypeText({text})");
    public Task PressKeyAsync(Key key, CancellationToken cancellationToken = default) => Record($"PressKey({key})");
    public Task KeyCombinationAsync(IReadOnlyList<Key> keys, CancellationToken cancellationToken = default) => Record($"KeyCombination({string.Join('+', keys)})");
    public Task<int> OpenApplicationAsync(string path, string? arguments = null, CancellationToken cancellationToken = default) { Calls.Add("OpenApplication"); return Task.FromResult(1234); }
    public Task CloseApplicationAsync(string processName, CancellationToken cancellationToken = default) => Record("CloseApplication");
    public Task<bool> IsApplicationRunningAsync(string processName, CancellationToken cancellationToken = default) => Task.FromResult(true);

    private Task Record(string call)
    {
        Calls.Add(call);
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
    public bool FocusByTitleResult { get; set; } = true;

    public Task<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WindowInfo>>([]);

    public Task<bool> FocusWindowByTitleAsync(string titleContains, CancellationToken cancellationToken = default)
    {
        Calls.Add($"FocusByTitle({titleContains})");
        return Task.FromResult(FocusByTitleResult);
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
        return Task.CompletedTask;
    }

    public Task SetTextAsync(DetectedElement element, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeOcrEngine : IOcrEngine
{
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
