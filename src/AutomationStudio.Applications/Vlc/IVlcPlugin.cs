namespace AutomationStudio.Applications.Vlc;

/// <summary>High-level VLC media player capabilities (DAS.md section 14/9 — backs the PLAY_MEDIA/PAUSE_MEDIA/STOP_MEDIA intents DAS.md section 9 already lists).</summary>
public interface IVlcPlugin
{
    Task<PluginActionResult> PlayAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> PauseAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> StopAsync(CancellationToken cancellationToken = default);

    Task<PluginActionResult> NextTrackAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> PreviousTrackAsync(CancellationToken cancellationToken = default);

    Task<PluginActionResult> ToggleMuteAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> ToggleFullscreenAsync(CancellationToken cancellationToken = default);
}
