using AutomationStudio.Storage;
using AutomationStudio.Storage.Repositories;

namespace AutomationStudio.UI.Tests;

internal sealed class FakeWorkflowRepository : IWorkflowRepository
{
    public Dictionary<string, WorkflowRecord> Saved { get; } = [];

    public Task<IReadOnlyList<WorkflowSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkflowSummary>>(Saved.Values
            .Select(r => new WorkflowSummary(r.Workflow.Id, r.Workflow.Name, r.Workflow.Description, r.Platform, r.Enabled, r.UpdatedAtUtc))
            .ToList());

    public Task<WorkflowRecord?> GetAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Saved.TryGetValue(id, out var record) ? record : null);

    public Task SaveAsync(WorkflowRecord record, CancellationToken cancellationToken = default)
    {
        Saved[record.Workflow.Id] = record;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        Saved.Remove(id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeExecutionRepository : IExecutionRepository
{
    public List<(string ExecutionId, string WorkflowId)> StartedExecutions { get; } = [];
    public List<(string ExecutionId, string Status, string? Error)> CompletedExecutions { get; } = [];
    public List<ExecutionLogEntry> LogEntries { get; } = [];

    public Task<string> StartExecutionAsync(string workflowId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString("n");
        StartedExecutions.Add((id, workflowId));
        return Task.FromResult(id);
    }

    public Task CompleteExecutionAsync(string executionId, DateTimeOffset completedAtUtc, string status, string? error, CancellationToken cancellationToken = default)
    {
        CompletedExecutions.Add((executionId, status, error));
        return Task.CompletedTask;
    }

    public Task AddLogEntryAsync(ExecutionLogEntry entry, CancellationToken cancellationToken = default)
    {
        LogEntries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ExecutionRecord>> ListExecutionsAsync(string workflowId, int limit = 50, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ExecutionRecord>>([]);

    public Task<IReadOnlyList<ExecutionLogEntry>> GetLogEntriesAsync(string executionId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ExecutionLogEntry>>(LogEntries.Where(e => e.ExecutionId == executionId).ToList());
}
