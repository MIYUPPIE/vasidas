using AutomationStudio.Abstractions;

namespace AutomationStudio.Applications.VideoPsalm;

/// <summary>
/// High-level VideoPsalm capabilities (DAS.md section 14/15) — the automation engine underneath
/// stays generic; this is the only place that knows VideoPsalm-specific detail (which lives in
/// <see cref="VideoPsalmProfile"/>, not hardcoded here).
/// </summary>
public interface IVideoPsalmPlugin
{
    Task<PluginActionResult> DisplayBibleVerseAsync(BibleReference reference, CancellationToken cancellationToken = default);
    Task<PluginActionResult> NextVerseAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> PreviousVerseAsync(CancellationToken cancellationToken = default);

    Task<PluginActionResult> DisplaySongAsync(string title, CancellationToken cancellationToken = default);
    Task<PluginActionResult> NextSongAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> PreviousSongAsync(CancellationToken cancellationToken = default);

    Task<PluginActionResult> ClearScreenAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> HideOutputAsync(CancellationToken cancellationToken = default);
}
