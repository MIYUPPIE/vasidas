using AutomationStudio.Abstractions;

namespace AutomationStudio.Recorder.Tests;

internal sealed class FakeInputRecorder : IInputRecorder
{
    public bool Started { get; private set; }
    public bool Stopped { get; private set; }
    public IReadOnlyList<RecordedInputEvent> EventsToReturn { get; set; } = [];

    public void Start()
    {
        if (Started && !Stopped)
        {
            throw new InvalidOperationException("Already recording.");
        }

        Started = true;
        Stopped = false;
    }

    public IReadOnlyList<RecordedInputEvent> Stop()
    {
        if (!Started || Stopped)
        {
            throw new InvalidOperationException("Not currently recording.");
        }

        Stopped = true;
        return EventsToReturn;
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeWindowManager : IWindowManager
{
    public WindowInfo? ActiveWindow { get; set; }

    public Task<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WindowInfo>>([]);

    public Task<bool> FocusWindowByTitleAsync(string titleContains, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> FocusWindowByProcessAsync(string processName, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(ActiveWindow);
}
