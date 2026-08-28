using System.Diagnostics;
using System.Runtime.Versioning;
using AutomationStudio.Abstractions;
using AutomationStudio.Windows.Input;

namespace AutomationStudio.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsDesktopController : IDesktopController
{
    private readonly WindowsInputController _input = new();

    public Task MoveMouseAsync(ScreenPoint position, CancellationToken cancellationToken = default)
    {
        _input.MoveMouse(position);
        return Task.CompletedTask;
    }

    public Task ClickAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        _input.Click(button, position);
        return Task.CompletedTask;
    }

    public Task DoubleClickAsync(ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        _input.DoubleClick(position);
        return Task.CompletedTask;
    }

    public Task MouseDownAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        _input.MouseDown(button, position);
        return Task.CompletedTask;
    }

    public Task MouseUpAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        _input.MouseUp(button, position);
        return Task.CompletedTask;
    }

    public Task ScrollAsync(int deltaX, int deltaY, CancellationToken cancellationToken = default)
    {
        _input.Scroll(deltaX, deltaY);
        return Task.CompletedTask;
    }

    public Task TypeTextAsync(string text, CancellationToken cancellationToken = default)
    {
        _input.TypeText(text);
        return Task.CompletedTask;
    }

    public Task PressKeyAsync(Key key, CancellationToken cancellationToken = default)
    {
        _input.PressKey(key);
        return Task.CompletedTask;
    }

    public Task KeyCombinationAsync(IReadOnlyList<Key> keys, CancellationToken cancellationToken = default)
    {
        _input.KeyCombination(keys);
        return Task.CompletedTask;
    }

    public Task<int> OpenApplicationAsync(string path, string? arguments = null, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(path)
        {
            UseShellExecute = true
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
