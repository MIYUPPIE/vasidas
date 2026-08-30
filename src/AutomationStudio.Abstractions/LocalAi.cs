namespace AutomationStudio.Abstractions;

/// <summary>
/// A local, offline language model (design doc section 23: "AI is an optional intelligence layer,
/// not the core automation engine... the application must remain functional without a local LLM").
/// Deliberately a raw completion primitive, not a task-specific method — the semantic layer that
/// decides what to ask for and how to parse the answer (intent classification, workflow generation)
/// is built on top of this, the same shape as <see cref="IOcrEngine"/>/<see cref="ISpeechToText"/>
/// being raw capabilities with their own semantic layers above them.
/// </summary>
public interface ILocalAiEngine
{
    /// <summary>Runs one independent, stateless completion — no memory of prior calls (design doc section 23's flow is one-shot classification/planning per command, never a back-and-forth conversation).</summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}
