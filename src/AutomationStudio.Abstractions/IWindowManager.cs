namespace AutomationStudio.Abstractions;

public sealed record WindowInfo(string Title, int ProcessId, string ProcessName);

/// <summary>
/// Minimal cross-platform window focus/lookup. Full window enumeration and UI element trees are
/// Phase 3 (UI Automation / AT-SPI); Phase 1 only needs enough to bring an application's window to
/// the foreground after launching it, which mouse/keyboard automation depends on.
/// </summary>
public interface IWindowManager
{
    Task<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken = default);

    /// <summary>Brings the first window whose title contains <paramref name="titleContains"/> to the foreground.</summary>
    Task<bool> FocusWindowByTitleAsync(string titleContains, CancellationToken cancellationToken = default);

    /// <summary>Brings the first window owned by <paramref name="processName"/> to the foreground.</summary>
    Task<bool> FocusWindowByProcessAsync(string processName, CancellationToken cancellationToken = default);

    /// <summary>The currently-focused window, or null if none can be determined. Used by the Recorder/Teaching system (DAS.md section 25) to record which application a taught workflow targets.</summary>
    Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default);
}
