using System.Diagnostics;
using AutomationStudio.Abstractions;
using AutomationStudio.Linux.X11;

namespace AutomationStudio.Linux;

/// <summary>
/// Linux <see cref="IDesktopController"/>. Process management (launch/close) is always available —
/// it goes through the BCL, not the display server. Mouse/keyboard injection needs X11 (native or
/// XWayland); when neither is reachable, every input method throws
/// <see cref="CapabilityUnavailableException"/> instead of silently doing nothing.
/// </summary>
public sealed class LinuxDesktopController : IDesktopController
{
    private readonly X11InputController? _input;

    internal LinuxDesktopController(X11Display? display)
    {
        _input = display is null ? null : new X11InputController(display);
    }

    private X11InputController RequireInput() =>
        _input ?? throw new CapabilityUnavailableException(
            "Mouse/keyboard injection",
            "no X11 display reachable (pure Wayland session without XWayland). See section 31 of the design doc.");

    public Task MoveMouseAsync(ScreenPoint position, CancellationToken cancellationToken = default)
    {
        RequireInput().MoveMouse(position);
        return Task.CompletedTask;
    }

    public Task ClickAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        RequireInput().Click(button, position);
        return Task.CompletedTask;
    }

    public Task DoubleClickAsync(ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        RequireInput().DoubleClick(position);
        return Task.CompletedTask;
    }

    public Task MouseDownAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        RequireInput().MouseDown(button, position);
        return Task.CompletedTask;
    }

    public Task MouseUpAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        RequireInput().MouseUp(button, position);
        return Task.CompletedTask;
    }

    public Task ScrollAsync(int deltaX, int deltaY, CancellationToken cancellationToken = default)
    {
        RequireInput().Scroll(deltaX, deltaY);
        return Task.CompletedTask;
    }

    public Task TypeTextAsync(string text, CancellationToken cancellationToken = default)
    {
        RequireInput().TypeText(text);
        return Task.CompletedTask;
    }

    public Task PressKeyAsync(Key key, CancellationToken cancellationToken = default)
    {
        RequireInput().PressKey(key);
        return Task.CompletedTask;
    }

    public Task KeyCombinationAsync(IReadOnlyList<Key> keys, CancellationToken cancellationToken = default)
    {
        RequireInput().KeyCombination(keys);
        return Task.CompletedTask;
    }

    public Task<int> OpenApplicationAsync(string path, string? arguments = null, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(path)
        {
            UseShellExecute = false
        };

        if (!string.IsNullOrEmpty(arguments))
        {
            startInfo.Arguments = arguments;
        }

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process '{path}'.");

        return Task.FromResult(process.Id);
    }

    public async Task CloseApplicationAsync(string processName, CancellationToken cancellationToken = default)
    {
        var name = NormalizeProcessName(processName);
        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process)
            {
                try
                {
                    if (!process.CloseMainWindow())
                    {
                        process.Kill(entireProcessTree: true);
                    }

                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    // Process already exited between the lookup and the close attempt.
                }
            }
        }
    }

    public Task<bool> IsApplicationRunningAsync(string processName, CancellationToken cancellationToken = default)
    {
        var name = NormalizeProcessName(processName);
        var processes = Process.GetProcessesByName(name);
        try
        {
            return Task.FromResult(processes.Length > 0);
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static string NormalizeProcessName(string processName) =>
        processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;
}
