namespace AutomationStudio.Storage.Repositories;

/// <summary>Persists workflow runs and their per-action log lines (section 27).</summary>
public interface IExecutionRepository
{
    /// <summary>Records a new run starting. Returns the new execution id.</summary>
    Task<string> StartExecutionAsync(string workflowId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken = default);

    Task CompleteExecutionAsync(
        string executionId, DateTimeOffset completedAtUtc, string status, string? error, CancellationToken cancellationToken = default);

    Task AddLogEntryAsync(ExecutionLogEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExecutionRecord>> ListExecutionsAsync(string workflowId, int limit = 50, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExecutionLogEntry>> GetLogEntriesAsync(string executionId, CancellationToken cancellationToken = default);
}
