using System.Runtime.Versioning;
using AutomationStudio.Abstractions;

namespace AutomationStudio.Windows;

/// <summary>
/// Composition root for the Windows backend. Unlike Linux there is no persistent connection to
/// share (SendInput/GDI/EnumWindows are all stateless Win32 calls), so this only exists for
/// symmetry with <c>AutomationStudio.Linux.LinuxAutomationEnvironment</c> and to give Phase 2's DI
/// wiring one consistent shape to construct on either platform.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsAutomationEnvironment
{
    public IDesktopController DesktopController { get; } = new WindowsDesktopController();
    public IScreenshotService ScreenshotService { get; } = new WindowsScreenshotService();
    public IWindowManager WindowManager { get; } = new WindowsWindowManager();
    public ICapabilityProvider CapabilityProvider { get; } = new WindowsCapabilityProvider();

    public static WindowsAutomationEnvironment Create() => new();
}
