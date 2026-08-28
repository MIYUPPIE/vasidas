using AutomationStudio.Core.Actions;

namespace AutomationStudio.Core.Workflows;

/// <summary>
/// A workflow: an ordered list of actions plus the metadata section 9's JSON format expects
/// (id, name, version). Platform-independent by construction — every field here is either data or
/// a <see cref="WorkflowAction"/>, and actions only ever talk to <c>IDesktopController</c> etc.
/// </summary>
public sealed class Workflow
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int Version { get; init; } = 1;
    public required IReadOnlyList<WorkflowAction> Actions { get; init; }

    /// <summary>Stop the whole workflow on the first action failure. Default true (section 16: never silently continue).</summary>
    public bool StopOnFailure { get; init; } = true;
}
