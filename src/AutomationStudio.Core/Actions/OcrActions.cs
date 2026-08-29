using System.Text.Json.Serialization;
using AutomationStudio.Abstractions;
using AutomationStudio.Core.Execution;

namespace AutomationStudio.Core.Actions;

/// <summary>
/// Shared polling search used by every OCR action (section 40 Phase 4's "Find text" / "Wait for
/// text"): a single OCR attempt, retried on a short interval until a match appears or the action's
/// own <see cref="WorkflowAction.TimeoutMilliseconds"/> cancels <see cref="ActionExecutionContext.CancellationToken"/>
/// — "wait for text" is this loop bounded by that existing per-action timeout, not a separate
/// action type or a second timeout concept.
/// </summary>
internal static class OcrSearch
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(400);

    public static async Task<OcrResult> PollForTextAsync(ActionExecutionContext context, string text, double minConfidence, ScreenRect? region)
    {
        var options = new OcrOptions { MinConfidence = minConfidence, Region = region };

        while (true)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var screenshot = await context.ScreenshotService.CaptureScreenAsync(context.CancellationToken).ConfigureAwait(false);
            var results = await context.OcrEngine.RecognizeAsync(screenshot, options, context.CancellationToken).ConfigureAwait(false);
            var match = results.FirstOrDefault(r => r.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(PollInterval, context.CancellationToken).ConfigureAwait(false);
        }
    }
}

public sealed class FindTextAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "FindText";

    public required string Text { get; init; }
    public double MinConfidence { get; init; } = 0.5;
    public ScreenRect? Region { get; init; }

    /// <summary>Positions the mouse over the found text without clicking — matches section 9's canonical JSON, where a FindText action is immediately followed by a bare Click action with no coordinates.</summary>
    public override async Task ExecuteAsync(ActionExecutionContext context)
    {
        var match = await OcrSearch.PollForTextAsync(context, Text, MinConfidence, Region).ConfigureAwait(false);
        await context.DesktopController.MoveMouseAsync(match.Bounds.Center, context.CancellationToken).ConfigureAwait(false);
    }
}

public sealed class ClickTextAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "ClickText";

    public required string Text { get; init; }
    public double MinConfidence { get; init; } = 0.5;
    public ScreenRect? Region { get; init; }
    public MouseButton Button { get; init; } = MouseButton.Left;

    public override async Task ExecuteAsync(ActionExecutionContext context)
    {
        var match = await OcrSearch.PollForTextAsync(context, Text, MinConfidence, Region).ConfigureAwait(false);
        await context.DesktopController.ClickAsync(Button, match.Bounds.Center, context.CancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Asserts text is on screen without touching the mouse — fails (times out) if it never appears, making it a pass/fail gate for a workflow.</summary>
public sealed class VerifyTextAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "VerifyText";

    public required string Text { get; init; }
    public double MinConfidence { get; init; } = 0.5;
    public ScreenRect? Region { get; init; }

    public override async Task ExecuteAsync(ActionExecutionContext context) =>
        await OcrSearch.PollForTextAsync(context, Text, MinConfidence, Region).ConfigureAwait(false);
}
