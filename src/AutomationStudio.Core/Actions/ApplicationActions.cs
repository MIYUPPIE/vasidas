using System.Text.Json.Serialization;
using AutomationStudio.Core.Execution;
using Microsoft.Extensions.Logging;

namespace AutomationStudio.Core.Actions;

public sealed class OpenApplicationAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "OpenApplication";

    public required string Path { get; init; }
    public string? Arguments { get; init; }

    public override async Task ExecuteAsync(ActionExecutionContext context)
    {
        var processId = await context.DesktopController
            .OpenApplicationAsync(Path, Arguments, context.CancellationToken)
            .ConfigureAwait(false);

        context.Logger.LogInformation("Launched {Path} (pid {ProcessId})", Path, processId);
    }
}

public sealed class CloseApplicationAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "CloseApplication";

    public required string ProcessName { get; init; }

    public override Task ExecuteAsync(ActionExecutionContext context) =>
        context.DesktopController.CloseApplicationAsync(ProcessName, context.CancellationToken);
}

public sealed class FocusWindowAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "FocusWindow";

    public required string TitleContains { get; init; }

    public override async Task ExecuteAsync(ActionExecutionContext context)
    {
        var focused = await context.WindowManager.FocusWindowByTitleAsync(TitleContains, context.CancellationToken).ConfigureAwait(false);
        if (!focused)
        {
            throw new InvalidOperationException($"No window with a title containing '{TitleContains}' could be focused.");
        }
    }
}

public sealed class FocusApplicationAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "FocusApplication";

    public required string ProcessName { get; init; }

    public override async Task ExecuteAsync(ActionExecutionContext context)
    {
        var focused = await context.WindowManager.FocusWindowByProcessAsync(ProcessName, context.CancellationToken).ConfigureAwait(false);
        if (!focused)
        {
            throw new InvalidOperationException($"No window belonging to process '{ProcessName}' could be focused.");
        }
    }
}
