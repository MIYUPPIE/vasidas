using AutomationStudio.Abstractions;

namespace AutomationStudio.Applications.Tests;

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
