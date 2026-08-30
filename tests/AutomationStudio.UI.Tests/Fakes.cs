using AutomationStudio.Abstractions;

namespace AutomationStudio.UI.Tests;

/// <summary>Minimal no-op fakes for constructing a real <see cref="AutomationStudio.Core.Execution.WorkflowExecutionEngine"/> in tests without touching real input devices.</summary>
internal sealed class FakeDesktopController : IDesktopController
{
    public List<string> Calls { get; } = [];

    public Task MoveMouseAsync(ScreenPoint position, CancellationToken cancellationToken = default) => Record("MoveMouse");
    public Task ClickAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default) => Record("Click");
    public Task DoubleClickAsync(ScreenPoint? position = null, CancellationToken cancellationToken = default) => Record("DoubleClick");
    public Task MouseDownAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default) => Record("MouseDown");
    public Task MouseUpAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default) => Record("MouseUp");
    public Task ScrollAsync(int deltaX, int deltaY, CancellationToken cancellationToken = default) => Record("Scroll");
    public Task TypeTextAsync(string text, CancellationToken cancellationToken = default) => Record("TypeText");
    public Task PressKeyAsync(Key key, CancellationToken cancellationToken = default) => Record("PressKey");
    public Task KeyCombinationAsync(IReadOnlyList<Key> keys, CancellationToken cancellationToken = default) => Record("KeyCombination");
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
    public Task<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WindowInfo>>([]);

    public Task<bool> FocusWindowByTitleAsync(string titleContains, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> FocusWindowByProcessAsync(string processName, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult<WindowInfo?>(null);
}

internal sealed class FakeCapabilityProvider : ICapabilityProvider
{
    public AutomationCapabilities DetectCapabilities() => new()
    {
        OperatingSystem = "TestOS",
        DesktopEnvironment = null,
        DisplayServer = DisplayServerKind.Unknown,
        MouseInjection = CapabilityState.Available,
        KeyboardInjection = CapabilityState.Available,
        ScreenCapture = CapabilityState.Available,
        WindowManagement = CapabilityState.Available,
        ElementFinding = CapabilityState.Available,
        Ocr = CapabilityState.Available,
        InputRecording = CapabilityState.Available,
        Notes = []
    };
}

internal sealed class FakeElementFinder : IElementFinder
{
    public List<string> Calls { get; } = [];

    /// <summary>Queued result for the next FindElementAsync call (by Text match). Null means "not found".</summary>
    public Dictionary<string, DetectedElement?> ElementsByText { get; } = [];

    public bool ThrowNotSupportedOnInvoke { get; set; }
    public bool ThrowNotSupportedOnSetText { get; set; }

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

    public Task SetTextAsync(DetectedElement element, string text, CancellationToken cancellationToken = default)
    {
        Calls.Add($"SetText({text})");
        if (ThrowNotSupportedOnSetText)
        {
            throw new ElementPatternNotSupportedException("Value", "test element");
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeOcrEngine : IOcrEngine
{
    public List<string> Calls { get; } = [];
    public List<OcrResult> Results { get; } = [];

    public Task<IReadOnlyList<OcrResult>> RecognizeAsync(Screenshot screenshot, OcrOptions? options = null, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Recognize(region={options?.Region})");
        return Task.FromResult<IReadOnlyList<OcrResult>>(Results);
    }
}
