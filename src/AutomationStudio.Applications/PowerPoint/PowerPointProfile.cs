using AutomationStudio.Abstractions;

namespace AutomationStudio.Applications.PowerPoint;

/// <summary>
/// Everything about PowerPoint the plugin needs beyond automation-engine logic (DAS.md section 13).
/// PowerPoint is a Windows-only product (no native Linux build; LibreOffice Impress is different
/// software) — same caveat class as VideoPsalm, compile/logic-verified only in this repo.
/// </summary>
public sealed record PowerPointProfile
{
    public required string ProcessName { get; init; }

    public required Key NextSlideKey { get; init; }
    public required Key PreviousSlideKey { get; init; }
    public required Key StartPresentationKey { get; init; }
    public required Key EndPresentationKey { get; init; }

    /// <summary>
    /// Confirmed via Microsoft's own published shortcut docs
    /// (support.microsoft.com/.../use-keyboard-shortcuts-to-deliver-powerpoint-presentations): F5
    /// starts a slide show from the beginning; Right/Down/N/Space/Page Down all advance a slide
    /// (Right used here); Left/Up/P/Backspace/Page Up all go back (Left used here); typing a slide
    /// number then Enter jumps to it during a slide show; Escape exits the slide show back to the
    /// editor (the universal "end presentation" key — distinct from B/Period, which blacks the
    /// screen without ending the show and is not what "EndPresentation" means here).
    /// </summary>
    public static PowerPointProfile Default { get; } = new()
    {
        ProcessName = "POWERPNT",
        NextSlideKey = Key.Right,
        PreviousSlideKey = Key.Left,
        StartPresentationKey = Key.F5,
        EndPresentationKey = Key.Escape,
    };
}
