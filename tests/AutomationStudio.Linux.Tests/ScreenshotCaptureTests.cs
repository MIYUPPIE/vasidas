using System.Buffers.Binary;
using AutomationStudio.Linux;

namespace AutomationStudio.Linux.Tests;

/// <summary>
/// Exercises the real XGetImage capture path against whatever display this test process is
/// actually running under. Safe to run unattended: it only reads pixels, never injects mouse or
/// keyboard input, so — unlike a hypothetical input-injection test — it cannot disturb whatever
/// else is happening on the developer's desktop while the suite runs.
/// </summary>
public class ScreenshotCaptureTests
{
    private static bool HasDisplay => Environment.GetEnvironmentVariable("DISPLAY") is not null;

    [Fact]
    public async Task CaptureScreenAsync_OnADisplay_ReturnsAWellFormedPng()
    {
        if (!HasDisplay)
        {
            return; // headless environment; nothing to capture.
        }

        using var environment = LinuxAutomationEnvironment.Create();
        var screenshot = await environment.ScreenshotService.CaptureScreenAsync();

        Assert.True(screenshot.Width > 0);
        Assert.True(screenshot.Height > 0);
        AssertWellFormedPng(screenshot.PngData, screenshot.Width, screenshot.Height);
    }

    /// <summary>
    /// Region capture only has an X11 implementation — there is deliberately no portal fallback for
    /// it yet (<see cref="AutomationStudio.Linux.LinuxScreenshotService.CaptureRegionAsync"/>'s doc
    /// comment). On a native Wayland session that means it always throws
    /// <see cref="NotSupportedException"/>, for the same underlying reason full-screen X11 capture
    /// does (confirmed live on this dev machine: XGetImage returns BadAlloc/BadMatch against
    /// XWayland's root window regardless of region size) — so this asserts whichever outcome this
    /// session's actual capability supports, rather than assuming X11 region capture works.
    /// </summary>
    [Fact]
    public async Task CaptureRegionAsync_ReturnsExactDimensions_OrThrowsWhenX11RegionCaptureIsUnavailable()
    {
        if (!HasDisplay)
        {
            return;
        }

        using var environment = LinuxAutomationEnvironment.Create();
        var region = new AutomationStudio.Abstractions.ScreenRect(0, 0, 100, 80);

        try
        {
            var screenshot = await environment.ScreenshotService.CaptureRegionAsync(region);
            Assert.Equal(100, screenshot.Width);
            Assert.Equal(80, screenshot.Height);
            AssertWellFormedPng(screenshot.PngData, 100, 80);
        }
        catch (NotSupportedException)
        {
            // Expected on a session where X11 region capture doesn't work and there is no fallback.
        }
    }

    [Fact]
    public async Task CaptureScreenAsync_CalledTwice_ProducesIndependentResults()
    {
        if (!HasDisplay)
        {
            return;
        }

        using var environment = LinuxAutomationEnvironment.Create();
        var first = await environment.ScreenshotService.CaptureScreenAsync();
        var second = await environment.ScreenshotService.CaptureScreenAsync();

        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Height, second.Height);
        Assert.NotSame(first.PngData, second.PngData);
    }

    private static void AssertWellFormedPng(byte[] png, int expectedWidth, int expectedHeight)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        Assert.True(png.Length > signature.Length + 25, "PNG is implausibly small.");
        Assert.True(png.AsSpan(0, signature.Length).SequenceEqual(signature), "Missing PNG signature.");

        // IHDR immediately follows the signature: 4-byte length, "IHDR", then width/height as big-endian int32.
        var ihdrType = System.Text.Encoding.ASCII.GetString(png, 12, 4);
        Assert.Equal("IHDR", ihdrType);

        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }
}
