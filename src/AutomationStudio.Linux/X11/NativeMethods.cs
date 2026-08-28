using System.Runtime.InteropServices;

namespace AutomationStudio.Linux.X11;

/// <summary>
/// Raw P/Invoke declarations for libX11 and libXtst. Kept to exactly the functions the Phase 1
/// input/screenshot backends need — this is not a general Xlib binding.
/// </summary>
internal static partial class NativeMethods
{
    private const string LibX11 = "libX11.so.6";
    private const string LibXtst = "libXtst.so.6";

    [LibraryImport(LibX11, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr XOpenDisplay(string? displayName);

    [LibraryImport(LibX11)]
    public static partial int XCloseDisplay(IntPtr display);

    [LibraryImport(LibX11)]
    public static partial IntPtr XDefaultRootWindow(IntPtr display);

    [LibraryImport(LibX11)]
    public static partial int XDefaultScreen(IntPtr display);

    [LibraryImport(LibX11)]
    public static partial int XDisplayWidth(IntPtr display, int screenNumber);

    [LibraryImport(LibX11)]
    public static partial int XDisplayHeight(IntPtr display, int screenNumber);

    [LibraryImport(LibX11)]
    public static partial IntPtr XGetImage(
        IntPtr display, IntPtr drawable, int x, int y, uint width, uint height, nuint planeMask, int format);

    [LibraryImport(LibX11)]
    public static partial void XDestroyImage(IntPtr image);

    [LibraryImport(LibX11)]
    public static partial int XFlush(IntPtr display);

    [LibraryImport(LibX11)]
    public static partial int XSync(IntPtr display, [MarshalAs(UnmanagedType.Bool)] bool discard);

    [LibraryImport(LibX11, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nuint XStringToKeysym(string @string);

    [LibraryImport(LibX11)]
    public static partial byte XKeysymToKeycode(IntPtr display, nuint keysym);

    [LibraryImport(LibX11)]
    public static partial void XDisplayKeycodes(IntPtr display, out int minKeycodesReturn, out int maxKeycodesReturn);

    [LibraryImport(LibX11)]
    public static partial IntPtr XGetKeyboardMapping(
        IntPtr display, byte firstKeycode, int keycodeCount, out int keysymsPerKeycodeReturn);

    [LibraryImport(LibX11)]
    public static partial int XChangeKeyboardMapping(
        IntPtr display, int firstKeycode, int keysymsPerKeycode, nuint[] keysyms, int numCodes);

    [LibraryImport(LibX11)]
    public static partial void XFree(IntPtr data);

    [LibraryImport(LibX11, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr XInternAtom(IntPtr display, string atomName, [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);

    [LibraryImport(LibX11)]
    public static partial int XGetWindowProperty(
        IntPtr display, IntPtr window, IntPtr property, nint longOffset, nint longLength,
        [MarshalAs(UnmanagedType.Bool)] bool delete, IntPtr reqType,
        out IntPtr actualTypeReturn, out int actualFormatReturn,
        out nuint nItemsReturn, out nuint bytesAfterReturn, out IntPtr propReturn);

    [LibraryImport(LibX11)]
    public static partial int XRaiseWindow(IntPtr display, IntPtr window);

    [LibraryImport(LibX11)]
    public static partial int XSetInputFocus(IntPtr display, IntPtr window, int revertTo, nuint time);

    public const int AnyPropertyType = 0;
    public const int RevertToParent = 2;

    [LibraryImport(LibXtst)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool XTestFakeMotionEvent(IntPtr display, int screenNumber, int x, int y, nuint delay);

    [LibraryImport(LibXtst)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool XTestFakeButtonEvent(IntPtr display, uint button, [MarshalAs(UnmanagedType.Bool)] bool isPress, nuint delay);

    [LibraryImport(LibXtst)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool XTestFakeKeyEvent(IntPtr display, uint keycode, [MarshalAs(UnmanagedType.Bool)] bool isPress, nuint delay);

    [LibraryImport(LibXtst)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool XTestQueryExtension(
        IntPtr display, out int eventBase, out int errorBase, out int major, out int minor);

    public const int ZPixmap = 2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XImage
{
    public int width;
    public int height;
    public int xoffset;
    public int format;
    public IntPtr data;
    public int byte_order;
    public int bitmap_unit;
    public int bitmap_bit_order;
    public int bitmap_pad;
    public int depth;
    public int bytes_per_line;
    public int bits_per_pixel;
    public nuint red_mask;
    public nuint green_mask;
    public nuint blue_mask;
}
