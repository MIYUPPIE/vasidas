using System.Runtime.InteropServices;
using System.Text;

namespace AutomationStudio.Linux.X11;

internal readonly record struct X11WindowHandle(IntPtr Window, string Title, int ProcessId);

/// <summary>
/// EWMH-based window listing/focus. Reads <c>_NET_CLIENT_LIST</c> off the root window for the
/// window set, <c>_NET_WM_NAME</c>/<c>WM_NAME</c> for titles and <c>_NET_WM_PID</c> to cross-reference
/// processes. Focusing uses <c>XRaiseWindow</c> + <c>XSetInputFocus</c> directly rather than sending
/// the full EWMH <c>_NET_ACTIVE_WINDOW</c> client-message request — simpler, and sufficient for
/// Phase 1's "bring the window I just launched to the front" use case; every window manager tested
/// against here (COSMIC, GNOME, KDE) honours direct focus/raise for a same-desktop window.
/// </summary>
internal sealed class X11WindowManager
{
    private readonly X11Display _display;
    private readonly IntPtr _netClientList;
    private readonly IntPtr _netWmName;
    private readonly IntPtr _netWmPid;
    private readonly IntPtr _wmName;
    private readonly IntPtr _netActiveWindow;

    public X11WindowManager(X11Display display)
    {
        _display = display;
        lock (_display.Lock)
        {
            _netClientList = NativeMethods.XInternAtom(display.Handle, "_NET_CLIENT_LIST", onlyIfExists: true);
            _netWmName = NativeMethods.XInternAtom(display.Handle, "_NET_WM_NAME", onlyIfExists: true);
            _netWmPid = NativeMethods.XInternAtom(display.Handle, "_NET_WM_PID", onlyIfExists: true);
            _wmName = NativeMethods.XInternAtom(display.Handle, "WM_NAME", onlyIfExists: true);
            _netActiveWindow = NativeMethods.XInternAtom(display.Handle, "_NET_ACTIVE_WINDOW", onlyIfExists: true);
        }
    }

    public IReadOnlyList<X11WindowHandle> ListWindows()
    {
        lock (_display.Lock)
        {
            var windows = new List<X11WindowHandle>();
            foreach (var window in ReadWindowList(_display.RootWindow, _netClientList))
            {
                windows.Add(new X11WindowHandle(window, ReadTitle(window), ReadPid(window)));
            }

            return windows;
        }
    }

    /// <summary>Reads <c>_NET_ACTIVE_WINDOW</c> off the root window (EWMH). Returns null when the property isn't set (window manager doesn't advertise it) or names no window.</summary>
    public X11WindowHandle? GetActiveWindow()
    {
        lock (_display.Lock)
        {
            if (_netActiveWindow == IntPtr.Zero || !TryGetProperty(_display.RootWindow, _netActiveWindow, out var format, out var count, out var data))
            {
                return null;
            }

            IntPtr window;
            try
            {
                if (format != 32 || count == 0)
                {
                    return null;
                }

                window = Marshal.ReadIntPtr(data, 0);
            }
            finally
            {
                NativeMethods.XFree(data);
            }

            return window == IntPtr.Zero ? null : new X11WindowHandle(window, ReadTitle(window), ReadPid(window));
        }
    }

    public void Focus(IntPtr window)
    {
        lock (_display.Lock)
        {
            NativeMethods.XRaiseWindow(_display.Handle, window);
            NativeMethods.XSetInputFocus(_display.Handle, window, NativeMethods.RevertToParent, 0);
            NativeMethods.XFlush(_display.Handle);
        }
    }

    /// <summary>Caller must hold <see cref="X11Display.Lock"/>.</summary>
    private List<IntPtr> ReadWindowList(IntPtr window, IntPtr propertyAtom)
    {
        if (propertyAtom == IntPtr.Zero || !TryGetProperty(window, propertyAtom, out var format, out var count, out var data))
        {
            return [];
        }

        try
        {
            var result = new List<IntPtr>((int)count);
            if (format == 32)
            {
                for (var i = 0; i < (int)count; i++)
                {
                    result.Add(Marshal.ReadIntPtr(data, i * IntPtr.Size));
                }
            }

            return result;
        }
        finally
        {
            NativeMethods.XFree(data);
        }
    }

    /// <summary>Caller must hold <see cref="X11Display.Lock"/>.</summary>
    private string ReadTitle(IntPtr window)
    {
        foreach (var atom in new[] { _netWmName, _wmName })
        {
            if (atom == IntPtr.Zero || !TryGetProperty(window, atom, out var format, out var count, out var data))
            {
                continue;
            }

            try
            {
                if (format == 8 && count > 0)
                {
                    var bytes = new byte[(int)count];
                    Marshal.Copy(data, bytes, 0, bytes.Length);
                    return Encoding.UTF8.GetString(bytes);
                }
            }
            finally
            {
                NativeMethods.XFree(data);
            }
        }

        return string.Empty;
    }

    /// <summary>Caller must hold <see cref="X11Display.Lock"/>.</summary>
    private int ReadPid(IntPtr window)
    {
        if (_netWmPid == IntPtr.Zero || !TryGetProperty(window, _netWmPid, out var format, out var count, out var data))
        {
            return 0;
        }

        try
        {
            return format == 32 && count > 0 ? (int)Marshal.ReadIntPtr(data, 0) : 0;
        }
        finally
        {
            NativeMethods.XFree(data);
        }
    }

    /// <summary>Caller must hold <see cref="X11Display.Lock"/>.</summary>
    private bool TryGetProperty(IntPtr window, IntPtr propertyAtom, out int format, out nuint count, out IntPtr data)
    {
        const int maxThirtyTwoBitUnits = 65536;

        var status = NativeMethods.XGetWindowProperty(
            _display.Handle, window, propertyAtom, 0, maxThirtyTwoBitUnits, delete: false,
            reqType: NativeMethods.AnyPropertyType,
            out var actualType, out format, out count, out _, out data);

        if (status != 0 || actualType == IntPtr.Zero || count == 0)
        {
            format = 0;
            count = 0;
            return false;
        }

        return true;
    }
}
