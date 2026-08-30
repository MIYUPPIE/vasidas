using AutomationStudio.Abstractions;

namespace AutomationStudio.Voice;

/// <summary>
/// Ties the VoiceEngine pieces together end to end (DAS.md section 5): microphone → voice activity
/// detection → speech-to-text → command normalization → intent recognition. This is Phase 3's
/// whole deliverable in one call; wiring it into always-on listening, a UI control, or actually
/// executing the resulting Intent is explicitly later phases (DAS.md section 37: Task Planner is
/// Phase 6) — an Intent here is data, not an instruction the caller has to act on immediately.
/// </summary>
public sealed class VoiceCommandListener(
    IMicrophoneCapture microphone, IVoiceActivityDetector voiceActivityDetector, ISpeechToText speechToText, IIntentRecognizer intentRecognizer)
{
    /// <summary>Waits up to <paramref name="timeout"/> for the user to start speaking, transcribes what they say, and recognizes it as an intent. Returns null if nothing was said in time or the words didn't match a known command.</summary>
    public async Task<Intent?> ListenAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var utterance = await voiceActivityDetector
            .ListenForUtteranceAsync(microphone.CaptureAsync(cancellationToken), microphone.SampleRate, timeout, cancellationToken)
            .ConfigureAwait(false);

        if (utterance is null)
        {
            return null;
        }

        var text = await speechToText.TranscribeAsync(utterance, cancellationToken).ConfigureAwait(false);
        return intentRecognizer.Recognize(text);
    }
}
