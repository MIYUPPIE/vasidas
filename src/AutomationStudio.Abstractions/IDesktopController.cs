namespace AutomationStudio.Abstractions;

/// <summary>
/// Platform-specific input and process control. Windows and Linux each provide their own
/// implementation; the workflow engine only ever talks to this interface (section 3 / 42 of the
/// design doc: the engine must never know how an action is actually carried out).
/// </summary>
public interface IDesktopController
{
    Task MoveMouseAsync(ScreenPoint position, CancellationToken cancellationToken = default);

    Task ClickAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default);

    Task DoubleClickAsync(ScreenPoint? position = null, CancellationToken cancellationToken = default);

    Task MouseDownAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default);

    Task MouseUpAsync(MouseButton button, ScreenPoint? position = null, CancellationToken cancellationToken = default);

    Task ScrollAsync(int deltaX, int deltaY, CancellationToken cancellationToken = default);

    Task TypeTextAsync(string text, CancellationToken cancellationToken = default);

    Task PressKeyAsync(Key key, CancellationToken cancellationToken = default);

    Task KeyCombinationAsync(IReadOnlyList<Key> keys, CancellationToken cancellationToken = default);

    /// <summary>Launches an application. Returns the OS process id.</summary>
    Task<int> OpenApplicationAsync(string path, string? arguments = null, CancellationToken cancellationToken = default);

    /// <summary>Requests every process matching <paramref name="processName"/> to close.</summary>
    Task CloseApplicationAsync(string processName, CancellationToken cancellationToken = default);

    /// <summary>True if a process with this name is currently running.</summary>
    Task<bool> IsApplicationRunningAsync(string processName, CancellationToken cancellationToken = default);
}
