using AutomationStudio.Core.Execution;
using Microsoft.Extensions.Logging;

namespace AutomationStudio.Core.Actions;

public sealed class OpenApplicationAction : WorkflowAction
{
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
    public override string ActionType => "CloseApplication";

    public required string ProcessName { get; init; }

    public override Task ExecuteAsync(ActionExecutionContext context) =>
        context.DesktopController.CloseApplicationAsync(ProcessName, context.CancellationToken);
}
