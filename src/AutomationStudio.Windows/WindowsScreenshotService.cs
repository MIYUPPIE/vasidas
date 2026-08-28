using System.Runtime.Versioning;
using AutomationStudio.Abstractions;
using AutomationStudio.Abstractions.Imaging;
using AutomationStudio.Windows.Win32;

namespace AutomationStudio.Windows;

/// <summary>
/// Captures pixels via classic GDI (<c>BitBlt</c> into a top-down 32bpp DIB). No permission prompt,
/// no portal — Windows has never restricted this the way Wayland restricts screen capture.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsScreenshotService : IScreenshotService
{
    public Task<Screenshot> CaptureScreenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CaptureFullScreen());

    public Task<Screenshot> CaptureRegionAsync(ScreenRect region, CancellationToken cancellationToken = default) =>
        Task.FromResult(CaptureRegion(region));

    private Screenshot CaptureFullScreen()
    {
        var left = NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen);
        var top = NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen);
        var width = NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen);
        return CaptureRegion(new ScreenRect(left, top, width, height));
    }

    private Screenshot CaptureRegion(ScreenRect region)
    {
        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("GetDC(NULL) failed to obtain the screen device context.");
        }

        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previousObject = IntPtr.Zero;

        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                throw new InvalidOperationException("CreateCompatibleDC failed.");
            }

            bitmap = NativeMethods.CreateCompatibleBitmap(screenDc, region.Width, region.Height);
            if (bitmap == IntPtr.Zero)
            {
                throw new InvalidOperationException("CreateCompatibleBitmap failed.");
            }

            previousObject = NativeMethods.SelectObject(memoryDc, bitmap);

            var copied = NativeMethods.BitBlt(
                memoryDc, 0, 0, region.Width, region.Height,
                screenDc, region.X, region.Y, NativeMethods.SrcCopy | NativeMethods.CaptureBlt);

            if (!copied)
            {
                throw new InvalidOperationException(
                    $"BitBlt failed (Win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).");
            }

            var rgba = ExtractRgba(memoryDc, bitmap, region.Width, region.Height);
            var png = PngEncoder.EncodeRgba32(rgba, region.Width, region.Height);
            return new Screenshot { PngData = png, Width = region.Width, Height = region.Height };
        }
        finally
        {
            if (memoryDc != IntPtr.Zero && previousObject != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memoryDc, previousObject);
            }

            if (bitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(bitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(memoryDc);
            }

            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static byte[] ExtractRgba(IntPtr memoryDc, IntPtr bitmap, int width, int height)
    {
        var header = new NativeMethods.BitmapInfoHeader
        {
            biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
            biWidth = width,
            biHeight = -height, // negative = top-down DIB
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0, // BI_RGB
        };

        // GDI's 32bpp BI_RGB layout is BGRX per pixel.
        var bgrx = new byte[width * height * 4];
        var linesCopied = NativeMethods.GetDIBits(
            memoryDc, bitmap, 0, (uint)height, bgrx, ref header, NativeMethods.DibRgbColors);

        if (linesCopied == 0)
        {
            throw new InvalidOperationException("GetDIBits returned no scanlines.");
        }

        var rgba = new byte[bgrx.Length];
        for (var i = 0; i < bgrx.Length; i += 4)
        {
            rgba[i + 0] = bgrx[i + 2]; // R
            rgba[i + 1] = bgrx[i + 1]; // G
            rgba[i + 2] = bgrx[i + 0]; // B
            rgba[i + 3] = 255;         // A
        }

        return rgba;
    }
}
