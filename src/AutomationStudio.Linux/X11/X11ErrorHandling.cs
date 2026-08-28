using System.Runtime.InteropServices;

namespace AutomationStudio.Linux.X11;

/// <summary>
/// Xlib's default error handler calls <c>exit()</c> on any X protocol error — meaning an
/// unhandled BadMatch/BadWindow/etc anywhere in this process crashes the entire application, not
/// just the request that triggered it. Installed once at process start so protocol errors become
/// ordinary, catchable C# exceptions instead (confirmed necessary the hard way: <c>XGetImage</c> on
/// this machine's XWayland root window returns BadMatch, and without this handler that took the
/// whole test host down with it).
/// </summary>
internal static class X11ErrorHandling
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int XErrorHandlerDelegate(IntPtr display, IntPtr errorEvent);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XSetErrorHandler(XErrorHandlerDelegate? handler);

    [StructLayout(LayoutKind.Sequential)]
    private struct XErrorEvent
    {
        public int type;
        public IntPtr display;
        public nuint serial;
        public byte error_code;
        public byte request_code;
        public byte minor_code;
        public nuint resourceid;
    }

    // Kept as a static field so the GC never collects it while native code holds the function pointer.
    private static readonly XErrorHandlerDelegate HandlerDelegate = HandleError;

    [ThreadStatic]
    private static byte _lastErrorCode;

    private static int _installed;

    public static void EnsureInstalled()
    {
        if (Interlocked.Exchange(ref _installed, 1) == 0)
        {
            XSetErrorHandler(HandlerDelegate);
        }
    }

    /// <summary>Clears the per-thread last-error slot before a call whose failure should be observed.</summary>
    public static void ClearLastError() => _lastErrorCode = 0;

    public static byte LastErrorCode => _lastErrorCode;

    private static int HandleError(IntPtr display, IntPtr errorEventPtr)
    {
        var errorEvent = Marshal.PtrToStructure<XErrorEvent>(errorEventPtr);
        _lastErrorCode = errorEvent.error_code;
        return 0;
    }

    public static string DescribeErrorCode(byte code) => code switch
    {
        1 => "BadRequest",
        2 => "BadValue",
        3 => "BadWindow",
        4 => "BadPixmap",
        5 => "BadAtom",
        6 => "BadCursor",
        7 => "BadFont",
        8 => "BadMatch",
        9 => "BadDrawable",
        10 => "BadAccess",
        11 => "BadAlloc",
        12 => "BadColor",
        13 => "BadGC",
        14 => "BadIDChoice",
        15 => "BadName",
        16 => "BadLength",
        17 => "BadImplementation",
        _ => $"X error {code}"
    };
}
