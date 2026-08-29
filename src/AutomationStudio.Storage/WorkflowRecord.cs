using AutomationStudio.Core.Workflows;

namespace AutomationStudio.Storage;

/// <summary>
/// A <see cref="Workflow"/> plus the persistence metadata section 37's <c>workflows</c> table adds
/// on top of it (platform targeting, enabled flag, timestamps). Kept separate from
/// <see cref="Workflow"/> itself deliberately: the portable JSON format (section 9) has no database
/// bookkeeping fields, and a workflow you import/export shouldn't need any either — only once it's
/// actually stored does it acquire this metadata.
/// </summary>
public sealed class WorkflowRecord
{
    public required Workflow Workflow { get; init; }

    /// <summary>Target platform for this workflow: "Windows", "Linux", or null for either.</summary>
    public string? Platform { get; init; }

    public bool Enabled { get; init; } = true;
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

/// <summary>Lightweight listing projection — avoids loading every action of every workflow just to populate a list view.</summary>
public sealed record WorkflowSummary(
    string Id,
    string Name,
    string? Description,
    string? Platform,
    bool Enabled,
    DateTimeOffset UpdatedAtUtc);
