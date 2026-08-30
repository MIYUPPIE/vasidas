using System.Runtime.InteropServices;

namespace AutomationStudio.Linux.X11;

/// <summary>
/// Raw P/Invoke declarations for the X11 RECORD extension (libXtst) — global input observation, the
/// counterpart to the XTest declarations in <see cref="NativeMethods"/> which do injection. Every
/// numeric layout below (struct field offsets, wire-event byte offsets used by
/// <see cref="X11InputRecorder"/>) was confirmed empirically against this project's own dev
/// environment via a compiled <c>offsetof()</c> probe and a hex dump of a synthetic injected event —
/// not transcribed from the header comments alone, since a C struct's real layout depends on
/// alignment rules this project doesn't want to get wrong silently. See
/// <see cref="X11InputRecorder"/> for exactly which offsets are used and why.
///
/// <see cref="XRecordEnableContext"/>/<see cref="XRecordEnableContextAsync"/> take a callback
/// (<see cref="XRecordInterceptProc"/>), so — matching the same lesson learned for Windows P/Invoke
/// (see <c>AutomationStudio.Windows.Win32.NativeMethods</c>'s remarks) — this uses classic
/// <see cref="DllImportAttribute"/> rather than <c>LibraryImport</c> for those two functions;
/// <c>LibraryImport</c>'s delegate-marshalling support is the less battle-tested path here, and this
/// callback fires from deep inside a blocking native call where getting it wrong is fatal.
/// </summary>
internal static partial class RecordNativeMethods
{
    private const string LibXtst = "libXtst.so.6";

    // recordconst.h
    public const int XRecordFromServer = 0;
    public const int XRecordCurrentClients = 1;
    public const int XRecordFutureClients = 2;
    public const int XRecordAllClients = 3;

    // Core X11 protocol event type codes (X.h) — the ones RECORD's device_events range covers.
    public const byte KeyPress = 2;
    public const byte KeyRelease = 3;
    public const byte ButtonPress = 4;
    public const byte ButtonRelease = 5;
    public const byte MotionNotify = 6;

    [LibraryImport(LibXtst)]
    public static partial int XRecordQueryVersion(IntPtr display, out int majorReturn, out int minorReturn);

    [LibraryImport(LibXtst)]
    public static partial IntPtr XRecordAllocRange();

    [LibraryImport(LibXtst)]
    public static partial nuint XRecordCreateContext(
        IntPtr display, int datumFlags, nuint[] clients, int nclients, IntPtr[] ranges, int nranges);

    public delegate void XRecordInterceptProc(IntPtr closure, IntPtr recordedData);

    [DllImport(LibXtst)]
    public static extern int XRecordEnableContext(IntPtr display, nuint context, XRecordInterceptProc callback, IntPtr closure);

    [DllImport(LibXtst)]
    public static extern int XRecordEnableContextAsync(IntPtr display, nuint context, XRecordInterceptProc callback, IntPtr closure);

    [LibraryImport(LibXtst)]
    public static partial void XRecordFreeData(IntPtr data);

    [LibraryImport(LibXtst)]
    public static partial int XRecordDisableContext(IntPtr display, nuint context);

    [LibraryImport(LibXtst)]
    public static partial int XRecordFreeContext(IntPtr display, nuint context);
}
