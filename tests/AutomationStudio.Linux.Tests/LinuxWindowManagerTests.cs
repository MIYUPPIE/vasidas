using System.Diagnostics;
using AutomationStudio.Linux;
using AutomationStudio.Linux.X11;

namespace AutomationStudio.Linux.Tests;

/// <summary>
/// Exercises the real EWMH window-focus path (Phase 11 — DAS.md section 31's "deepen X11 coverage")
/// against real, separately-launched GTK apps, the same "test against the real thing" discipline as
/// <see cref="AtspiElementFinderTests"/>. <see cref="X11WindowManager.Focus"/> now sends the real
/// <c>_NET_ACTIVE_WINDOW</c> client message (not just <c>XRaiseWindow</c>/<c>XSetInputFocus</c>
/// called directly) and verifies success by polling <c>_NET_ACTIVE_WINDOW</c> back — these tests are
/// what actually prove that round-trip works, not just that the P/Invoke call compiles.
/// </summary>
/// <remarks>
/// Confirmed live on this dev machine while writing these tests, surprising enough to record here
/// (also documented on <see cref="LinuxCapabilityProvider"/>): a plain <c>gnome-font-viewer</c>/
/// <c>gnome-system-monitor</c> launch on this COSMIC/Wayland session renders natively via Wayland,
/// which means it never appears in <c>_NET_CLIENT_LIST</c> at all — <c>xprop -root _NET_CLIENT_LIST</c>
/// showed only an already-open, XWayland-backed VLC window, with zero trace of either freshly-launched
/// GTK app. Setting <c>GDK_BACKEND=x11</c> on the launched process forces XWayland rendering, which
/// makes it show up correctly with the right PID. This is the real, live-verified shape of DAS.md
/// section 31's "Wayland places stronger restrictions" warning: it's not just input injection that's
/// restricted on Wayland, <c>X11WindowManager</c>'s entire window listing/focus/EWMH mechanism is
/// completely blind to any window that isn't XWayland-backed — which is most modern GTK4/GNOME apps
/// by default. These tests force <c>GDK_BACKEND=x11</c> specifically to test what
/// <see cref="X11WindowManager"/> is capable of seeing; they intentionally do NOT prove it can find a
/// natively-Wayland-rendered instance of the same app, because it provably cannot.
/// </remarks>
public class LinuxWindowManagerTests
{
    private static bool CanRunLiveWindowManagerTest() =>
        Environment.GetEnvironmentVariable("DISPLAY") is not null
        && IsOnPath("gnome-font-viewer")
        && IsOnPath("gnome-system-monitor");

    private static bool IsOnPath(string executable) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator)
            .Any(dir => File.Exists(Path.Combine(dir, executable)));

    [Fact]
    public async Task FocusWindowByProcessAsync_ActuallySwitchesFocus_ConfirmedByReadingActiveWindowBack()
    {
        if (!CanRunLiveWindowManagerTest())
        {
            return;
        }

        using var display = X11Display.TryOpen();
        Assert.NotNull(display);

        await using var fontViewer = await LaunchedApp.StartAsync("gnome-font-viewer");
        await using var systemMonitor = await LaunchedApp.StartAsync("gnome-system-monitor");
        // Launching gnome-system-monitor second means it's very likely the active window already —
        // the real test is switching focus BACK to gnome-font-viewer and confirming the window
        // manager actually honoured that, not merely that it was already focused.
        var manager = new LinuxWindowManager(display);

        var focused = await manager.FocusWindowByProcessAsync("gnome-font-viewer");

        Assert.True(focused, "the window manager did not report gnome-font-viewer as focused within the poll timeout");
        var active = await manager.GetActiveWindowAsync();
        Assert.NotNull(active);
        Assert.Equal("gnome-font-viewer", active!.ProcessName);
    }

    [Fact]
    public async Task FocusWindowByProcessAsync_ForANonexistentProcess_ReturnsFalseWithoutTouchingAnyRealWindow()
    {
        if (!CanRunLiveWindowManagerTest())
        {
            return;
        }

        using var display = X11Display.TryOpen();
        Assert.NotNull(display);
        var manager = new LinuxWindowManager(display);

        var focused = await manager.FocusWindowByProcessAsync("definitely-not-a-real-process-xyz");

        Assert.False(focused);
    }

    [Fact]
    public async Task FocusWindowByTitleAsync_ThenGetActiveWindowAsync_AgreeOnTheSameRealWindow()
    {
        if (!CanRunLiveWindowManagerTest())
        {
            return;
        }

        using var display = X11Display.TryOpen();
        Assert.NotNull(display);

        await using var fontViewer = await LaunchedApp.StartAsync("gnome-font-viewer");
        await using var systemMonitor = await LaunchedApp.StartAsync("gnome-system-monitor");
        var manager = new LinuxWindowManager(display);

        var windows = await manager.ListWindowsAsync();
        var fontViewerWindow = windows.FirstOrDefault(w => w.ProcessName == "gnome-font-viewer");
        Assert.NotNull(fontViewerWindow);

        var focused = await manager.FocusWindowByTitleAsync(fontViewerWindow!.Title);

        Assert.True(focused);
        var active = await manager.GetActiveWindowAsync();
        Assert.Equal(fontViewerWindow.ProcessName, active?.ProcessName);
    }

    /// <summary>Launches a process and guarantees it (and any children) are killed on disposal, even
    /// if the test fails — mirrors <see cref="AtspiElementFinderTests"/>'s own helper, plus
    /// <c>GDK_BACKEND=x11</c> so the launched GTK app is actually visible to X11/EWMH (see this
    /// class's own remarks) rather than rendering natively via Wayland and staying invisible to
    /// everything <see cref="X11WindowManager"/> reads.</summary>
    private sealed class LaunchedApp : IAsyncDisposable
    {
        private readonly Process _process;

        private LaunchedApp(Process process) => _process = process;

        public static async Task<LaunchedApp> StartAsync(string executable)
        {
            var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
            startInfo.Environment["GDK_BACKEND"] = "x11";
            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start '{executable}'.");
            await Task.Delay(2000); // give the app time to launch, create a window, and be picked up by the window manager.
            return new LaunchedApp(process);
        }

        public ValueTask DisposeAsync()
        {
            try { _process.Kill(entireProcessTree: true); } catch { /* best-effort cleanup */ }
            _process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
