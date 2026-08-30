using System.Text.Json.Serialization;
using AutomationStudio.Abstractions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Core.Vision;
using Microsoft.Extensions.Logging;

namespace AutomationStudio.Core.Actions;

/// <summary>
/// How to locate a target for the full resolution chain (design doc section 12): native element
/// query, OCR text, and/or a template image, tried in that priority order, with fixed coordinates as
/// the last resort. At least one field should be set — finders/matchers only run for the tiers that
/// have data. The template is a path to a PNG on disk, not embedded inline, matching how every other
/// image reference in this codebase (screenshots, OCR fixtures) is a file path, not raw bytes in JSON.
/// </summary>
public sealed class VisualTargetSpec
{
    public string? Text { get; init; }
    public string? AutomationId { get; init; }
    public string? WindowTitleContains { get; init; }

    public string? OcrText { get; init; }
    public double OcrMinConfidence { get; init; } = 0.5;

    public string? TemplateImagePath { get; init; }
    public double TemplateMinConfidence { get; init; } = 0.8;

    public ScreenRect? SearchRegion { get; init; }
    public int? FallbackX { get; init; }
    public int? FallbackY { get; init; }

    private bool HasElementQuery => Text is not null || AutomationId is not null || WindowTitleContains is not null;

    public async Task<VisualTarget> ToVisualTargetAsync(CancellationToken cancellationToken)
    {
        byte[]? templateBytes = null;
        if (TemplateImagePath is { Length: > 0 } path)
        {
            templateBytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }

        return new VisualTarget(
            Element: HasElementQuery ? new ElementQuery(Text, AutomationId, WindowTitleContains) : null,
            OcrText: OcrText,
            OcrMinConfidence: OcrMinConfidence,
            TemplateImage: templateBytes,
            TemplateMinConfidence: TemplateMinConfidence,
            SearchRegion: SearchRegion,
            Coordinates: FallbackX.HasValue && FallbackY.HasValue ? new ScreenPoint(FallbackX.Value, FallbackY.Value) : null);
    }
}

/// <summary>
/// Shared by every Target action: polls the resolution chain (same 400ms cadence as
/// <see cref="OcrSearch"/>, bounded by the action's own <see cref="WorkflowAction.TimeoutMilliseconds"/>
/// via <see cref="ActionExecutionContext.CancellationToken"/> — a natural generalization of both
/// ElementAction's retry-based waiting and OcrAction's poll-based waiting into one mechanism for
/// this richer, unified action family), then logs section 26's self-healing signal (a higher-priority
/// configured tier failed, a lower one caught it) as a structured warning — the trace a future UI
/// would read to propose updating the workflow, per that section's own worked example.
/// </summary>
internal static class TargetResolutionHelper
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(400);

    public static async Task<TargetResolution> ResolveOrThrowAsync(ActionExecutionContext context, VisualTargetSpec spec)
    {
        var target = await spec.ToVisualTargetAsync(context.CancellationToken).ConfigureAwait(false);

        while (true)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var resolution = await context.TargetResolver.ResolveAsync(target, context.CancellationToken).ConfigureAwait(false);
            if (resolution is not null)
            {
                LogResolution(context, target, resolution);
                return resolution;
            }

            await Task.Delay(PollInterval, context.CancellationToken).ConfigureAwait(false);
        }
    }

    private static void LogResolution(ActionExecutionContext context, VisualTarget target, TargetResolution resolution)
    {
        var proposal = WorkflowHealer.Propose(target, resolution);
        if (proposal is not null)
        {
            context.Logger.LogWarning(
                "Target resolved via fallback: {FailedStrategy} did not find it, {SucceededStrategy} did (confidence {Confidence:F2}) — this workflow may need updating (design doc section 26).",
                proposal.FailedStrategy, proposal.SucceededStrategy, resolution.Confidence);
            // Also handed to the engine via the context, not just logged — this is what lets a real
            // UI surface it as an actual confirmation prompt instead of text nobody reads.
            context.HealingProposal = proposal;
        }
        else
        {
            context.Logger.LogDebug("Target resolved via {Strategy} (confidence {Confidence:F2}).", resolution.Strategy, resolution.Confidence);
        }
    }
}

/// <summary>Locates a target via the full resolution chain and positions the mouse over it without clicking — matches FindTextAction's convention of chaining into a following bare Click action.</summary>
public sealed class FindTargetAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "FindTarget";

    public required VisualTargetSpec Target { get; init; }

    public override async Task ExecuteAsync(ActionExecutionContext context)
    {
        var resolution = await TargetResolutionHelper.ResolveOrThrowAsync(context, Target).ConfigureAwait(false);
        await context.DesktopController.MoveMouseAsync(resolution.Position, context.CancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Locates a target via the full resolution chain (design doc section 12) and clicks it — invoking
/// the native automation pattern when the ElementFinder tier is what resolved it, falling back to a
/// coordinate click at the resolved position otherwise (including when the native pattern exists but
/// isn't invoke-style, the same fallback <see cref="ClickElementAction"/> already makes).
/// </summary>
public sealed class ClickTargetAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "ClickTarget";

    public required VisualTargetSpec Target { get; init; }
    public MouseButton Button { get; init; } = MouseButton.Left;

    public override async Task ExecuteAsync(ActionExecutionContext context)
    {
        var resolution = await TargetResolutionHelper.ResolveOrThrowAsync(context, Target).ConfigureAwait(false);

        if (resolution is { Strategy: TargetStrategy.ElementFinder, Element: { } element })
        {
            try
            {
                await context.ElementFinder.InvokeAsync(element, context.CancellationToken).ConfigureAwait(false);
                return;
            }
            catch (ElementPatternNotSupportedException)
            {
                // Falls through to the coordinate click below — the element exists but has no
                // invoke-style pattern, same fallback ClickElementAction already makes.
            }
        }

        await context.DesktopController.ClickAsync(Button, resolution.Position, context.CancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Asserts a target can be located via the full resolution chain without touching the mouse — fails (times out via the action's own retry/timeout) if it never resolves, making it a pass/fail gate for a workflow.</summary>
public sealed class VerifyTargetAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "VerifyTarget";

    public required VisualTargetSpec Target { get; init; }

    public override async Task ExecuteAsync(ActionExecutionContext context) =>
        await TargetResolutionHelper.ResolveOrThrowAsync(context, Target).ConfigureAwait(false);
}
