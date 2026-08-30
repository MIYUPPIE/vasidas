using AutomationStudio.Abstractions;

namespace AutomationStudio.Applications.VideoPsalm;

/// <summary>Result of a plugin capability — success/failure with an explanation, not an exception: "verse not found" or "VideoPsalm isn't running" are expected, routine outcomes a caller (e.g. voice command handling) should branch on, not catch.</summary>
public sealed record VideoPsalmActionResult(bool Success, string? Message = null)
{
    public static VideoPsalmActionResult Ok() => new(true);
    public static VideoPsalmActionResult Fail(string message) => new(false, message);
}

/// <summary>
/// High-level VideoPsalm capabilities (DAS.md section 14/15) — the automation engine underneath
/// stays generic; this is the only place that knows VideoPsalm-specific detail (which lives in
/// <see cref="VideoPsalmProfile"/>, not hardcoded here).
/// </summary>
public interface IVideoPsalmPlugin
{
    Task<VideoPsalmActionResult> DisplayBibleVerseAsync(BibleReference reference, CancellationToken cancellationToken = default);
    Task<VideoPsalmActionResult> NextVerseAsync(CancellationToken cancellationToken = default);
    Task<VideoPsalmActionResult> PreviousVerseAsync(CancellationToken cancellationToken = default);

    Task<VideoPsalmActionResult> DisplaySongAsync(string title, CancellationToken cancellationToken = default);
    Task<VideoPsalmActionResult> NextSongAsync(CancellationToken cancellationToken = default);
    Task<VideoPsalmActionResult> PreviousSongAsync(CancellationToken cancellationToken = default);

    Task<VideoPsalmActionResult> ClearScreenAsync(CancellationToken cancellationToken = default);
    Task<VideoPsalmActionResult> HideOutputAsync(CancellationToken cancellationToken = default);
}
