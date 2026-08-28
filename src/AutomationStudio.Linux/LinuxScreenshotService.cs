using AutomationStudio.Abstractions;
using AutomationStudio.Linux.Portal;
using AutomationStudio.Linux.X11;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Linux;

/// <summary>
/// Linux <see cref="IScreenshotService"/>. Tries XGetImage over $DISPLAY first — fast, synchronous,
/// no dialog — and falls back to the xdg-desktop-portal Screenshot API when that fails, which on a
/// native Wayland session is every time: XWayland's root window generally has no real backing
/// pixels for compositor-rendered content (confirmed live on this dev machine, where XGetImage
/// returns BadMatch even for a 1x1 region), because Wayland compositors don't expose their
/// framebuffer through it. This mirrors the reliability-first fallback hierarchy the design doc
/// lays out in section 2, applied to screen capture instead of element finding.
/// </summary>
public sealed class LinuxScreenshotService : IScreenshotService
{
    private readonly X11ScreenshotService? _x11;
    private readonly PortalScreenshotService _portal = new();
    private readonly ILogger _logger;

    internal LinuxScreenshotService(X11Display? display, ILogger? logger = null)
    {
        _x11 = display is null ? null : new X11ScreenshotService(display);
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<Screenshot> CaptureScreenAsync(CancellationToken cancellationToken = default)
    {
        if (_x11 is not null)
        {
            try
            {
                return _x11.CaptureFullScreen();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "X11 screen capture failed; falling back to the xdg-desktop-portal Screenshot API.");
            }
        }

        return await _portal.CaptureFullScreenAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<Screenshot> CaptureRegionAsync(ScreenRect region, CancellationToken cancellationToken = default)
    {
        if (_x11 is null)
        {
            throw new NotSupportedException(
                "Region-limited capture requires the X11 path; the portal fallback only supports full-screen " +
                "capture in this phase (cropping would need a PNG decoder, which nothing in Phase 1 needs yet).");
        }

        try
        {
            return Task.FromResult(_x11.CaptureRegion(region));
        }
        catch (Exception ex)
        {
            throw new NotSupportedException(
                "Region-limited capture failed via X11 and has no portal fallback in this phase.", ex);
        }
    }
}
