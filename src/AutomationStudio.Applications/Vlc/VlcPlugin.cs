using AutomationStudio.Applications.Plugins;
using AutomationStudio.Core.Execution;

namespace AutomationStudio.Applications.Vlc;

/// <summary>Profile-driven VLC automation (DAS.md section 14) — every capability sends <see cref="VlcProfile"/>'s documented shortcut and runs it through the same generic <see cref="WorkflowExecutionEngine"/> everything else uses.</summary>
public sealed class VlcPlugin(WorkflowExecutionEngine engine, VlcProfile? profile = null)
    : ApplicationPluginBase(engine, (profile ?? VlcProfile.Default).ProcessName), IVlcPlugin
{
    private readonly VlcProfile _profile = profile ?? VlcProfile.Default;

    public Task<PluginActionResult> PlayAsync(CancellationToken cancellationToken = default) =>
        RunAsync("vlc-play", [FocusApplication(), PressKey(_profile.PlayPauseKey)], cancellationToken);

    public Task<PluginActionResult> PauseAsync(CancellationToken cancellationToken = default) =>
        RunAsync("vlc-pause", [FocusApplication(), PressKey(_profile.PlayPauseKey)], cancellationToken);

    public Task<PluginActionResult> StopAsync(CancellationToken cancellationToken = default) =>
        RunAsync("vlc-stop", [FocusApplication(), PressKey(_profile.StopKey)], cancellationToken);

    public Task<PluginActionResult> NextTrackAsync(CancellationToken cancellationToken = default) =>
        RunAsync("vlc-next-track", [FocusApplication(), PressKey(_profile.NextTrackKey)], cancellationToken);

    public Task<PluginActionResult> PreviousTrackAsync(CancellationToken cancellationToken = default) =>
        RunAsync("vlc-previous-track", [FocusApplication(), PressKey(_profile.PreviousTrackKey)], cancellationToken);

    public Task<PluginActionResult> ToggleMuteAsync(CancellationToken cancellationToken = default) =>
        RunAsync("vlc-toggle-mute", [FocusApplication(), PressKey(_profile.MuteKey)], cancellationToken);

    public Task<PluginActionResult> ToggleFullscreenAsync(CancellationToken cancellationToken = default) =>
        RunAsync("vlc-toggle-fullscreen", [FocusApplication(), PressKey(_profile.FullscreenKey)], cancellationToken);
}
