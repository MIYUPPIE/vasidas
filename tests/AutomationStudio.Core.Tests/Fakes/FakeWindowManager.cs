using AutomationStudio.Abstractions;

namespace AutomationStudio.Core.Tests.Fakes;

public sealed class FakeWindowManager : IWindowManager
{
    public Task<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WindowInfo>>([]);

    public Task<bool> FocusWindowByTitleAsync(string titleContains, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<bool> FocusWindowByProcessAsync(string processName, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
