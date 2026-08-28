using System.Runtime.Versioning;
using AutomationStudio.Abstractions;

namespace AutomationStudio.Windows;

/// <summary>
/// Windows has no analogue to Wayland's input-injection restriction (section 31), so every
/// capability is simply Available. Detection still runs, matching section 32's flow, so the UI
/// reports the same shape of information on both platforms.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCapabilityProvider : ICapabilityProvider
{
    public AutomationCapabilities DetectCapabilities() => new()
    {
        OperatingSystem = "Windows",
        DesktopEnvironment = null,
        DisplayServer = DisplayServerKind.Windows,
        MouseInjection = CapabilityState.Available,
        KeyboardInjection = CapabilityState.Available,
        ScreenCapture = CapabilityState.Available,
        WindowManagement = CapabilityState.Available,
        Notes = []
    };
}
