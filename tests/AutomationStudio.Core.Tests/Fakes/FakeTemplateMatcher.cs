using AutomationStudio.Abstractions;

namespace AutomationStudio.Core.Tests.Fakes;

public sealed class FakeTemplateMatcher : ITemplateMatcher
{
    public List<string> Calls { get; } = [];

    /// <summary>Result returned by every FindAsync call, regardless of the images passed in. Null (the default) means "no match".</summary>
    public TemplateMatchResult? Result { get; set; }

    public Task<TemplateMatchResult?> FindAsync(
        Screenshot screenshot, byte[] templatePngData, double minConfidence = 0.8,
        ScreenRect? searchRegion = null, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Find(region={searchRegion})");
        return Task.FromResult(Result);
    }
}
