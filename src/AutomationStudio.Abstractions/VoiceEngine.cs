namespace AutomationStudio.Abstractions;

/// <summary>Mono PCM audio normalized to [-1, 1] float samples — the shared currency between microphone capture and speech-to-text (DAS.md section 5's VoiceEngine).</summary>
public sealed class AudioSamples
{
    public required float[] Samples { get; init; }
    public int SampleRate { get; init; } = 16000;

    public TimeSpan Duration => TimeSpan.FromSeconds(Samples.Length / (double)SampleRate);
}

/// <summary>Local speech-to-text (DAS.md section 6) — implementations must work fully offline. The engine is swappable without touching anything above it.</summary>
public interface ISpeechToText
{
    Task<string> TranscribeAsync(AudioSamples audio, CancellationToken cancellationToken = default);
}

/// <summary>
/// Raw microphone input as a stream of PCM frames — no voice-activity detection or buffering here;
/// see <see cref="IVoiceActivityDetector"/> for turning this into utterance boundaries (DAS.md
/// section 5 keeps MicrophoneCapture and VoiceActivityDetection as separate VoiceEngine components).
/// </summary>
public interface IMicrophoneCapture : IDisposable
{
    int SampleRate { get; }

    /// <summary>Each frame is mono PCM, normalized to [-1, 1].</summary>
    IAsyncEnumerable<float[]> CaptureAsync(CancellationToken cancellationToken = default);
}

/// <summary>Decides where speech starts and ends within a stream of microphone frames (DAS.md section 5/6).</summary>
public interface IVoiceActivityDetector
{
    /// <summary>
    /// Consumes frames from <paramref name="frames"/> and returns the first complete utterance
    /// (concatenated samples from speech start to trailing silence). Returns null if
    /// <paramref name="timeout"/> elapses before any speech starts.
    /// </summary>
    Task<AudioSamples?> ListenForUtteranceAsync(
        IAsyncEnumerable<float[]> frames, int sampleRate, TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>A recognized voice command (DAS.md section 9) — e.g. Name="START_RECORDING" for "start recording", or Name="OPEN_APPLICATION" with Parameters["application"]="obs" for "open obs".</summary>
public sealed record Intent(string Name, IReadOnlyDictionary<string, string> Parameters)
{
    public static Intent Simple(string name) => new(name, new Dictionary<string, string>());

    public string? Parameter(string key) => Parameters.TryGetValue(key, out var value) ? value : null;
}

/// <summary>Turns normalized command text into a structured <see cref="Intent"/> (DAS.md section 9) — deterministic pattern matching, not an LLM; DAS.md section 10/37 Phase 10 is explicit that local AI only enters the picture once this deterministic layer is stable.</summary>
public interface IIntentRecognizer
{
    /// <summary>Returns null if the text doesn't match any known command. Async because recognizing some intents (e.g. a Bible reference) means validating against real data before committing to that interpretation — DAS.md section 16's "validate before automation," applied as early as possible.</summary>
    Task<Intent?> RecognizeAsync(string text, CancellationToken cancellationToken = default);
}
