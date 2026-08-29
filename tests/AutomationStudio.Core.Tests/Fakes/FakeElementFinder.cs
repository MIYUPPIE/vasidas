using AutomationStudio.Abstractions;

namespace AutomationStudio.Core.Tests.Fakes;

public sealed class FakeElementFinder : IElementFinder
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
