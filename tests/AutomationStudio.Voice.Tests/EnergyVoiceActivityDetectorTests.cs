using System.Runtime.CompilerServices;
using AutomationStudio.Voice.Commands;

namespace AutomationStudio.Voice.Tests;

public class EnergyVoiceActivityDetectorTests
{
    private const int SampleRate = 16000;
    private const int FrameLength = 512; // ~32ms per frame at 16kHz, matching PvRecorder's default.

    private static float[] SilentFrame() => new float[FrameLength];

    private static float[] SpeechFrame()
    {
        var frame = new float[FrameLength];
        for (var i = 0; i < frame.Length; i++)
        {
            frame[i] = i % 2 == 0 ? 0.5f : -0.5f; // well above the default 0.02 RMS threshold.
        }

        return frame;
    }

    private static async IAsyncEnumerable<float[]> ToAsyncFrames(
        IEnumerable<float[]> frames, TimeSpan? delayPerFrame = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var frame in frames)
        {
            if (delayPerFrame is { } delay)
            {
                await Task.Delay(delay, cancellationToken);
            }

            yield return frame;
        }

        // Real microphone streams never end on their own; block "forever" (until the caller stops
        // enumerating, via cancellation or disposal) rather than letting the async iterator complete.
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    [Fact]
    public async Task ListenForUtteranceAsync_WithNoSpeech_ReturnsNullAfterTimeout()
    {
        var detector = new EnergyVoiceActivityDetector();
        // A real per-frame delay matters here: with none, all 200 frames would be consumed in
        // microseconds — long before the 150ms deadline could ever elapse — running the source dry
        // and hanging on ToAsyncFrames' "block forever" tail with nothing left to cancel it (that
        // tail exists to simulate a real mic stream, which never ends on its own; a *test* still
        // needs enough real time to pass for the deadline check between frames to actually trip).
        var frames = ToAsyncFrames(Enumerable.Repeat(SilentFrame(), 200), delayPerFrame: TimeSpan.FromMilliseconds(5));

        var result = await detector.ListenForUtteranceAsync(frames, SampleRate, TimeSpan.FromMilliseconds(150));

        Assert.Null(result);
    }

    [Fact]
    public async Task ListenForUtteranceAsync_CapturesSpeechUntilTrailingSilence()
    {
        var detector = new EnergyVoiceActivityDetector { TrailingSilence = TimeSpan.FromMilliseconds(100) };
        // ~4 silent frames (~128ms) of trailing silence after 3 speech frames is enough to end the utterance.
        var frames = ToAsyncFrames([SpeechFrame(), SpeechFrame(), SpeechFrame(), SilentFrame(), SilentFrame(), SilentFrame(), SilentFrame()]);

        var result = await detector.ListenForUtteranceAsync(frames, SampleRate, TimeSpan.FromSeconds(5));

        Assert.NotNull(result);
        // 3 speech frames + however many silent frames were consumed before the trailing-silence threshold tripped.
        Assert.True(result!.Samples.Length >= FrameLength * 3);
        Assert.Equal(SampleRate, result.SampleRate);
    }

    [Fact]
    public async Task ListenForUtteranceAsync_StopsAtMaxUtteranceLength_EvenIfSpeechContinues()
    {
        var detector = new EnergyVoiceActivityDetector
        {
            MaxUtteranceLength = TimeSpan.FromMilliseconds(64), // 2 frames' worth
            TrailingSilence = TimeSpan.FromSeconds(10) // long enough that this isn't what stops it
        };
        var frames = ToAsyncFrames(Enumerable.Repeat(SpeechFrame(), 1000)); // continuous speech, never stops on its own

        var result = await detector.ListenForUtteranceAsync(frames, SampleRate, TimeSpan.FromSeconds(5));

        Assert.NotNull(result);
        Assert.True(result!.Duration <= TimeSpan.FromMilliseconds(96), $"expected the max-length cap to apply, got {result.Duration}");
    }

    [Fact]
    public async Task ListenForUtteranceAsync_WhenSpeechStartsNearTheEndOfTheStartTimeout_IsNotTruncatedByIt()
    {
        // Regression: the start-timeout must only bound *waiting* for speech, not the recording
        // itself — speech beginning just before the timeout window closes must still be captured
        // in full, not cut short because it happened to start late.
        var detector = new EnergyVoiceActivityDetector
        {
            TrailingSilence = TimeSpan.FromMilliseconds(50),
            MaxUtteranceLength = TimeSpan.FromSeconds(10)
        };
        var silentFrames = Enumerable.Repeat(SilentFrame(), 6); // ~192ms of silence before speech starts
        var speechThenSilence = new[] { SpeechFrame(), SpeechFrame(), SpeechFrame(), SpeechFrame(), SilentFrame(), SilentFrame() };
        var frames = ToAsyncFrames(silentFrames.Concat(speechThenSilence), delayPerFrame: TimeSpan.FromMilliseconds(30));

        // Start-timeout (250ms) is shorter than how long it takes to finish speaking (silence 180ms + speech 120ms = 300ms+),
        // but speech starts at ~180ms, well within the 250ms window — it must be allowed to complete.
        var result = await detector.ListenForUtteranceAsync(frames, SampleRate, TimeSpan.FromMilliseconds(250));

        Assert.NotNull(result);
        Assert.True(result!.Samples.Length >= FrameLength * 4, $"expected all 4 speech frames captured, got {result.Samples.Length / FrameLength} frames' worth");
    }

    [Fact]
    public async Task ListenForUtteranceAsync_RespectsExternalCancellation()
    {
        var detector = new EnergyVoiceActivityDetector();
        var frames = ToAsyncFrames(Enumerable.Repeat(SilentFrame(), 10000));
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => detector.ListenForUtteranceAsync(frames, SampleRate, TimeSpan.FromSeconds(30), cts.Token));
    }
}
