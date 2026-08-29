namespace AutomationStudio.Storage;

/// <summary>Section 37's <c>executions</c> table: one row per workflow run.</summary>
public sealed record ExecutionRecord(
    string Id,
    string WorkflowId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string Status,
    string? Error);

/// <summary>Section 37's <c>execution_logs</c> table: the detailed per-event log section 27 requires.</summary>
public sealed record ExecutionLogEntry(
    string ExecutionId,
    DateTimeOffset TimestampUtc,
    string Level,
    string Message,
    string? ActionId,
    string? ScreenshotPath);
