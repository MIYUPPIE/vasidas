using AutomationStudio.Abstractions;
using AutomationStudio.Ai.Intents;

namespace AutomationStudio.Voice.Intents;

/// <summary>
/// Design doc section 23's full "Deterministic Parser → known command execute immediately; unknown/
/// complex → Local AI → Intent" chain: tries <paramref name="deterministicRecognizer"/> first (fast,
/// free, exact — always tried, regardless of whether AI is configured), falling back to
/// <paramref name="aiInterpreter"/> only when that returns nothing. Leaving <paramref name="aiInterpreter"/>
/// null keeps this behaviorally identical to the plain deterministic recognizer, which is exactly
/// section 23's "the application must remain functional without a local LLM."
/// </summary>
public sealed class AiAugmentedIntentRecognizer(IIntentRecognizer deterministicRecognizer, AiIntentInterpreter? aiInterpreter = null) : IIntentRecognizer
{
    public async Task<Intent?> RecognizeAsync(string text, CancellationToken cancellationToken = default)
    {
        var intent = await deterministicRecognizer.RecognizeAsync(text, cancellationToken).ConfigureAwait(false);
        if (intent is not null)
        {
            return intent;
        }

        return aiInterpreter is null
            ? null
            : await aiInterpreter.InterpretAsync(text, cancellationToken).ConfigureAwait(false);
    }
}
