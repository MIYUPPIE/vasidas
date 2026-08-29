using AutomationStudio.Abstractions;
using AutomationStudio.Linux.Atspi;
using AutomationStudio.Linux.X11;

namespace AutomationStudio.Linux;

/// <summary>
/// Composition root for the Linux backend. Opens $DISPLAY once and hands the same connection to
/// the desktop controller, screenshot service and window manager, rather than each opening its own
/// (X11Display is internal specifically so nothing outside this assembly can end up managing a
/// connection's lifetime incorrectly). Capability detection is intentionally independent — it is a
/// one-off startup check, not a hot path, so it opens (and closes) its own short-lived connection.
/// </summary>
public sealed class LinuxAutomationEnvironment : IDisposable
{
    private readonly X11Display? _display;

    public IDesktopController DesktopController { get; }
    public IScreenshotService ScreenshotService { get; }
    public IWindowManager WindowManager { get; }
    public ICapabilityProvider CapabilityProvider { get; }

    public IElementFinder ElementFinder { get; }

    private LinuxAutomationEnvironment(X11Display? display)
    {
        _display = display;
        DesktopController = new LinuxDesktopController(display);
        ScreenshotService = new LinuxScreenshotService(display);
        WindowManager = new LinuxWindowManager(display);
        CapabilityProvider = new LinuxCapabilityProvider();
        ElementFinder = new AtspiElementFinder();
    }

    public static LinuxAutomationEnvironment Create() => new(X11Display.TryOpen());

    public void Dispose() => _display?.Dispose();
}
