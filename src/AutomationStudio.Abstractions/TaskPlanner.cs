namespace AutomationStudio.Abstractions;

/// <summary>Result of actually carrying out an intent — as opposed to <see cref="IIntentRecognizer"/>/context resolution, which only decide what the user meant.</summary>
public sealed record TaskExecutionResult(bool Success, string? Message = null)
{
    public static TaskExecutionResult Ok(string? message = null) => new(true, message);
    public static TaskExecutionResult Fail(string message) => new(false, message);
}

/// <summary>
/// Turns a resolved <see cref="Intent"/> into action (DAS.md section 10/31): "Intent → Task Planner
/// → Task Plan → Workflow Engine." The intent never manipulates the mouse/keyboard directly — the
/// planner decides whether an existing plugin action, a generic workflow, or (not built — that's
/// section 37 Phase 10, gated behind local AI) a dynamically generated one should handle it.
/// </summary>
public interface ITaskPlanner
{
    /// <summary>Executes the intent, or reports it can't (an unrecognized intent name, or one this planner has no plan for yet) — never throws for "I don't know how to do that," only for a genuine infrastructure failure.</summary>
    Task<TaskExecutionResult> ExecuteAsync(Intent intent, CancellationToken cancellationToken = default);
}
