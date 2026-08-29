using AutomationStudio.Abstractions;

namespace AutomationStudio.Core.Tests.Fakes;

public sealed class FakeOcrEngine : IOcrEngine
{
    public List<string> Calls { get; } = [];
    public List<OcrOptions?> OptionsUsed { get; } = [];

    /// <summary>Results returned by every RecognizeAsync call, regardless of the screenshot passed in.</summary>
    public List<OcrResult> Results { get; } = [];

    public Task<IReadOnlyList<OcrResult>> RecognizeAsync(Screenshot screenshot, OcrOptions? options = null, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Recognize(region={options?.Region})");
        OptionsUsed.Add(options);
        return Task.FromResult<IReadOnlyList<OcrResult>>(Results);
    }
}
