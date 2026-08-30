namespace AutomationStudio.Abstractions;

/// <summary>One located template match (design doc section 24/12's "Template Matching").</summary>
public sealed record TemplateMatchResult(ScreenRect Bounds, double Confidence);

/// <summary>
/// Finds a small reference image (an icon, a button's appearance) within a screenshot via
/// normalized cross-correlation (design doc section 24: "Template Matching"). Vision remains a
/// fallback per that section — called on demand by <see cref="AutomationStudio.Core.Vision.TargetResolver"/>
/// (Core), never run continuously against the screen.
/// </summary>
public interface ITemplateMatcher
{
    /// <summary>Returns the best match at or above <paramref name="minConfidence"/> (0.0-1.0), or null if nothing qualifies. <paramref name="searchRegion"/> restricts the search to a sub-area of the screenshot (in full-screenshot coordinates) — narrowing it materially speeds up matching and reduces false positives when the caller has a rough idea where to look.</summary>
    Task<TemplateMatchResult?> FindAsync(
        Screenshot screenshot, byte[] templatePngData, double minConfidence = 0.8,
        ScreenRect? searchRegion = null, CancellationToken cancellationToken = default);
}
