using AutomationStudio.Abstractions;

namespace AutomationStudio.Voice.Tests;

/// <summary>Yields no frames — VoiceCommandListenerTests never actually drives real audio through EnergyVoiceActivityDetector; FakeVoiceActivityDetector below stands in for it directly.</summary>
internal sealed class FakeMicrophoneCapture : IMicrophoneCapture
{
    public int SampleRate => 16000;

    public async IAsyncEnumerable<float[]> CaptureAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public void Dispose() { }
}

/// <summary>Returns a queued canned "utterance" (or null) regardless of the real frame stream — VoiceCommandListenerTests is about the recognition→context pipeline, not real audio.</summary>
internal sealed class FakeVoiceActivityDetector : IVoiceActivityDetector
{
    public AudioSamples? NextUtterance { get; set; } = new AudioSamples { Samples = [0.1f], SampleRate = 16000 };

    public Task<AudioSamples?> ListenForUtteranceAsync(IAsyncEnumerable<float[]> frames, int sampleRate, TimeSpan timeout, CancellationToken cancellationToken = default) =>
        Task.FromResult(NextUtterance);
}

/// <summary>Returns a queued canned transcription regardless of the audio passed in.</summary>
internal sealed class FakeSpeechToText : ISpeechToText
{
    public string NextTranscription { get; set; } = "";

    public Task<string> TranscribeAsync(AudioSamples audio, CancellationToken cancellationToken = default) =>
        Task.FromResult(NextTranscription);
}

/// <summary>Configurable stand-in for a deterministic recognizer — used to test the Tier-1/Tier-2 composition in <c>AiAugmentedIntentRecognizerTests</c> without depending on the real regex patterns.</summary>
internal sealed class FakeIntentRecognizer : IIntentRecognizer
{
    public Intent? NextIntent { get; set; }
    public int CallCount { get; private set; }

    public Task<Intent?> RecognizeAsync(string text, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(NextIntent);
    }
}

/// <summary>Configurable stand-in for the real LLamaSharp engine — canned completions, never real inference.</summary>
internal sealed class FakeLocalAiEngine : ILocalAiEngine
{
    public string NextCompletion { get; set; } = "";
    public int CallCount { get; private set; }

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(NextCompletion);
    }
}
