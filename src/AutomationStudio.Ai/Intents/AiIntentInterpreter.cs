using System.Text.Json;
using AutomationStudio.Abstractions;

namespace AutomationStudio.Ai.Intents;

/// <summary>
/// Design doc section 23's "Local AI" fallback tier: when the deterministic pattern matcher can't
/// parse a command, ask the local LLM to classify it against the SAME known intent vocabulary
/// instead of leaving it unrecognized (e.g. "would you mind pausing that for me" → PAUSE_MEDIA).
/// Deliberately narrow — classification against a fixed list, never open-ended intent invention —
/// which is exactly what a real run against the chosen model confirmed it's reliably good at.
/// </summary>
public sealed class AiIntentInterpreter(ILocalAiEngine engine)
{
    // Not an interpolated raw string: the JSON example below needs literal single braces, and
    // IntentCatalog.ToPromptCatalog() is spliced in via concatenation instead so the brace-escaping
    // rules for interpolated raw strings never come into play.
    private static readonly string SystemPrompt = """
        You are a command classifier for a desktop automation tool. Given a spoken command, decide
        which ONE of the following intents it matches, if any:

        """ + IntentCatalog.ToPromptCatalog() + """


        Respond with ONLY a JSON object: {"intent": "<NAME>", "parameters": {...}}. Use "UNKNOWN"
        for intent if nothing matches confidently. Include only the parameters listed for that
        intent, using the exact spoken values (a scene name, a URL, a slide number as a string, and
        so on). Respond with ONLY the JSON object — no other text, no markdown formatting.
        """;

    public async Task<Intent?> InterpretAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string completion;
        try
        {
            completion = await engine.CompleteAsync(SystemPrompt, text, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The model is unavailable/misconfigured — degrade to "nothing understood," matching
            // section 23's "must remain functional without a local LLM," not a crash.
            return null;
        }

        return ParseResponse(completion);
    }

    internal static Intent? ParseResponse(string completion)
    {
        var json = JsonExtraction.ExtractJsonObject(completion);
        if (json is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("intent", out var intentProperty))
            {
                return null;
            }

            var intentName = intentProperty.GetString();
            if (string.IsNullOrWhiteSpace(intentName) || intentName == "UNKNOWN")
            {
                return null;
            }

            var descriptor = IntentCatalog.All.FirstOrDefault(i => i.Name == intentName);
            if (descriptor is null)
            {
                // A hallucinated intent name that isn't real — reject it rather than dispatch
                // something TaskPlanner has no plan for anyway.
                return null;
            }

            var parameters = new Dictionary<string, string>();
            if (root.TryGetProperty("parameters", out var parametersElement) && parametersElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in parametersElement.EnumerateObject())
                {
                    if (!descriptor.Parameters.Contains(property.Name))
                    {
                        continue;
                    }

                    var value = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Number => property.Value.GetRawText(),
                        _ => null
                    };

                    if (value is not null)
                    {
                        parameters[property.Name] = value;
                    }
                }
            }

            // Every parameter the descriptor requires must actually be present — a classification
            // missing a required parameter isn't usable, so fail closed instead of handing
            // TaskPlanner something it will just reject anyway.
            if (descriptor.Parameters.Any(p => !parameters.ContainsKey(p)))
            {
                return null;
            }

            return new Intent(intentName, parameters);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
