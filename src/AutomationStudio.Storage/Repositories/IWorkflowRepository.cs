using AutomationStudio.Storage;

namespace AutomationStudio.Storage.Repositories;

public interface IWorkflowRepository
{
    Task<IReadOnlyList<WorkflowSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<WorkflowRecord?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Inserts or fully replaces the workflow and its actions.</summary>
    Task SaveAsync(WorkflowRecord record, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
