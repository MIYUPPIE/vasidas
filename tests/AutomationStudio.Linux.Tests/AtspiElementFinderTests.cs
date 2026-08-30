using System.Diagnostics;
using AutomationStudio.Abstractions;
using AutomationStudio.Linux.Atspi;

namespace AutomationStudio.Linux.Tests;

/// <summary>
/// Exercises the real AT-SPI2 D-Bus pipeline (search, bounds, invoke) against a live GTK app rather
/// than a fake accessibility tree — the same "test against the real thing" discipline as
/// <see cref="ScreenshotCaptureTests"/>.
/// </summary>
/// <remarks>
/// Two things confirmed live on this dev machine while writing these tests, both surprising enough
/// to record:
/// <list type="bullet">
/// <item>gnome-font-viewer's own top-level frame reports a blank AT-SPI Name in this environment
/// (a remote-desktop/COSMIC session), so <c>ElementQuery.WindowTitleContains</c> narrowing — which
/// matches against that Name — can never find it here. These tests therefore match by
/// <c>Text</c> against real elements several levels deep in the app's tree instead: the "All Fonts"
/// group, and individual grid-cell font names such as "Aa AR PL UKai CN" (a font that happens to be
/// installed on this dev machine's base image).</item>
/// <item>Those grid cells implement the AT-SPI Action interface (confirmed via a live InvokeAsync
/// call), which is what led to widening <see cref="AtspiElementFinder"/>'s role mapping to cover
/// AT-SPI's "grid"/"grid cell" roles (GTK's FlowBox-style pickers), previously falling through to
/// <see cref="ElementType.Unknown"/>.</item>
/// </list>
/// </remarks>
public class AtspiElementFinderTests
{
    private const string TestFontName = "Aa AR PL UKai CN";

    private static bool CanRunLiveAtspiTest() =>
        Environment.GetEnvironmentVariable("DISPLAY") is not null
        && AtspiCapabilityCheck.IsToolkitAccessibilityEnabled() == true
        && IsOnPath("gnome-font-viewer");

    private static bool IsOnPath(string executable) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator)
            .Any(dir => File.Exists(Path.Combine(dir, executable)));

    [Fact]
    public async Task FindElementAsync_AgainstALiveGtkApp_FindsARealElementByText()
    {
        if (!CanRunLiveAtspiTest())
        {
            return;
        }

        await using var app = await LaunchedApp.StartAsync("gnome-font-viewer");
        var finder = new AtspiElementFinder();

        var found = await PollForElementAsync(finder, new ElementQuery(Text: "All Fonts"));

        Assert.NotNull(found);
        Assert.Equal(ElementType.Pane, found!.Type);
    }

    [Fact]
    public async Task FindElementAsync_ForANonexistentElement_ReturnsNull()
    {
        if (!CanRunLiveAtspiTest())
        {
            return;
        }

        await using var app = await LaunchedApp.StartAsync("gnome-font-viewer");
        var finder = new AtspiElementFinder();
        await PollForElementAsync(finder, new ElementQuery(Text: "All Fonts")); // wait for the tree to settle first.

        var found = await finder.FindElementAsync(new ElementQuery(Text: "Definitely Not A Real Element Name 12345"));

        Assert.Null(found);
    }

    /// <summary>
    /// Regression test for AtspiElementFinder's parallel-children tree walk (Phase 11 — DAS.md
    /// section 31/32's "deepen AT-SPI coverage"): confirmed live before this change that a full,
    /// no-match walk of gnome-font-viewer's font grid (~5800 accessible objects) took ~1.9-2.3s with
    /// the original sequential (one child at a time) traversal and ~250-285ms once children were
    /// explored concurrently via <see cref="Task.WhenAll"/> — same result, ~7-8x faster. A generous
    /// 1.5s bound (well under the old sequential baseline, with real margin for CI/machine variance)
    /// is enough to catch a regression back to sequential traversal without being a flaky timing test.
    /// </summary>
    [Fact]
    public async Task FindElementAsync_ForANonexistentElement_CompletesWellUnderTheOldSequentialBaseline()
    {
        if (!CanRunLiveAtspiTest())
        {
            return;
        }

        await using var app = await LaunchedApp.StartAsync("gnome-font-viewer");
        var finder = new AtspiElementFinder();
        await PollForElementAsync(finder, new ElementQuery(Text: "All Fonts")); // wait for the tree to settle, and warm the connection.

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var found = await finder.FindElementAsync(new ElementQuery(Text: "Definitely Not A Real Element Name 12345"));
        stopwatch.Stop();

        Assert.Null(found);
        Assert.True(stopwatch.ElapsedMilliseconds < 1500, $"expected a parallel tree walk well under 1.5s, took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task FindElementAsync_ThenInvokeAsync_ActivatesARealElement()
    {
        if (!CanRunLiveAtspiTest())
        {
            return;
        }

        await using var app = await LaunchedApp.StartAsync("gnome-font-viewer");
        var finder = new AtspiElementFinder();
        await PollForElementAsync(finder, new ElementQuery(Text: "All Fonts")); // wait for the tree to settle first.

        var cell = await finder.FindElementAsync(new ElementQuery(Text: TestFontName));
        if (cell is null)
        {
            return; // this font isn't installed on this machine; nothing to click.
        }

        Assert.Equal(ElementType.ListItem, cell.Type);

        // Throws ElementPatternNotSupportedException if this regresses — grid cells expose the
        // AT-SPI Action interface, confirmed live against this app.
        await finder.InvokeAsync(cell);
    }

    private static async Task<DetectedElement?> PollForElementAsync(AtspiElementFinder finder, ElementQuery query)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var found = await finder.FindElementAsync(query);
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(500);
        }

        return null;
    }

    /// <summary>Launches a process and guarantees it (and any children) are killed on disposal, even if the test fails.</summary>
    private sealed class LaunchedApp : IAsyncDisposable
    {
        private readonly Process _process;

        private LaunchedApp(Process process) => _process = process;

        public static async Task<LaunchedApp> StartAsync(string executable)
        {
            var process = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false })
                ?? throw new InvalidOperationException($"Failed to start '{executable}'.");
            await Task.Delay(2000); // give the app time to launch and register with AT-SPI.
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
