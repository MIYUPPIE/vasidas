using System.Numerics;
using System.Runtime.InteropServices;
using AutomationStudio.Abstractions;
using AutomationStudio.Abstractions.Imaging;

namespace AutomationStudio.Linux.X11;

/// <summary>
/// Captures pixels via <c>XGetImage</c> on the root window. Works for a native X11 session and for
/// XWayland (confirmed reachable on this dev machine's COSMIC/Wayland session at $DISPLAY) — silent,
/// synchronous, no permission dialog. Only 32-bit-per-pixel, 8-bit-per-channel TrueColor visuals are
/// supported, which covers effectively every desktop Linux configuration in use today; anything else
/// fails loudly rather than producing a corrupted image.
/// </summary>
internal sealed class X11ScreenshotService
{
    private static readonly nuint AllPlanes = unchecked((nuint)~0UL);

    private readonly X11Display _display;

    public X11ScreenshotService(X11Display display)
    {
        _display = display;
    }

    public Screenshot CaptureFullScreen() =>
        CaptureRegion(new ScreenRect(0, 0, _display.ScreenWidth, _display.ScreenHeight));

    public Screenshot CaptureRegion(ScreenRect region)
    {
        lock (_display.Lock)
        {
            X11ErrorHandling.ClearLastError();

            var imagePtr = NativeMethods.XGetImage(
                _display.Handle, _display.RootWindow, region.X, region.Y,
                (uint)region.Width, (uint)region.Height, AllPlanes, NativeMethods.ZPixmap);

            if (imagePtr == IntPtr.Zero)
            {
                var errorCode = X11ErrorHandling.LastErrorCode;
                throw new InvalidOperationException(
                    errorCode == 0
                        ? $"XGetImage failed for region {region} (no X error was reported; the region may fall outside the screen bounds)."
                        : $"XGetImage failed for region {region}: X11 {X11ErrorHandling.DescribeErrorCode(errorCode)}. " +
                          "On XWayland this typically means the root window has no real backing pixels for this region " +
                          "(Wayland compositors generally do not expose their framebuffer through the root window) — " +
                          "the portal-based capture path should be used instead of X11 for this session.");
            }

            try
            {
                var image = Marshal.PtrToStructure<XImage>(imagePtr);
                if (image.bits_per_pixel != 32)
                {
                    throw new NotSupportedException(
                        $"Unsupported X11 visual: {image.bits_per_pixel} bits per pixel (only 32bpp TrueColor is supported).");
                }

                var rgba = ConvertToRgba(image, region.Width, region.Height);
                var png = PngEncoder.EncodeRgba32(rgba, region.Width, region.Height);
                return new Screenshot { PngData = png, Width = region.Width, Height = region.Height };
            }
            finally
            {
                NativeMethods.XDestroyImage(imagePtr);
            }
        }
    }

    private static unsafe byte[] ConvertToRgba(XImage image, int width, int height)
    {
        var (rShift, rWidth) = MaskInfo(image.red_mask);
        var (gShift, gWidth) = MaskInfo(image.green_mask);
        var (bShift, bWidth) = MaskInfo(image.blue_mask);

        if (rWidth != 8 || gWidth != 8 || bWidth != 8)
        {
            throw new NotSupportedException("Only 8-bit-per-channel TrueColor visuals are supported.");
        }

        var rgba = new byte[width * height * 4];
        var src = (byte*)image.data;

        for (var y = 0; y < height; y++)
        {
            var row = src + (long)y * image.bytes_per_line;
            for (var x = 0; x < width; x++)
            {
                var pixel = *(uint*)(row + x * 4);
                var di = (y * width + x) * 4;
                rgba[di + 0] = (byte)((pixel & (uint)image.red_mask) >> rShift);
                rgba[di + 1] = (byte)((pixel & (uint)image.green_mask) >> gShift);
                rgba[di + 2] = (byte)((pixel & (uint)image.blue_mask) >> bShift);
                rgba[di + 3] = 255;
            }
        }

        return rgba;
    }

    private static (int Shift, int Width) MaskInfo(nuint mask) =>
        mask == 0 ? (0, 0) : (BitOperations.TrailingZeroCount((ulong)mask), BitOperations.PopCount((ulong)mask));
}
