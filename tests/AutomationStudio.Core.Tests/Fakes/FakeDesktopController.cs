using AutomationStudio.Abstractions;

namespace AutomationStudio.Core.Tests.Fakes;

/// <summary>
/// Records every call it receives instead of touching real input devices, so the engine's
/// sequencing/retry/timeout/cancellation logic can be tested without a display or a real OS
/// process — exactly the platform-independence the design doc's section 42 architecture promises.
/// </summary>
public sealed class FakeDesktopController : IDesktopController
{
    public List<string> Calls { get; } = [];

    public Func<CancellationToken, Task>? OnClick { get; set; }
    public Func<CancellationToken, Task>? OnTypeText { get; set; }
    public Func<CancellationToken, Task>? OnOpenApplication { get; set; }

    private static string FormatPoint(ScreenPoint? position) =>
        position is { } p ? $"{p.X},{p.Y}" : "null";

    public Task MoveMouseAsync(ScreenPoint position, CancellationToken cancellationToken = default)
    {
        Calls.Add($"MoveMouse({position.X},{position.Y})");
        return Task.CompletedTask;
    }

    public async Task ClickAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Click({button},{FormatPoint(position)})");
        if (OnClick is not null)
        {
            await OnClick(cancellationToken);
        }
    }

    public Task DoubleClickAsync(ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        Calls.Add($"DoubleClick({FormatPoint(position)})");
        return Task.CompletedTask;
    }

    public Task MouseDownAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        Calls.Add($"MouseDown({button},{FormatPoint(position)})");
        return Task.CompletedTask;
    }

    public Task MouseUpAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default)
    {
        Calls.Add($"MouseUp({button},{FormatPoint(position)})");
        return Task.CompletedTask;
    }

    public Task ScrollAsync(int deltaX, int deltaY, CancellationToken cancellationToken = default)
    {
        Calls.Add($"Scroll({deltaX},{deltaY})");
        return Task.CompletedTask;
    }

    public async Task TypeTextAsync(string text, CancellationToken cancellationToken = default)
    {
        Calls.Add($"TypeText({text})");
        if (OnTypeText is not null)
        {
            await OnTypeText(cancellationToken);
        }
    }

    public Task PressKeyAsync(Key key, CancellationToken cancellationToken = default)
    {
        Calls.Add($"PressKey({key})");
        return Task.CompletedTask;
    }

    public Task KeyCombinationAsync(IReadOnlyList<Key> keys, CancellationToken cancellationToken = default)
    {
        Calls.Add($"KeyCombination({string.Join('+', keys)})");
        return Task.CompletedTask;
    }

    public async Task<int> OpenApplicationAsync(string path, string? arguments = null, CancellationToken cancellationToken = default)
    {
        Calls.Add($"OpenApplication({path},{arguments})");
        if (OnOpenApplication is not null)
        {
            await OnOpenApplication(cancellationToken);
        }

        return 1234;
    }

    public Task CloseApplicationAsync(string processName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"CloseApplication({processName})");
        return Task.CompletedTask;
    }

    public Task<bool> IsApplicationRunningAsync(string processName, CancellationToken cancellationToken = default)
    {
        Calls.Add($"IsApplicationRunning({processName})");
        return Task.FromResult(true);
    }
}
