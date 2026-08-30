using AutomationStudio.Abstractions;
using AutomationStudio.Core.Vision;

namespace AutomationStudio.UI.ViewModels;

/// <summary>
/// Design doc section 26's "the original selector failed, a fallback succeeded — update this
/// workflow?" as a real, visible UI element (<see cref="WorkflowEditorViewModel.HealingProposals"/>)
/// instead of a structured log line nobody reads (which is all this signal was surfaced as before —
/// see <c>TargetResolutionHelper</c>'s own history). Plain data — <see cref="WorkflowEditorViewModel"/>
/// owns the Apply/Dismiss commands (matching this codebase's established parameterized-RelayCommand
/// pattern, e.g. <c>AddActionCommand(ActionPaletteEntry)</c>), not this class.
/// </summary>
public sealed class HealingProposalViewModel(string actionId, string actionTypeName, HealingProposal proposal)
{
    public string ActionId { get; } = actionId;
    public string ActionTypeName { get; } = actionTypeName;
    public HealingProposal Proposal { get; } = proposal;

    /// <summary>
    /// Only the Coordinates-succeeded case has real new data to write back — see
    /// <see cref="WorkflowHealer"/>'s own doc comment: OCR/template succeeding over a
    /// higher-priority configured tier means the target spec already has that tier configured
    /// (that's why it was tried at all), so there's nothing different to persist, only something
    /// to acknowledge.
    /// </summary>
    public bool CanApply { get; } = proposal.SucceededStrategy == TargetStrategy.Coordinates && proposal.UpdatedTarget.Coordinates is not null;

    public string Summary { get; } = proposal.SucceededStrategy == TargetStrategy.Coordinates && proposal.UpdatedTarget.Coordinates is { } point
        ? $"{actionTypeName}: {proposal.FailedStrategy} didn't find the target — a coordinate fallback at ({point.X}, {point.Y}) did. Add it to this action?"
        : $"{actionTypeName}: {proposal.FailedStrategy} didn't find the target — {proposal.SucceededStrategy} (already configured) did instead.";
}
