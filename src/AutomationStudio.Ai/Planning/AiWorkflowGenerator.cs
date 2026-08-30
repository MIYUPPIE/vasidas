using System.Text.Json;
using AutomationStudio.Abstractions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Core.Workflows;

namespace AutomationStudio.Ai.Planning;

/// <summary>
/// Design doc section 23's "Advanced task planning" and the dynamic-workflow-generation fallback
/// <c>ITaskPlanner</c>'s own doc comment forward-references as Phase 10's local-AI-gated addition —
/// for a request that doesn't match any single known intent (even via
/// <see cref="Intents.AiIntentInterpreter"/>) but describes a multi-step task, matching design doc
/// section 4.3's own worked example ("Start Sunday service" → open VideoPsalm, load service, open
/// OBS, start streaming, display opening slide).
/// Reuses <see cref="WorkflowSerializer"/> for parsing and validating the model's output — the model
/// only ever has to produce JSON in the SAME schema every taught/saved workflow already uses, and an
/// invalid or unparseable response fails closed (treated as "couldn't plan this") rather than
/// executing anything unvalidated.
/// </summary>
public sealed class AiWorkflowGenerator(ILocalAiEngine engine, WorkflowExecutionEngine executionEngine)
{
    private const string SystemPrompt = """
        You are a workflow planner for a desktop automation tool. Given a natural-language
        description of a multi-step task, produce a JSON workflow using ONLY this schema:

        {"id": "<short-slug>", "name": "<name>", "version": 1, "actions": [ ...actions... ]}

        Each entry in "actions" is one of exactly these shapes:
        {"type": "FocusApplication", "processName": "<process name, e.g. obs, vlc>"}
        {"type": "FocusWindow", "titleContains": "<window title substring>"}
        {"type": "Wait", "milliseconds": <number>}
        {"type": "TypeText", "text": "<text to type>"}
        {"type": "PressKey", "key": "<KeyName>"}
        {"type": "KeyCombination", "keys": ["<KeyName>", "<KeyName>", ...]}
        {"type": "ClickTarget", "target": {"text": "<visible on-screen text>", "ocrText": "<same text>"}}
        {"type": "FindTarget", "target": {"text": "<visible on-screen text>", "ocrText": "<same text>"}}
        {"type": "VerifyTarget", "target": {"text": "<visible on-screen text>", "ocrText": "<same text>"}}

        Valid KeyName values: A-Z, D0-D9, F1-F12, Enter, Escape, Backspace, Tab, Space, Left, Right,
        Up, Down, Home, End, PageUp, PageDown, Insert, Delete, LeftControl, RightControl, LeftAlt,
        RightAlt, LeftShift, RightShift.

        Rules:
        - Never use an action type other than the ones listed above — there are no others.
        - Never invent a real file path — you do not know one. Use "FocusApplication" with the
          application's process name to switch to an already-running or already-launchable
          application instead of trying to open it by path.
        - Prefer "ClickTarget" with descriptive on-screen text over guessing screen coordinates.
        - Insert a "Wait" action after switching applications, before interacting with them.
        - Respond with ONLY the JSON object — no other text, no markdown formatting, no commentary.
        """;

    /// <summary>Generates a workflow from <paramref name="description"/> and, if generation produced something usable, runs it through the same <see cref="WorkflowExecutionEngine"/> every other workflow runs through. Returns null (not a failed <see cref="TaskExecutionResult"/>) when nothing could even be generated — the caller treats that the same as "nothing understood," not as an attempted-and-failed action.</summary>
    public async Task<TaskExecutionResult?> TryGenerateAndRunAsync(string description, CancellationToken cancellationToken = default)
    {
        var workflow = await TryGenerateAsync(description, cancellationToken).ConfigureAwait(false);
        if (workflow is null)
        {
            return null;
        }

        var result = await executionEngine.ExecuteAsync(workflow, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.Status == ActionStatus.Success
            ? TaskExecutionResult.Ok($"Generated and ran a {workflow.Actions.Count}-step workflow.")
            : TaskExecutionResult.Fail(result.FailureReason ?? $"Generated workflow ended with status {result.Status}.");
    }

    internal async Task<Workflow?> TryGenerateAsync(string description, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        string completion;
        try
        {
            completion = await engine.CompleteAsync(SystemPrompt, description, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }

        var json = JsonExtraction.ExtractJsonObject(completion);
        if (json is null)
        {
            return null;
        }

        try
        {
            var workflow = WorkflowSerializer.FromJson(json);
            return workflow.Actions.Count > 0 ? workflow : null;
        }
        catch (JsonException)
        {
            // The model didn't produce valid/known workflow JSON — fail closed, never execute
            // anything that didn't round-trip through the same validation every other workflow does.
            return null;
        }
    }
}
