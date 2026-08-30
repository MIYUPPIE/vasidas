using AutomationStudio.Abstractions;

namespace AutomationStudio.Voice.Commands;

/// <summary>
/// Decides speech boundaries by simple RMS energy thresholding — the standard, well-established
/// technique for "when should I start/stop recording from a live mic stream" (distinct from
/// Whisper's own Silero-VAD-based silence *trimming* within an already-captured clip; this decides
/// the clip's boundaries in the first place).
/// </summary>
public sealed class EnergyVoiceActivityDetector : IVoiceActivityDetector
{
    /// <summary>RMS amplitude (0-1 scale) above which a frame counts as speech. Chosen well above typical room-noise floor but well below normal speaking volume.</summary>
    public double SpeechThreshold { get; init; } = 0.02;

    /// <summary>How long trailing silence must persist before an utterance is considered complete.</summary>
    public TimeSpan TrailingSilence { get; init; } = TimeSpan.FromMilliseconds(800);

    /// <summary>Hard cap on a single utterance's length, regardless of continued speech.</summary>
    public TimeSpan MaxUtteranceLength { get; init; } = TimeSpan.FromSeconds(15);

    public async Task<AudioSamples?> ListenForUtteranceAsync(
        IAsyncEnumerable<float[]> frames, int sampleRate, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        // `timeout` only bounds waiting for speech to start; once it has, MaxUtteranceLength (below)
        // is what bounds the recording — a long utterance must not be cut off by the start-timeout
        // just because it happened to begin near the end of that window. Checked between frames
        // (arriving every ~32ms at PvRecorder's default frame length) rather than via a second
        // cancellation token, which would need a second enumerator/MoveNextAsync race against this
        // same source — two overlapping reads on one async-iterator enumerator is unsafe.
        var deadline = DateTimeOffset.UtcNow + timeout;

        var buffer = new List<float>();
        var speechStarted = false;
        var silenceDuration = TimeSpan.Zero;
        var utteranceDuration = TimeSpan.Zero;

        await foreach (var frame in frames.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var frameDuration = TimeSpan.FromSeconds(frame.Length / (double)sampleRate);
            var isSpeech = RootMeanSquare(frame) >= SpeechThreshold;

            if (!speechStarted)
            {
                if (!isSpeech)
                {
                    if (DateTimeOffset.UtcNow >= deadline)
                    {
                        return null; // waited `timeout` with no speech detected.
                    }

                    continue;
                }

                speechStarted = true;
            }

            buffer.AddRange(frame);
            utteranceDuration += frameDuration;
            silenceDuration = isSpeech ? TimeSpan.Zero : silenceDuration + frameDuration;

            if (silenceDuration >= TrailingSilence || utteranceDuration >= MaxUtteranceLength)
            {
                break;
            }
        }

        if (!speechStarted || buffer.Count == 0)
        {
            return null;
        }

        return new AudioSamples { Samples = buffer.ToArray(), SampleRate = sampleRate };
    }

    private static double RootMeanSquare(float[] frame)
    {
        if (frame.Length == 0)
        {
            return 0;
        }

        double sumOfSquares = 0;
        foreach (var sample in frame)
        {
            sumOfSquares += (double)sample * sample;
        }

        return Math.Sqrt(sumOfSquares / frame.Length);
    }
}
