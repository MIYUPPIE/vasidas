using System.Diagnostics;
using AutomationStudio.Abstractions;
using AutomationStudio.Linux.X11;

namespace AutomationStudio.Linux;

public sealed class LinuxWindowManager : IWindowManager
{
    private readonly X11WindowManager? _windowManager;

    internal LinuxWindowManager(X11Display? display)
    {
        _windowManager = display is null ? null : new X11WindowManager(display);
    }

    private X11WindowManager Require() =>
        _windowManager ?? throw new CapabilityUnavailableException(
            "Window management",
            "no X11 display reachable (pure Wayland session without XWayland).");

    public Task<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken = default)
    {
        var windows = Require().ListWindows()
            .Select(w => new WindowInfo(w.Title, w.ProcessId, ResolveProcessName(w.ProcessId)))
            .ToList();

        return Task.FromResult<IReadOnlyList<WindowInfo>>(windows);
    }

    public Task<bool> FocusWindowByTitleAsync(string titleContains, CancellationToken cancellationToken = default)
    {
        var manager = Require();
        var match = manager.ListWindows()
            .FirstOrDefault(w => w.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase));

        if (match.Window == IntPtr.Zero)
        {
            return Task.FromResult(false);
        }

        manager.Focus(match.Window);
        return Task.FromResult(true);
    }

    public Task<bool> FocusWindowByProcessAsync(string processName, CancellationToken cancellationToken = default)
    {
        var manager = Require();
        var match = manager.ListWindows()
            .FirstOrDefault(w => string.Equals(ResolveProcessName(w.ProcessId), processName, StringComparison.OrdinalIgnoreCase));

        if (match.Window == IntPtr.Zero)
        {
            return Task.FromResult(false);
        }

        manager.Focus(match.Window);
        return Task.FromResult(true);
    }

    public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var active = Require().GetActiveWindow();
        return Task.FromResult(active is null
            ? null
            : (WindowInfo?)new WindowInfo(active.Value.Title, active.Value.ProcessId, ResolveProcessName(active.Value.ProcessId)));
    }

    private static string ResolveProcessName(int processId)
    {
        if (processId <= 0)
        {
            return string.Empty;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }
}
