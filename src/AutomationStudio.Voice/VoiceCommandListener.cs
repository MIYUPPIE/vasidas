using AutomationStudio.Abstractions;
using AutomationStudio.Voice.Context;

namespace AutomationStudio.Voice;

/// <summary>
/// Ties the VoiceEngine pieces together end to end (DAS.md section 5): microphone → voice activity
/// detection → speech-to-text → command normalization → intent recognition → context resolution
/// (DAS.md section 8/31 — "next verse" only means something once resolved against what's currently
/// displayed). This is Phase 3+5's whole deliverable in one call; wiring it into always-on
/// listening, a UI control, or actually executing the resulting Intent is explicitly later phases
/// (DAS.md section 37: Task Planner is Phase 6) — an Intent here is data, not an instruction the
/// caller has to act on immediately.
/// </summary>
public sealed class VoiceCommandListener(
    IMicrophoneCapture microphone,
    IVoiceActivityDetector voiceActivityDetector,
    ISpeechToText speechToText,
    IIntentRecognizer intentRecognizer,
    IntentContextResolver contextResolver)
{
    /// <summary>Waits up to <paramref name="timeout"/> for the user to start speaking and returns exactly what they said, with no intent recognition — used by the "teach new command" dialogue (DAS.md section 25) while it's expecting a command *name*, which must never be matched against a known command pattern.</summary>
    public async Task<string?> TranscribeAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var utterance = await voiceActivityDetector
            .ListenForUtteranceAsync(microphone.CaptureAsync(cancellationToken), microphone.SampleRate, timeout, cancellationToken)
            .ConfigureAwait(false);

        return utterance is null ? null : await speechToText.TranscribeAsync(utterance, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Waits up to <paramref name="timeout"/> for the user to start speaking, transcribes what they say, recognizes it as an intent, and resolves it against current context. Returns null if nothing was said in time, the words didn't match a known command, or a context-dependent command (e.g. "next verse") had nothing to resolve against.</summary>
    public async Task<Intent?> ListenAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var result = await ListenWithTextAsync(timeout, cancellationToken).ConfigureAwait(false);
        return result?.Intent;
    }

    /// <summary>Same pipeline as <see cref="ListenAsync"/>, but also hands back the raw transcribed text alongside whatever intent (if any) was recognized from it. Callers that only need the intent should keep using <see cref="ListenAsync"/>; this exists for a Tier-3 AI fallback (DAS.md section 23) that needs the original words when no known intent matched at all — returns null only when nothing was said in time, never merely because recognition failed.</summary>
    public async Task<VoiceCommandResult?> ListenWithTextAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var text = await TranscribeAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (text is null)
        {
            return null;
        }

        var intent = await intentRecognizer.RecognizeAsync(text, cancellationToken).ConfigureAwait(false);
        if (intent is null)
        {
            return new VoiceCommandResult(text, null);
        }

        var resolved = await contextResolver.ResolveAsync(intent, cancellationToken).ConfigureAwait(false);
        return new VoiceCommandResult(text, resolved);
    }
}

/// <summary>What was said and, if it matched, what it was resolved to — see <see cref="VoiceCommandListener.ListenWithTextAsync"/>.</summary>
public sealed record VoiceCommandResult(string Text, Intent? Intent);
