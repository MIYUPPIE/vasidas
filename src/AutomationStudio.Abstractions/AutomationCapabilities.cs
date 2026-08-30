namespace AutomationStudio.Abstractions;

public enum DisplayServerKind
{
    Unknown,
    Windows,
    X11,
    Wayland
}

public enum CapabilityState
{
    Available,
    Restricted,
    Unavailable
}

/// <summary>
/// What this machine can actually do, decided once at startup (section 32). Wayland sessions in
/// particular restrict raw input injection (section 31); the application must report that clearly
/// rather than silently failing mid-workflow.
/// </summary>
public sealed record AutomationCapabilities
{
    public required string OperatingSystem { get; init; }
    public required string? DesktopEnvironment { get; init; }
    public required DisplayServerKind DisplayServer { get; init; }

    public required CapabilityState MouseInjection { get; init; }
    public required CapabilityState KeyboardInjection { get; init; }
    public required CapabilityState ScreenCapture { get; init; }
    public required CapabilityState WindowManagement { get; init; }

    /// <summary>Native UI element finding (section 10/42): Windows UI Automation, Linux AT-SPI.</summary>
    public required CapabilityState ElementFinding { get; init; }

    /// <summary>Text recognition (section 11/32): PaddleOCR primary, Tesseract fallback. Identical on both platforms.</summary>
    public required CapabilityState Ocr { get; init; }

    /// <summary>Global input capture for the Recorder/Teaching system (DAS.md section 25): Windows low-level hooks, Linux the X11 RECORD extension.</summary>
    public required CapabilityState InputRecording { get; init; }

    /// <summary>Human-readable notes explaining any Restricted/Unavailable capability, for the UI and logs.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}

/// <summary>Detects what this machine's platform and session actually support.</summary>
public interface ICapabilityProvider
{
    AutomationCapabilities DetectCapabilities();
}
