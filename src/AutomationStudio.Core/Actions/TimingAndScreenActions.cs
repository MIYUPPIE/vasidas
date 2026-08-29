using System.Text.Json.Serialization;
using AutomationStudio.Core.Execution;

namespace AutomationStudio.Core.Actions;

public sealed class WaitAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "Wait";

    public required int Milliseconds { get; init; }

    public override Task ExecuteAsync(ActionExecutionContext context) =>
        Task.Delay(Milliseconds, context.CancellationToken);
}

public sealed class TakeScreenshotAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "TakeScreenshot";

    /// <summary>Optional path to save the PNG to. If omitted, the screenshot is only captured for the log.</summary>
    public string? SavePath { get; init; }

    public override async Task ExecuteAsync(ActionExecutionContext context)
    {
        var screenshot = await context.ScreenshotService
            .CaptureScreenAsync(context.CancellationToken)
            .ConfigureAwait(false);

        if (SavePath is not null)
        {
            await screenshot.SaveAsync(SavePath, context.CancellationToken).ConfigureAwait(false);
        }
    }
}
