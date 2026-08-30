using AutomationStudio.Applications.Plugins;
using AutomationStudio.Core.Execution;

namespace AutomationStudio.Applications.Obs;

/// <summary>Profile-driven OBS automation (DAS.md section 14) — every capability clicks the matching on-screen control via the design doc section 12 resolution chain (see <see cref="ObsProfile"/> for why OBS uses button text rather than keyboard shortcuts) and runs it through the same generic <see cref="WorkflowExecutionEngine"/> everything else uses.</summary>
public sealed class ObsPlugin(WorkflowExecutionEngine engine, ObsProfile? profile = null)
    : ApplicationPluginBase(engine, (profile ?? ObsProfile.Default).ProcessName), IObsPlugin
{
    private readonly ObsProfile _profile = profile ?? ObsProfile.Default;

    public Task<PluginActionResult> StartRecordingAsync(CancellationToken cancellationToken = default) =>
        RunAsync("obs-start-recording", [FocusApplication(), ClickButton(_profile.StartRecordingButtonText)], cancellationToken);

    public Task<PluginActionResult> StopRecordingAsync(CancellationToken cancellationToken = default) =>
        RunAsync("obs-stop-recording", [FocusApplication(), ClickButton(_profile.StopRecordingButtonText)], cancellationToken);

    public Task<PluginActionResult> StartStreamingAsync(CancellationToken cancellationToken = default) =>
        RunAsync("obs-start-streaming", [FocusApplication(), ClickButton(_profile.StartStreamingButtonText)], cancellationToken);

    public Task<PluginActionResult> StopStreamingAsync(CancellationToken cancellationToken = default) =>
        RunAsync("obs-stop-streaming", [FocusApplication(), ClickButton(_profile.StopStreamingButtonText)], cancellationToken);

    public Task<PluginActionResult> SwitchSceneAsync(string sceneName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return Task.FromResult(PluginActionResult.Fail("No scene name was given."));
        }

        return RunAsync("obs-switch-scene", [FocusApplication(), ClickButton(sceneName)], cancellationToken);
    }
}
