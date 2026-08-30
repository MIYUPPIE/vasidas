using AutomationStudio.Abstractions;

namespace AutomationStudio.Core.Tests.Fakes;

public sealed class FakeWindowManager : IWindowManager
{
    public List<string> Calls { get; } = [];
    public bool FocusByTitleResult { get; set; } = true;
    public bool FocusByProcessResult { get; set; } = true;

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
}
