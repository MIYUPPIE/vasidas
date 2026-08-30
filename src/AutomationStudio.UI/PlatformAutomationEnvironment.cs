using AutomationStudio.Abstractions;

namespace AutomationStudio.UI;

/// <summary>
/// Picks the Windows or Linux backend at runtime (section 3: the workflow engine and UI never know
/// which platform they're on beyond this one composition point). The UI project references both
/// platform assemblies — each compiles everywhere (plain P/Invoke declarations, no OS-specific SDK
/// needed to build), so this only ever instantiates whichever one actually matches
/// <see cref="OperatingSystem"/> at runtime; the other's types are simply never touched.
/// </summary>
public sealed class PlatformAutomationEnvironment : IDisposable
{
    public IDesktopController DesktopController { get; }
    public IScreenshotService ScreenshotService { get; }
    public IWindowManager WindowManager { get; }
    public ICapabilityProvider CapabilityProvider { get; }
    public IElementFinder ElementFinder { get; }

    /// <summary>Null on Linux when no X11 display is reachable at all (pure Wayland, no XWayland) — mirrors <c>LinuxAutomationEnvironment</c>'s own nullability rather than throwing, so UI code (the "teach new command" feature) can check before using it. Always non-null on Windows.</summary>
    public IInputRecorder? InputRecorder { get; }

    private readonly IDisposable? _disposable;

    private PlatformAutomationEnvironment(
        IDesktopController desktopController,
        IScreenshotService screenshotService,
        IWindowManager windowManager,
        ICapabilityProvider capabilityProvider,
        IElementFinder elementFinder,
        IInputRecorder? inputRecorder,
        IDisposable? disposable)
    {
        DesktopController = desktopController;
        ScreenshotService = screenshotService;
        WindowManager = windowManager;
        CapabilityProvider = capabilityProvider;
        ElementFinder = elementFinder;
        InputRecorder = inputRecorder;
        _disposable = disposable;
    }

    public static PlatformAutomationEnvironment Create()
    {
        if (OperatingSystem.IsWindows())
        {
            var env = AutomationStudio.Windows.WindowsAutomationEnvironment.Create();
            return new PlatformAutomationEnvironment(
                env.DesktopController, env.ScreenshotService, env.WindowManager, env.CapabilityProvider, env.ElementFinder, env.InputRecorder, disposable: null);
        }

        if (OperatingSystem.IsLinux())
        {
            var env = AutomationStudio.Linux.LinuxAutomationEnvironment.Create();
            return new PlatformAutomationEnvironment(
                env.DesktopController, env.ScreenshotService, env.WindowManager, env.CapabilityProvider, env.ElementFinder, env.InputRecorder, disposable: env);
        }

        throw new PlatformNotSupportedException(
            $"Desktop Automation Studio supports Windows and Linux; '{Environment.OSVersion.Platform}' is neither.");
    }

    public void Dispose() => _disposable?.Dispose();
}
