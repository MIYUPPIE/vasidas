namespace AutomationStudio.Abstractions;

/// <summary>Which strategy actually located a <see cref="VisualTarget"/> — design doc section 12's fallback chain, collapsed to the four tiers this codebase implements (native UI Automation and AT-SPI accessibility are the same tier here, both behind <see cref="IElementFinder"/>).</summary>
public enum TargetStrategy
{
    ElementFinder,
    Ocr,
    TemplateMatching,
    Coordinates
}

/// <summary>
/// Describes what to find and how, in section 12's priority order: a native element query, OCR
/// text, and/or a template image, tried in that order, with fixed coordinates as the last resort.
/// At least one field should be set. Every field is independently optional — a workflow author (or
/// the self-healing flow in section 26) can supply as many fallback tiers as they have data for.
/// </summary>
public sealed record VisualTarget(
    ElementQuery? Element = null,
    string? OcrText = null,
    double OcrMinConfidence = 0.5,
    byte[]? TemplateImage = null,
    double TemplateMinConfidence = 0.8,
    ScreenRect? SearchRegion = null,
    ScreenPoint? Coordinates = null);

/// <summary>The outcome of resolving a <see cref="VisualTarget"/> — which tier succeeded, where, and how confident it was. <see cref="Element"/> is only set when <see cref="Strategy"/> is <see cref="TargetStrategy.ElementFinder"/>.</summary>
public sealed record TargetResolution(TargetStrategy Strategy, ScreenPoint Position, double Confidence, DetectedElement? Element = null);
