using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Core.Workflows;

namespace AutomationStudio.Applications.Plugins;

/// <summary>
/// Shared mechanics for every profile-driven application plugin (DAS.md section 14): focus the
/// target application, then either send a keyboard shortcut or click an on-screen control by its
/// text (via the design doc section 12 Target Resolution Engine — the right choice for an
/// application like OBS that ships with no default hotkeys at all, confirmed live/via research; see
/// <c>ObsPlugin</c>), and run the result as a <see cref="Workflow"/> through the same generic
/// <see cref="WorkflowExecutionEngine"/> everything else uses. <c>VideoPsalmPlugin</c> was the first
/// of these; every plugin since reuses this instead of re-deriving the same handful of helpers.
/// </summary>
public abstract class ApplicationPluginBase(WorkflowExecutionEngine engine, string processName)
{
    protected FocusApplicationAction FocusApplication() => new() { ProcessName = processName };

    protected static PressKeyAction PressKey(Key key) => new() { Key = key };

    protected static KeyCombinationAction KeyCombo(params Key[] keys) => new() { Keys = keys };

    protected static TypeTextAction TypeText(string text) => new() { Text = text };

    /// <summary>
    /// Clicks an on-screen control by its visible text via the full section 12 resolution chain
    /// (native element query, falling back to OCR) — for applications with no reliable default
    /// keyboard shortcut for a given action. A short 5-second default (WorkflowAction's own default
    /// is 30 seconds) — this is a "the control isn't there" check, not something worth waiting half
    /// a minute to fail; caught live via a genuinely slow test run before shipping this default.
    /// </summary>
    protected static ClickTargetAction ClickButton(string text, int timeoutMilliseconds = 5000) =>
        new() { Target = new VisualTargetSpec { Text = text, OcrText = text }, TimeoutMilliseconds = timeoutMilliseconds };

    protected async Task<PluginActionResult> RunAsync(string workflowId, IReadOnlyList<WorkflowAction> actions, CancellationToken cancellationToken)
    {
        var workflow = new Workflow { Id = workflowId, Name = workflowId, Actions = actions };
        var result = await engine.ExecuteAsync(workflow, cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.Status == ActionStatus.Success
            ? PluginActionResult.Ok()
            : PluginActionResult.Fail(result.FailureReason ?? $"Workflow ended with status {result.Status}.");
    }
}
