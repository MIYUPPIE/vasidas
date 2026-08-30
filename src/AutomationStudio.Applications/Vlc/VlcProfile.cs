using AutomationStudio.Abstractions;

namespace AutomationStudio.Applications.Vlc;

/// <summary>Everything about VLC the plugin needs beyond automation-engine logic (DAS.md section 13). VLC is genuinely cross-platform; the process name below is the common Linux/Windows binary name, though a given install could differ (e.g. a distro packaging it under a different name) — not independently verified per-distro.</summary>
public sealed record VlcProfile
{
    public required string ProcessName { get; init; }

    public required Key PlayPauseKey { get; init; }
    public required Key StopKey { get; init; }
    public required Key NextTrackKey { get; init; }
    public required Key PreviousTrackKey { get; init; }
    public required Key MuteKey { get; init; }
    public required Key FullscreenKey { get; init; }

    /// <summary>
    /// Confirmed VLC default hotkeys (vlchelp.com, ditig.com cheat sheets). Space is documented as a
    /// single toggle for play/pause — VLC has no separate default "play" vs. "pause" key, so
    /// <see cref="VlcPlugin.PlayAsync"/> and <see cref="VlcPlugin.PauseAsync"/> both send this same
    /// key, honestly matching what the application actually does rather than inventing two shortcuts
    /// that don't exist. S stops, N/P move to the next/previous playlist item, M mutes, F toggles
    /// fullscreen.
    /// </summary>
    public static VlcProfile Default { get; } = new()
    {
        ProcessName = "vlc",
        PlayPauseKey = Key.Space,
        StopKey = Key.S,
        NextTrackKey = Key.N,
        PreviousTrackKey = Key.P,
        MuteKey = Key.M,
        FullscreenKey = Key.F,
    };
}
