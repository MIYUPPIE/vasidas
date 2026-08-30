using System.Runtime.InteropServices;
using System.Text;

namespace AutomationStudio.Linux.X11;

internal readonly record struct X11WindowHandle(IntPtr Window, string Title, int ProcessId);

/// <summary>
/// EWMH-based window listing/focus. Reads <c>_NET_CLIENT_LIST</c> off the root window for the
/// window set, <c>_NET_WM_NAME</c>/<c>WM_NAME</c> for titles and <c>_NET_WM_PID</c> to cross-reference
/// processes.
/// </summary>
/// <remarks>
/// <see cref="Focus"/> sends the real EWMH <c>_NET_ACTIVE_WINDOW</c> client-message request to the
/// root window (design doc section 31/Phase 11 — "deepen X11/EWMH coverage") — the same protocol a
/// taskbar or alt-tab switcher uses, and the one a spec-compliant/focus-stealing-prevention window
/// manager actually expects — alongside <c>XRaiseWindow</c>/<c>XSetInputFocus</c> called directly on
/// the target window (Phase 1's original approach, kept as a same-call fallback for a WM that ignores
/// the client message). Verifying it worked turned out to be the harder half: confirmed live on this
/// dev machine's COSMIC/XWayland session that <c>_NET_ACTIVE_WINDOW</c> is advertised in
/// <c>_NET_SUPPORTED</c> but its value on the root window never actually changes, no matter which
/// window really has focus — real focus-switching between two separately-launched XWayland windows
/// left the root property pinned to the root window's own id throughout, while each window's own
/// <c>_NET_WM_STATE_FOCUSED</c> flag correctly tracked the real focus change. <see cref="GetActiveWindow"/>
/// therefore falls back to scanning for that per-window flag when the root property can't be trusted
/// — see its own doc comment. GNOME/mutter and KDE/KWin both document correct <c>_NET_ACTIVE_WINDOW</c>
/// maintenance and weren't live-tested here (no session available in this environment); the fallback
/// exists specifically so a similarly-immature EWMH implementation elsewhere degrades gracefully
/// rather than silently reporting nothing.
/// </remarks>
internal sealed class X11WindowManager
{
    private readonly X11Display _display;
    private readonly IntPtr _netClientList;
    private readonly IntPtr _netWmName;
    private readonly IntPtr _netWmPid;
    private readonly IntPtr _wmName;
    private readonly IntPtr _netActiveWindow;
    private readonly IntPtr _netWmState;
    private readonly IntPtr _netWmStateFocused;

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
            _netWmState = NativeMethods.XInternAtom(display.Handle, "_NET_WM_STATE", onlyIfExists: true);
            _netWmStateFocused = NativeMethods.XInternAtom(display.Handle, "_NET_WM_STATE_FOCUSED", onlyIfExists: true);
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

    /// <summary>
    /// Reports whichever window is really focused. Primary source is <c>_NET_ACTIVE_WINDOW</c> on
    /// the root window (EWMH) — correctly maintained by most compliant window managers — with a
    /// fallback to scanning <c>_NET_CLIENT_LIST</c> for whichever window carries
    /// <c>_NET_WM_STATE_FOCUSED</c>. The fallback is not just defensive: confirmed live on this dev
    /// machine's COSMIC/XWayland session that <c>_NET_ACTIVE_WINDOW</c> is advertised in
    /// <c>_NET_SUPPORTED</c> but its VALUE never actually changes away from the root window itself
    /// (checked by switching real focus between two separately-launched XWayland windows — root's
    /// property stayed pinned to the root window's own id the entire time), while
    /// <c>_NET_WM_STATE_FOCUSED</c> on the individual windows correctly tracked which one actually
    /// had focus. A still-maturing compositor's XWayland integration, not a universal EWMH gap — but
    /// exactly the failure mode this fallback exists to catch regardless of which WM produces it.
    /// </summary>
    public X11WindowHandle? GetActiveWindow()
    {
        lock (_display.Lock)
        {
            var primary = TryGetActiveWindowFromRootProperty();
            if (primary is not null)
            {
                return primary;
            }

            foreach (var window in ReadWindowList(_display.RootWindow, _netClientList))
            {
                if (HasFocusedState(window))
                {
                    return new X11WindowHandle(window, ReadTitle(window), ReadPid(window));
                }
            }

            return null;
        }
    }

