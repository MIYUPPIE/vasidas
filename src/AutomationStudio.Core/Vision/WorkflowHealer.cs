using AutomationStudio.Abstractions;

namespace AutomationStudio.Core.Vision;

/// <summary>The primary (highest-priority configured) tier didn't find anything, but <see cref="SucceededStrategy"/> did — design doc section 26's "the original selector failed, a fallback succeeded" signal.</summary>
public sealed record HealingProposal(TargetStrategy FailedStrategy, TargetStrategy SucceededStrategy, VisualTarget UpdatedTarget);

/// <summary>
/// Detects section 26's self-healing signal and proposes an updated <see cref="VisualTarget"/>
/// capturing what actually worked. Deciding whether to apply and persist the proposal is left to the
/// caller — section 26's own worked example phrases this as a question put to the user ("Update this
/// workflow to use OCR as a fallback?"), not something to do automatically.
/// </summary>
public static class WorkflowHealer
{
    /// <summary>Returns null when nothing needs healing: either the highest-priority tier the target was already configured for is exactly what succeeded, or nothing succeeded at all.</summary>
    public static HealingProposal? Propose(VisualTarget originalTarget, TargetResolution resolution)
    {
        var primaryTier = HighestConfiguredTier(originalTarget);
        if (primaryTier is null || primaryTier == resolution.Strategy)
        {
            return null;
        }

        // Coordinates succeeding is the one case with genuinely new information to capture — the
        // OCR/template tiers, if they're what succeeded, were already configured for the resolver
        // to have tried them at all, so there's nothing to add beyond the flag itself.
        var updatedTarget = resolution.Strategy == TargetStrategy.Coordinates
            ? originalTarget with { Coordinates = resolution.Position }
            : originalTarget;

        return new HealingProposal(primaryTier.Value, resolution.Strategy, updatedTarget);
    }

    private static TargetStrategy? HighestConfiguredTier(VisualTarget target)
    {
        if (target.Element is not null)
        {
            return TargetStrategy.ElementFinder;
        }

        if (!string.IsNullOrEmpty(target.OcrText))
        {
            return TargetStrategy.Ocr;
        }

        if (target.TemplateImage is { Length: > 0 })
        {
            return TargetStrategy.TemplateMatching;
        }

        if (target.Coordinates is not null)
        {
            return TargetStrategy.Coordinates;
        }

        return null;
    }
}
