using System.Text.Json.Serialization;
using AutomationStudio.Abstractions;
using AutomationStudio.Core.Execution;

namespace AutomationStudio.Core.Actions;

/// <summary>How to locate an element — the nested JSON "target" object from section 15's example (<c>{ "target": { "text": "Export" } }</c>).</summary>
public sealed class ElementTarget
{
    public string? Text { get; init; }
    public string? AutomationId { get; init; }
    public string? WindowTitleContains { get; init; }

    public ElementQuery ToQuery() => new(Text, AutomationId, WindowTitleContains);
}

/// <summary>
/// Locates an element and fails the action (retryable, like every action, via
/// <see cref="WorkflowAction.RetryCount"/>/<see cref="WorkflowAction.TimeoutMilliseconds"/>) if it
/// isn't found — a "wait for this UI element to exist" gate, reusing the engine's existing
/// retry/timeout machinery rather than needing a separate WaitUntil mechanism.
/// </summary>
public sealed class FindElementAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "FindElement";

    public required ElementTarget Target { get; init; }

    public override async Task ExecuteAsync(ActionExecutionContext context)
    {
        var query = Target.ToQuery();
        var element = await context.ElementFinder.FindElementAsync(query, context.CancellationToken).ConfigureAwait(false);
        if (element is null)
        {
            throw new InvalidOperationException($"No element found matching {query}.");
        }
    }
}

/// <summary>
/// Finds an element and invokes it via its native automation pattern (e.g. a button press),
/// falling back to a coordinate click at the element's bounds when the platform reports the
/// element has no invoke-style pattern — the same reliability-first fallback the design doc
/// describes in section 2, applied one level down from "which finder" to "how to activate".
/// </summary>
public sealed class ClickElementAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "ClickElement";

    public required ElementTarget Target { get; init; }

    public override async Task ExecuteAsync(ActionExecutionContext context)
    {
        var query = Target.ToQuery();
        var element = await context.ElementFinder.FindElementAsync(query, context.CancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No element found matching {query}.");

        try
        {
            await context.ElementFinder.InvokeAsync(element, context.CancellationToken).ConfigureAwait(false);
        }
        catch (ElementPatternNotSupportedException)
        {
            await context.DesktopController.ClickAsync(MouseButton.Left, element.Bounds.Center, context.CancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Finds an element and sets its text via the native text-value pattern, falling back to clicking
/// it (to focus) and typing via <see cref="IDesktopController"/> when the platform reports no
/// text-value pattern is available.
/// </summary>
public sealed class TypeIntoElementAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "TypeIntoElement";

    public required ElementTarget Target { get; init; }
    public required string Text { get; init; }

    public override async Task ExecuteAsync(ActionExecutionContext context)
    {
        var query = Target.ToQuery();
        var element = await context.ElementFinder.FindElementAsync(query, context.CancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No element found matching {query}.");

        try
        {
            await context.ElementFinder.SetTextAsync(element, Text, context.CancellationToken).ConfigureAwait(false);
        }
        catch (ElementPatternNotSupportedException)
        {
            await context.DesktopController.ClickAsync(MouseButton.Left, element.Bounds.Center, context.CancellationToken).ConfigureAwait(false);
            await context.DesktopController.TypeTextAsync(Text, context.CancellationToken).ConfigureAwait(false);
        }
    }
}
