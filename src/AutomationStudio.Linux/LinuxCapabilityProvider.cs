using AutomationStudio.Abstractions;
using AutomationStudio.Linux.Atspi;
using AutomationStudio.Linux.X11;
using AutomationStudio.Vision.OCR;

namespace AutomationStudio.Linux;

/// <summary>
/// Implements the capability-detection flow from section 32: OS → desktop environment → display
/// server → available automation APIs. Run once at startup so the UI (and the workflow author) can
/// see up front what this session can actually do, rather than discovering it mid-workflow.
/// </summary>
public sealed class LinuxCapabilityProvider : ICapabilityProvider
{
    public AutomationCapabilities DetectCapabilities()
    {
        var desktopEnvironment = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        var displayServer = DetectDisplayServer();
        var notes = new List<string>();

        using var display = X11Display.TryOpen();

        CapabilityState inputState;
        if (display is null)
        {
            inputState = CapabilityState.Restricted;
            notes.Add(displayServer == DisplayServerKind.Wayland
                ? "No XWayland display reachable: this is a pure-Wayland session, which restricts raw input injection by design (design doc section 31)."
                : "No X11 display reachable at $DISPLAY.");
        }
        else if (!display.HasXTest())
        {
            inputState = CapabilityState.Restricted;
            notes.Add("X11 display found but the XTest extension is not available; mouse/keyboard injection needs XTest.");
        }
        else
        {
            inputState = CapabilityState.Available;
            if (displayServer == DisplayServerKind.Wayland)
            {
                notes.Add("Running over XWayland: input reaches native Wayland windows via the compositor's XWayland integration, which most compositors support for pointer/keyboard but may not cover every application.");
            }
        }

        // XGetImage cannot read a Wayland compositor's framebuffer through XWayland's root window
        // (confirmed live: it returns BadMatch on this machine even for a 1x1 region), so X11 being
        // reachable does NOT mean screen capture will work — LinuxScreenshotService always falls
        // back to the xdg-desktop-portal Screenshot API, which only needs a session D-Bus bus.
        var hasSessionBus = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS"))
            || File.Exists(Path.Combine(Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "/run/user/0", "bus"));

        CapabilityState screenCaptureState;
        if (display is not null)
        {
            screenCaptureState = CapabilityState.Available;
        }
        else if (hasSessionBus)
        {
            screenCaptureState = CapabilityState.Available;
            notes.Add("Screen capture will use the xdg-desktop-portal Screenshot API (no X11 display); the first call on a fresh system may show a one-time permission dialog.");
        }
        else
        {
            screenCaptureState = CapabilityState.Restricted;
            notes.Add("Screen capture is unavailable: no X11 display and no session D-Bus bus for the xdg-desktop-portal fallback.");
        }

        var elementFindingState = AtspiCapabilityCheck.IsToolkitAccessibilityEnabled() switch
        {
            true => CapabilityState.Available,
            false => Restricted("Element finding needs accessibility enabled: 'gsettings set org.gnome.desktop.interface toolkit-accessibility true' (GNOME/COSMIC) — apps started before that won't retroactively expose their UI tree either."),
            null => Restricted("Could not confirm accessibility is enabled (non-GNOME desktop, or 'gsettings' unavailable); AT-SPI element finding may return nothing even though the D-Bus calls succeed.")
        };

        var ocrState = OcrCapabilityCheck.ArePrimaryModelsPresent()
            ? CapabilityState.Available
            : TesseractOcrEngine.IsAvailable()
                ? CapabilityState.Available
                : Restricted("OCR is unavailable: PaddleOCR's bundled model files were not found next to the app, and the 'tesseract' CLI fallback is not installed.");

        var inputRecordingState = display is not null && display.HasRecord()
            ? CapabilityState.Available
            : Restricted(display is null
                ? "Input recording is unavailable: no X11 display reachable."
                : "Input recording is unavailable: the X11 RECORD extension is not present on this display.");

        return new AutomationCapabilities
        {
            OperatingSystem = "Linux",
            DesktopEnvironment = string.IsNullOrEmpty(desktopEnvironment) ? null : desktopEnvironment,
            DisplayServer = displayServer,
            MouseInjection = inputState,
            KeyboardInjection = inputState,
            ScreenCapture = screenCaptureState,
            WindowManagement = display is null ? CapabilityState.Restricted : CapabilityState.Available,
            ElementFinding = elementFindingState,
            Ocr = ocrState,
            InputRecording = inputRecordingState,
            Notes = notes
        };

        CapabilityState Restricted(string note)
        {
            notes.Add(note);
            return CapabilityState.Restricted;
        }
    }

    private static DisplayServerKind DetectDisplayServer()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            return DisplayServerKind.Wayland;
        }

        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        if (string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            return DisplayServerKind.Wayland;
        }

        if (string.Equals(sessionType, "x11", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
        {
            return DisplayServerKind.X11;
        }

        return DisplayServerKind.Unknown;
    }
}
