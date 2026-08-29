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

    private readonly IDisposable? _disposable;

    private PlatformAutomationEnvironment(
        IDesktopController desktopController,
        IScreenshotService screenshotService,
        IWindowManager windowManager,
        ICapabilityProvider capabilityProvider,
        IDisposable? disposable)
    {
        DesktopController = desktopController;
        ScreenshotService = screenshotService;
        WindowManager = windowManager;
        CapabilityProvider = capabilityProvider;
        _disposable = disposable;
    }

    public static PlatformAutomationEnvironment Create()
    {
        if (OperatingSystem.IsWindows())
        {
            var env = AutomationStudio.Windows.WindowsAutomationEnvironment.Create();
            return new PlatformAutomationEnvironment(
                env.DesktopController, env.ScreenshotService, env.WindowManager, env.CapabilityProvider, disposable: null);
        }

        if (OperatingSystem.IsLinux())
        {
            var env = AutomationStudio.Linux.LinuxAutomationEnvironment.Create();
            return new PlatformAutomationEnvironment(
                env.DesktopController, env.ScreenshotService, env.WindowManager, env.CapabilityProvider, disposable: env);
        }

        throw new PlatformNotSupportedException(
            $"Desktop Automation Studio supports Windows and Linux; '{Environment.OSVersion.Platform}' is neither.");
    }

    public void Dispose() => _disposable?.Dispose();
}
