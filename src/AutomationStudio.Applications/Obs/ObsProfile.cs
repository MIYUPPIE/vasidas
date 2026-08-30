namespace AutomationStudio.Applications.Obs;

/// <summary>
/// Everything about OBS Studio the plugin needs beyond automation-engine logic (DAS.md section 13).
/// Unlike VideoPsalm, OBS ships with <b>no default keyboard shortcuts for recording or streaming at
/// all</b> — confirmed via research (OBS's own forum: "OBS has no universal defaults, hotkeys are
/// user-defined"; Settings &gt; Hotkeys starts with these actions unbound). So this profile drives
/// button clicks via the design doc section 12 Target Resolution Engine instead of keyboard
/// shortcuts — OBS's control labels are stable and well-known regardless of hotkey configuration,
/// and the Start/Stop Recording (and Streaming) controls are documented to toggle their own visible
/// label between the two states, which is exactly what lets one button-text click reliably mean
/// "start" only while OBS is actually stopped.
/// </summary>
public sealed record ObsProfile
{
    public required string ProcessName { get; init; }

    public required string StartRecordingButtonText { get; init; }
    public required string StopRecordingButtonText { get; init; }

    public required string StartStreamingButtonText { get; init; }
    public required string StopStreamingButtonText { get; init; }

    public static ObsProfile Default { get; } = new()
    {
        ProcessName = "obs",
        StartRecordingButtonText = "Start Recording",
        StopRecordingButtonText = "Stop Recording",
        StartStreamingButtonText = "Start Streaming",
        StopStreamingButtonText = "Stop Streaming",
    };
}
