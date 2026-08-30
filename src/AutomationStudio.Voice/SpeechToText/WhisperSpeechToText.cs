using System.Text;
using AutomationStudio.Abstractions;
using Whisper.net;

namespace AutomationStudio.Voice.SpeechToText;

/// <summary>
/// Local offline speech-to-text via whisper.cpp bindings (DAS.md section 6) — confirmed live on
/// this dev machine: loads a ggml model and transcribes real audio end to end with no native-binary
/// issues. The model file itself is never committed to the repo (a model weight, not source —
/// downloaded once via <see cref="WhisperModelProvisioning"/>, matching section 29's "optional AI
/// models may be separately installed because of their size").
/// </summary>
public sealed class WhisperSpeechToText : ISpeechToText, IDisposable
{
    private readonly WhisperFactory _factory;
    private readonly string _language;

    public WhisperSpeechToText(string modelPath, string language = "en")
    {
        _factory = WhisperFactory.FromPath(modelPath);
        _language = language;
    }

    public async Task<string> TranscribeAsync(AudioSamples audio, CancellationToken cancellationToken = default)
    {
        using var processor = _factory.CreateBuilder().WithLanguage(_language).Build();
        var samples = audio.SampleRate == 16000 ? audio.Samples : Resample(audio.Samples, audio.SampleRate, 16000);

        var text = new StringBuilder();
        await foreach (var segment in processor.ProcessAsync(samples, cancellationToken).ConfigureAwait(false))
        {
            if (text.Length > 0)
            {
                text.Append(' ');
            }

            text.Append(segment.Text.Trim());
        }

        return text.ToString().Trim();
    }

    /// <summary>Naive linear resample — only exercised if a future <see cref="IMicrophoneCapture"/> implementation doesn't already provide 16kHz (PvRecorder does, so this is a defensive fallback, not the common path).</summary>
    private static float[] Resample(float[] samples, int fromRate, int toRate)
    {
        if (fromRate == toRate || samples.Length == 0)
        {
            return samples;
        }

        var ratio = (double)toRate / fromRate;
        var outputLength = Math.Max(1, (int)(samples.Length * ratio));
        var output = new float[outputLength];
        for (var i = 0; i < outputLength; i++)
        {
            var sourceIndex = i / ratio;
            var sourceIndexFloor = (int)sourceIndex;
            var fraction = sourceIndex - sourceIndexFloor;
            var a = samples[Math.Min(sourceIndexFloor, samples.Length - 1)];
            var b = samples[Math.Min(sourceIndexFloor + 1, samples.Length - 1)];
            output[i] = (float)(a + (b - a) * fraction);
        }

        return output;
    }

    public void Dispose() => _factory.Dispose();
}
