namespace AutomationStudio.Abstractions;

/// <summary>
/// Captures the screen. Kept separate from <see cref="IDesktopController"/> (section 6 of the
/// design doc) because OCR, vision matching and failure-diagnostics all need screenshots without
/// needing input control.
/// </summary>
public interface IScreenshotService
{
    /// <summary>Captures the full virtual screen (all monitors).</summary>
    Task<Screenshot> CaptureScreenAsync(CancellationToken cancellationToken = default);

    /// <summary>Captures a specific region of the screen.</summary>
    Task<Screenshot> CaptureRegionAsync(ScreenRect region, CancellationToken cancellationToken = default);
}
