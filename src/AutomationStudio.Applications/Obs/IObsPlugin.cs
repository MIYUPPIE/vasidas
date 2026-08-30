namespace AutomationStudio.Applications.Obs;

/// <summary>High-level OBS Studio capabilities (DAS.md section 14) — the exact method set DAS.md's own worked example lists for OBS.</summary>
public interface IObsPlugin
{
    Task<PluginActionResult> StartRecordingAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> StopRecordingAsync(CancellationToken cancellationToken = default);

    Task<PluginActionResult> StartStreamingAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> StopStreamingAsync(CancellationToken cancellationToken = default);

    Task<PluginActionResult> SwitchSceneAsync(string sceneName, CancellationToken cancellationToken = default);
}