    /// <summary>Caller must hold <see cref="X11Display.Lock"/>. Returns null both when the property is genuinely absent/empty AND when it resolves to the root window itself or to a handle with no readable identity — either way, not something the caller can trust as "the" active window.</summary>
    private X11WindowHandle? TryGetActiveWindowFromRootProperty()
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

        if (window == IntPtr.Zero || window == _display.RootWindow)
        {
            return null;
        }

        var title = ReadTitle(window);
        var pid = ReadPid(window);
        return string.IsNullOrEmpty(title) && pid == 0 ? null : new X11WindowHandle(window, title, pid);
    }

    /// <summary>Caller must hold <see cref="X11Display.Lock"/>.</summary>
    private bool HasFocusedState(IntPtr window)
    {
        if (_netWmState == IntPtr.Zero || _netWmStateFocused == IntPtr.Zero || !TryGetProperty(window, _netWmState, out var format, out var count, out var data))
        {
            return false;
        }

        try
        {
            if (format != 32)
            {
                return false;
            }

            for (var i = 0; i < (int)count; i++)
            {
                if (Marshal.ReadIntPtr(data, i * IntPtr.Size) == _netWmStateFocused)
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            NativeMethods.XFree(data);
        }
    }

    /// <summary>Requests focus for <paramref name="window"/> and reports whether the window manager
    /// actually honoured it (polls <c>_NET_ACTIVE_WINDOW</c> back for up to <paramref name="timeout"/>)
    /// — a focus-stealing-prevention policy or a non-EWMH WM can silently ignore the request, and
    /// callers (<see cref="LinuxWindowManager.FocusWindowByTitleAsync"/>/<c>ByProcessAsync</c>) need
    /// to know the difference rather than assuming "we asked" means "it happened."</summary>
    public bool Focus(IntPtr window, TimeSpan? timeout = null)
    {
        lock (_display.Lock)
        {
            SendActiveWindowClientMessage(window);

            // Kept alongside the EWMH request, not instead of it: a window manager with no/partial
            // EWMH support (or one that doesn't recognise this specific request) still gets a direct
            // raise/focus attempt, matching Phase 1's original behavior for that case.
            NativeMethods.XRaiseWindow(_display.Handle, window);
            NativeMethods.XSetInputFocus(_display.Handle, window, NativeMethods.RevertToParent, 0);
            NativeMethods.XFlush(_display.Handle);
        }

        return PollUntilActive(window, timeout ?? TimeSpan.FromMilliseconds(500));
    }

    /// <summary>Caller must hold <see cref="X11Display.Lock"/>.</summary>
    private void SendActiveWindowClientMessage(IntPtr window)
    {
        if (_netActiveWindow == IntPtr.Zero)
        {
            return; // window manager doesn't even advertise the atom; nothing to send.
        }

        // Reenters the same C# `lock` from the same thread (Monitor is reentrant), safe by design.
        var currentlyActive = GetActiveWindow()?.Window ?? IntPtr.Zero;
        var message = new XClientMessageEvent
        {
            type = 33, // ClientMessage
            send_event = 1,
            display = _display.Handle,
            window = window,
            message_type = _netActiveWindow,
            format = 32,
            data0 = 1, // source indication: 1 = normal application (EWMH spec)
            data1 = 0, // timestamp — 0 (CurrentTime) is accepted by every WM tested here
            data2 = (long)currentlyActive,
        };

        NativeMethods.XSendEvent(_display.Handle, _display.RootWindow, propagate: false, NativeMethods.SubstructureRedirectAndNotifyMask, ref message);
    }

    private bool PollUntilActive(IntPtr window, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        do
        {
            if (GetActiveWindow()?.Window == window)
            {
                return true;
            }

            Thread.Sleep(20);
        } while (DateTime.UtcNow < deadline);

        return GetActiveWindow()?.Window == window;
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
