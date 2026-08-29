using AutomationStudio.Abstractions;
using AutomationStudio.Vision.OCR;

namespace AutomationStudio.Vision.Tests;

/// <summary>Engine-availability fallback (not a per-search cascade): once the primary fails, every later call in the same instance skips straight to the fallback.</summary>
public class CompositeOcrEngineTests
{
    private sealed class FakeOcrEngine(Func<Task<IReadOnlyList<OcrResult>>>? onRecognize = null) : IOcrEngine
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<OcrResult>> RecognizeAsync(Screenshot screenshot, OcrOptions? options = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return onRecognize?.Invoke() ?? Task.FromResult<IReadOnlyList<OcrResult>>([]);
        }
    }

    private static Screenshot MakeScreenshot() => new() { PngData = [137, 80, 78, 71], Width = 10, Height = 10 };

    [Fact]
    public async Task RecognizeAsync_WhenPrimarySucceeds_NeverCallsFallback()
    {
        var primary = new FakeOcrEngine();
        var fallback = new FakeOcrEngine();
        var composite = new CompositeOcrEngine(primary, fallback, isFallbackAvailable: () => true);

        await composite.RecognizeAsync(MakeScreenshot());

        Assert.Equal(1, primary.CallCount);
        Assert.Equal(0, fallback.CallCount);
    }

    [Fact]
    public async Task RecognizeAsync_WhenPrimaryThrows_FallsBackIfAvailable()
    {
        var primary = new FakeOcrEngine(() => throw new InvalidOperationException("model failed to load"));
        var fallback = new FakeOcrEngine();
        var composite = new CompositeOcrEngine(primary, fallback, isFallbackAvailable: () => true);

        var results = await composite.RecognizeAsync(MakeScreenshot());

        Assert.NotNull(results);
        Assert.Equal(1, fallback.CallCount);
    }

    [Fact]
    public async Task RecognizeAsync_AfterPrimaryFailsOnce_NeverRetriesPrimaryOnLaterCalls()
    {
        var primary = new FakeOcrEngine(() => throw new InvalidOperationException("model failed to load"));
        var fallback = new FakeOcrEngine();
        var composite = new CompositeOcrEngine(primary, fallback, isFallbackAvailable: () => true);

        await composite.RecognizeAsync(MakeScreenshot());
        await composite.RecognizeAsync(MakeScreenshot());
        await composite.RecognizeAsync(MakeScreenshot());

        Assert.Equal(1, primary.CallCount); // tried once, then never again
        Assert.Equal(3, fallback.CallCount);
    }

    [Fact]
    public async Task RecognizeAsync_WhenPrimaryThrowsAndFallbackUnavailable_ThrowsCapabilityUnavailable()
    {
        var primary = new FakeOcrEngine(() => throw new InvalidOperationException("model failed to load"));
        var fallback = new FakeOcrEngine();
        var composite = new CompositeOcrEngine(primary, fallback, isFallbackAvailable: () => false);

        await Assert.ThrowsAsync<CapabilityUnavailableException>(() => composite.RecognizeAsync(MakeScreenshot()));
        Assert.Equal(0, fallback.CallCount);
    }

    [Fact]
    public async Task RecognizeAsync_WhenPrimaryIsCancelled_PropagatesCancellation_RatherThanFallingBack()
    {
        using var cts = new CancellationTokenSource();
        var primary = new FakeOcrEngine(() => throw new OperationCanceledException(cts.Token));
        var fallback = new FakeOcrEngine();
        var composite = new CompositeOcrEngine(primary, fallback, isFallbackAvailable: () => true);

        await Assert.ThrowsAsync<OperationCanceledException>(() => composite.RecognizeAsync(MakeScreenshot(), cancellationToken: cts.Token));
        Assert.Equal(0, fallback.CallCount);
    }
}
