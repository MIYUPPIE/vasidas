using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using AutomationStudio.Abstractions;
using AutomationStudio.Windows.Win32;

namespace AutomationStudio.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsWindowManager : IWindowManager
{
    public Task<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WindowInfo>>(EnumerateVisibleWindows().Select(ToWindowInfo).ToList());

    public Task<bool> FocusWindowByTitleAsync(string titleContains, CancellationToken cancellationToken = default)
    {
        var match = EnumerateVisibleWindows()
            .FirstOrDefault(w => w.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(TryFocus(match.Handle));
    }

    public Task<bool> FocusWindowByProcessAsync(string processName, CancellationToken cancellationToken = default)
    {
        var match = EnumerateVisibleWindows()
            .FirstOrDefault(w => string.Equals(ResolveProcessName((int)w.ProcessId), processName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(TryFocus(match.Handle));
    }

    public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        var handle = NativeMethods.GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return Task.FromResult<WindowInfo?>(null);
        }

        var length = NativeMethods.GetWindowTextLength(handle);
        var builder = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(handle, builder, builder.Capacity);
        NativeMethods.GetWindowThreadProcessId(handle, out var processId);

        return Task.FromResult<WindowInfo?>(new WindowInfo(builder.ToString(), (int)processId, ResolveProcessName((int)processId)));
    }

    private static bool TryFocus(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        // Windows silently ignores SetForegroundWindow from a background process unless the
        // calling thread is attached to the current foreground thread's input queue — this dance
        // is the documented workaround, not defensive over-engineering.
        var foregroundWindow = NativeMethods.GetForegroundWindow();
        var foregroundThread = NativeMethods.GetWindowThreadProcessId(foregroundWindow, out _);
        var currentThread = NativeMethods.GetCurrentThreadId();
        var attached = foregroundThread != currentThread &&
                       NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);

        try
        {
            NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
            return NativeMethods.SetForegroundWindow(handle);
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }

    private static IEnumerable<(IntPtr Handle, string Title, uint ProcessId)> EnumerateVisibleWindows()
    {
        var windows = new List<(IntPtr, string, uint)>();

        NativeMethods.EnumWindows((handle, _) =>
        {
            if (!NativeMethods.IsWindowVisible(handle))
            {
                return true;
            }

            var length = NativeMethods.GetWindowTextLength(handle);
            if (length == 0)
            {
                return true;
            }

            var builder = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(handle, builder, builder.Capacity);
            NativeMethods.GetWindowThreadProcessId(handle, out var processId);

            windows.Add((handle, builder.ToString(), processId));
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static WindowInfo ToWindowInfo((IntPtr Handle, string Title, uint ProcessId) window) =>
        new(window.Title, (int)window.ProcessId, ResolveProcessName((int)window.ProcessId));

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
