using AutomationStudio.Abstractions;

namespace AutomationStudio.Core.Execution;

public sealed class ActionExecutionResult
{
    public required string ActionId { get; init; }
    public required string ActionType { get; init; }
    public required ActionStatus Status { get; init; }
    public string? Message { get; init; }
    public string? ScreenshotPath { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required TimeSpan Duration { get; init; }
    public int AttemptCount { get; init; } = 1;

    public static ActionExecutionResult Success(string actionId, string actionType, DateTimeOffset startedAtUtc, TimeSpan duration, int attempts, string? message = null) =>
        new()
        {
            ActionId = actionId,
            ActionType = actionType,
            Status = ActionStatus.Success,
            Message = message,
            StartedAtUtc = startedAtUtc,
            Duration = duration,
            AttemptCount = attempts
        };

    public static ActionExecutionResult Failed(string actionId, string actionType, DateTimeOffset startedAtUtc, TimeSpan duration, int attempts, string message, string? screenshotPath = null) =>
        new()
        {
            ActionId = actionId,
            ActionType = actionType,
            Status = ActionStatus.Failed,
            Message = message,
            ScreenshotPath = screenshotPath,
            StartedAtUtc = startedAtUtc,
            Duration = duration,
            AttemptCount = attempts
        };

    public static ActionExecutionResult TimedOut(string actionId, string actionType, DateTimeOffset startedAtUtc, TimeSpan duration, int attempts, string? screenshotPath = null) =>
        new()
        {
            ActionId = actionId,
            ActionType = actionType,
            Status = ActionStatus.Timeout,
            Message = $"Action timed out after {duration.TotalMilliseconds:F0}ms",
            ScreenshotPath = screenshotPath,
            StartedAtUtc = startedAtUtc,
            Duration = duration,
            AttemptCount = attempts
        };

    public static ActionExecutionResult Skipped(string actionId, string actionType, DateTimeOffset startedAtUtc) =>
        new()
        {
            ActionId = actionId,
            ActionType = actionType,
            Status = ActionStatus.Skipped,
            Message = "Action is disabled",
            StartedAtUtc = startedAtUtc,
            Duration = TimeSpan.Zero,
            AttemptCount = 0
        };

    public static ActionExecutionResult Cancelled(string actionId, string actionType, DateTimeOffset startedAtUtc, TimeSpan duration, int attempts) =>
        new()
        {
            ActionId = actionId,
            ActionType = actionType,
            Status = ActionStatus.Cancelled,
            Message = "Workflow was cancelled",
            StartedAtUtc = startedAtUtc,
            Duration = duration,
            AttemptCount = attempts
        };
}

public sealed class WorkflowExecutionResult
{
    public required string WorkflowId { get; init; }
    public required ActionStatus Status { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required DateTimeOffset CompletedAtUtc { get; init; }
    public required IReadOnlyList<ActionExecutionResult> ActionResults { get; init; }
    public string? FailureReason { get; init; }

    public TimeSpan Duration => CompletedAtUtc - StartedAtUtc;
}
