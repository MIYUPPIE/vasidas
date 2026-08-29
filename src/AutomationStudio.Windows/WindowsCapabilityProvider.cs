using System.Runtime.Versioning;
using AutomationStudio.Abstractions;
using AutomationStudio.Vision.OCR;

namespace AutomationStudio.Windows;

/// <summary>
/// Windows has no analogue to Wayland's input-injection restriction (section 31), so most
/// capabilities are simply Available. Detection still runs, matching section 32's flow, so the UI
/// reports the same shape of information on both platforms. OCR is the one exception worth actually
/// checking here too: model/CLI deployment is a packaging concern, not an OS restriction, and can be
/// missing on Windows exactly like it can on Linux.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCapabilityProvider : ICapabilityProvider
{
    public AutomationCapabilities DetectCapabilities()
    {
        var ocrAvailable = OcrCapabilityCheck.IsAvailable();
        return new AutomationCapabilities
        {
            OperatingSystem = "Windows",
            DesktopEnvironment = null,
            DisplayServer = DisplayServerKind.Windows,
            MouseInjection = CapabilityState.Available,
            KeyboardInjection = CapabilityState.Available,
            ScreenCapture = CapabilityState.Available,
            WindowManagement = CapabilityState.Available,
            ElementFinding = CapabilityState.Available,
            Ocr = ocrAvailable ? CapabilityState.Available : CapabilityState.Restricted,
            Notes = ocrAvailable
                ? []
                : ["OCR is unavailable: PaddleOCR's bundled model files were not found next to the app, and the 'tesseract' CLI fallback is not installed."]
        };
    }
}
