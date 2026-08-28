using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace AutomationStudio.Windows.Win32;

/// <remarks>
/// Uses classic <see cref="DllImportAttribute"/> rather than the newer source-generated
/// <c>LibraryImport</c> throughout: the source generator does not support <see cref="StringBuilder"/>
/// marshalling (needed for <c>GetWindowTextW</c>) and its delegate-callback support (needed for
/// <c>EnumWindows</c>) is less battle-tested. DllImport handles every marshalling shape this file
/// needs reliably, which matters more here than the AOT/perf edge it gives up, since this code can't
/// be runtime-verified from the Linux dev machine it was written on — it needs to be right the first time on Windows.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class NativeMethods
{
    // --- SendInput (mouse + keyboard) ---

    public const uint InputMouse = 0;
    public const uint InputKeyboard = 1;

    public const uint MouseEventFMove = 0x0001;
    public const uint MouseEventFAbsolute = 0x8000;
    public const uint MouseEventFVirtualDesk = 0x4000;
    public const uint MouseEventFLeftDown = 0x0002;
    public const uint MouseEventFLeftUp = 0x0004;
    public const uint MouseEventFRightDown = 0x0008;
    public const uint MouseEventFRightUp = 0x0010;
    public const uint MouseEventFMiddleDown = 0x0020;
    public const uint MouseEventFMiddleUp = 0x0040;
    public const uint MouseEventFWheel = 0x0800;
    public const uint MouseEventFHWheel = 0x1000;

    public const uint KeyEventFExtendedKey = 0x0001;
    public const uint KeyEventFKeyUp = 0x0002;
    public const uint KeyEventFUnicode = 0x0004;

    public const int WheelDelta = 120;

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseInput
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeybdInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MouseInput mi;
        [FieldOffset(0)] public KeybdInput ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Input
    {
        public uint type;
        public InputUnion u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint numberOfInputs, Input[] inputs, int structSize);

    // --- Virtual screen metrics (multi-monitor) ---

    public const int SmXVirtualScreen = 76;
    public const int SmYVirtualScreen = 77;
    public const int SmCxVirtualScreen = 78;
    public const int SmCyVirtualScreen = 79;

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);

    // --- GDI screen capture ---

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hdc);

    public const uint SrcCopy = 0x00CC0020;
    public const uint CaptureBlt = 0x40000000;

    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(
        IntPtr hdcDest, int xDest, int yDest, int width, int height,
        IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfoHeader
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    public const int DibRgbColors = 0;

    [DllImport("gdi32.dll")]
    public static extern int GetDIBits(
        IntPtr hdc, IntPtr hbmp, uint start, uint cLines,
        [Out] byte[] lpvBits, ref BitmapInfoHeader lpbi, uint usage);

    // --- Window enumeration / focus ---

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    public const int SwRestore = 9;

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
