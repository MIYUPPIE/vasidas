using System.Runtime.InteropServices;
using AutomationStudio.Abstractions;

namespace AutomationStudio.Linux.X11;

/// <summary>
/// Global mouse/keyboard capture via the X11 RECORD extension (DAS.md section 25) — the observation
/// counterpart to <see cref="X11InputController"/>'s injection. RECORD's own API forces a specific
/// two-connection shape: <c>XRecordEnableContext</c> blocks the connection it's called on until
/// <c>XRecordDisableContext</c> is called on a DIFFERENT connection (documented Xlib behaviour, not
/// a workaround). So this opens two dedicated connections of its own — never the shared one
/// <see cref="LinuxAutomationEnvironment"/> hands other backends, which would deadlock everything
/// else using it — a "control" connection for create/disable/free, and a "data" connection whose
/// only job is to sit inside the blocking enable call on a background thread.
/// </summary>
internal sealed class X11InputRecorder : IInputRecorder
{
    private readonly IntPtr _controlDisplay;
    private readonly IntPtr _dataDisplay;
    private readonly object _lock = new();

    private nuint _context;
    private Thread? _thread;
    private List<RecordedInputEvent>? _events;
    private long _startTimestampTicks;

    // Pre-fetched once per Start(), never touched again while recording: resolving a keycode to a
    // keysym from inside the RECORD callback (see ParseKeyEvent) must not issue any Xlib request of
    // its own — see the comment there for why. A plain array lookup has zero I/O, so it's safe.
    private byte _minKeycode;
    private int _keysymsPerKeycode;
    private nuint[] _keysymTable = [];

    /// <summary>Rooted for the lifetime of an active recording so the GC never collects the delegate while unmanaged code still holds a pointer to it — it is invoked repeatedly from inside the blocking XRecordEnableContext call on <see cref="_thread"/>.</summary>
    private RecordNativeMethods.XRecordInterceptProc? _callback;

    public X11InputRecorder(X11Display? sharedDisplay)
    {
        if (sharedDisplay is null)
        {
            throw new CapabilityUnavailableException("Input recording", "no X11 display reachable (pure Wayland session without XWayland).");
        }

        _controlDisplay = NativeMethods.XOpenDisplay(null);
        _dataDisplay = NativeMethods.XOpenDisplay(null);
        if (_controlDisplay == IntPtr.Zero || _dataDisplay == IntPtr.Zero)
        {
            throw new CapabilityUnavailableException("Input recording", "could not open a dedicated X11 connection for the RECORD extension.");
        }

        if (RecordNativeMethods.XRecordQueryVersion(_controlDisplay, out _, out _) == 0)
        {
            NativeMethods.XCloseDisplay(_controlDisplay);
            NativeMethods.XCloseDisplay(_dataDisplay);
            throw new CapabilityUnavailableException("Input recording", "the X11 RECORD extension is not available on this display.");
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_thread is not null)
            {
                throw new InvalidOperationException("Already recording.");
            }

            var range = RecordNativeMethods.XRecordAllocRange();
            if (range == IntPtr.Zero)
            {
                throw new InvalidOperationException("XRecordAllocRange failed.");
            }

            nuint context;
            try
            {
                // device_events (XRecordRange8): offset 18 = first, offset 19 = last — verified via
                // a compiled offsetof() probe against this project's own <X11/extensions/record.h>.
                // XRecordAllocRange zero-initializes everything else, so this is the only interest
                // registered: core+extension requests/replies/errors are left untouched.
                Marshal.WriteByte(range, 18, RecordNativeMethods.KeyPress);
                Marshal.WriteByte(range, 19, RecordNativeMethods.MotionNotify);

                // Created on _dataDisplay, not _controlDisplay: an XRecordContext id is scoped to
                // the connection that created it, and XRecordEnableContext must be called on that
                // same connection — only XRecordDisableContext needs the separate control
                // connection. Getting this backwards (control creates, data enables) makes
                // XRecordEnableContext fail immediately rather than block, confirmed live.
                var clients = new[] { (nuint)RecordNativeMethods.XRecordAllClients };
                var ranges = new[] { range };
                context = RecordNativeMethods.XRecordCreateContext(_dataDisplay, 0, clients, 1, ranges, 1);
            }
            finally
            {
                NativeMethods.XFree(range);
            }

            if (context == 0)
            {
                throw new InvalidOperationException("XRecordCreateContext failed.");
            }

            CacheKeyboardMapping();

            _context = context;
            _events = [];
            _startTimestampTicks = DateTimeOffset.UtcNow.Ticks;
            _callback = OnIntercepted;

            _thread = new Thread(() => RecordNativeMethods.XRecordEnableContext(_dataDisplay, _context, _callback, IntPtr.Zero))
            {
                IsBackground = true,
                Name = "X11InputRecorder"
            };
            _thread.Start();
        }
    }

    public IReadOnlyList<RecordedInputEvent> Stop()
    {
        Thread thread;
        nuint context;
        lock (_lock)
        {
            if (_thread is null)
            {
                throw new InvalidOperationException("Not currently recording.");
            }

            thread = _thread;
            context = _context;
        }

        // Deliberately outside the lock: this causes the blocked XRecordEnableContext call on
        // _thread to return, but the callback may still be mid-flight and needs _lock itself to
        // append its final event(s) — holding the lock here would deadlock against that.
        RecordNativeMethods.XRecordDisableContext(_controlDisplay, context);
        NativeMethods.XFlush(_controlDisplay);
        thread.Join();

        lock (_lock)
        {
            // Freed on _dataDisplay: it owns the context (see the comment in Start()), and is no
            // longer blocked now that thread.Join() above has returned.
            RecordNativeMethods.XRecordFreeContext(_dataDisplay, context);
            _context = 0;
            _thread = null;
            _callback = null;

            var events = _events ?? [];
            _events = null;
            return events;
        }
    }

    public void Dispose()
    {
        if (_thread is not null)
        {
            try
            {
                Stop();
            }
            catch (InvalidOperationException)
            {
                // Another thread already stopped it between the check above and here — fine.
            }
        }

        if (_controlDisplay != IntPtr.Zero)
        {
            NativeMethods.XCloseDisplay(_controlDisplay);
        }

        if (_dataDisplay != IntPtr.Zero)
        {
            NativeMethods.XCloseDisplay(_dataDisplay);
        }
    }

    private void OnIntercepted(IntPtr closure, IntPtr recordedData)
    {
        try
        {
            ParseIntercepted(recordedData);
        }
        catch
        {
            // An exception must never cross back into the unmanaged XRecordEnableContext call that
            // invoked this — that terminates the process. Same broad-catch lesson as
            // PvMicrophoneCapture's background capture thread.
        }
        finally
        {
            RecordNativeMethods.XRecordFreeData(recordedData);
        }
    }

    private void ParseIntercepted(IntPtr recordedData)
    {
        // XRecordInterceptData layout (verified via offsetof()): category at byte 24 (int), data at
        // byte 32 (unsigned char*), data_len at byte 40 (unsigned long, in 4-byte units), sizeof = 48.
        var category = Marshal.ReadInt32(recordedData, 24);
        if (category != RecordNativeMethods.XRecordFromServer)
        {
            return; // client-started/died/start-of-data/end-of-data notices carry no event payload.
        }

        var data = Marshal.ReadIntPtr(recordedData, 32);
        var dataLen = Marshal.ReadInt64(recordedData, 40);
        var totalBytes = dataLen * 4;
        if (data == IntPtr.Zero || totalBytes <= 0)
        {
            return;
        }

        var buffer = new byte[32];
        var elapsed = TimeSpan.FromTicks(DateTimeOffset.UtcNow.Ticks - _startTimestampTicks);

        // RECORD batches multiple wire events (e.g. rapid mouse motion) into one delivery; every
        // core input event this range covers is exactly 32 bytes, so read fixed-size strides across
        // the whole buffer.
        for (var offset = 0L; offset + 32 <= totalBytes; offset += 32)
        {
            Marshal.Copy(data + (nint)offset, buffer, 0, 32);
            var evt = ParseEvent(buffer, elapsed);
            if (evt is not null)
            {
                lock (_lock)
                {
                    _events?.Add(evt);
                }
            }
        }
    }

    /// <summary>
    /// Parses one 32-byte core X11 wire event. Byte offsets verified empirically (hex dump of a real
    /// captured event, cross-checked against a known injected position): type=0, detail=1
    /// (keycode/button), rootX=20-21 (int16 LE), rootY=22-23 (int16 LE), state=28-29 (uint16 LE) —
    /// every KeyButtonPointer-class event (KeyPress/Release, ButtonPress/Release, MotionNotify)
    /// shares this layout.
    /// </summary>
    private RecordedInputEvent? ParseEvent(byte[] buffer, TimeSpan elapsed)
    {
        var type = buffer[0];
        var detail = buffer[1];
        var rootX = BitConverter.ToInt16(buffer, 20);
        var rootY = BitConverter.ToInt16(buffer, 22);
        var state = BitConverter.ToUInt16(buffer, 28);
        var position = new ScreenPoint(rootX, rootY);

        return type switch
        {
            RecordNativeMethods.MotionNotify => new RecordedInputEvent(RecordedInputEventKind.MouseMove, elapsed, position),
            RecordNativeMethods.ButtonPress or RecordNativeMethods.ButtonRelease => ParseButtonEvent(type, detail, elapsed, position),
            RecordNativeMethods.KeyPress or RecordNativeMethods.KeyRelease => ParseKeyEvent(type, detail, state, elapsed, position),
            _ => null
        };
    }

    /// <summary>Button numbers 1-3 mirror <see cref="X11InputController"/>'s own ToButtonNumber; 4-7 are wheel ticks (X11 has no separate scroll-event type — a wheel notch is a synthetic button press/release pair), mapped with the same axis/sign convention <see cref="X11InputController.Scroll"/> injects with, so normalization can round-trip it directly into a ScrollAction.</summary>
    private static RecordedInputEvent? ParseButtonEvent(byte type, byte detail, TimeSpan elapsed, ScreenPoint position)
    {
        if (detail is >= 4 and <= 7)
        {
            if (type != RecordNativeMethods.ButtonPress)
            {
                return null; // one tick per press; the matching release carries nothing new.
            }

            return detail switch
            {
                4 => new RecordedInputEvent(RecordedInputEventKind.MouseScroll, elapsed, position, ScrollDeltaY: 1),
                5 => new RecordedInputEvent(RecordedInputEventKind.MouseScroll, elapsed, position, ScrollDeltaY: -1),
                6 => new RecordedInputEvent(RecordedInputEventKind.MouseScroll, elapsed, position, ScrollDeltaX: -1),
                _ => new RecordedInputEvent(RecordedInputEventKind.MouseScroll, elapsed, position, ScrollDeltaX: 1)
            };
        }

        var button = detail switch
        {
            1 => MouseButton.Left,
            2 => MouseButton.Middle,
            3 => MouseButton.Right,
            _ => (MouseButton?)null
        };

        if (button is null)
        {
            return null;
        }

        var kind = type == RecordNativeMethods.ButtonPress ? RecordedInputEventKind.MouseDown : RecordedInputEventKind.MouseUp;
        return new RecordedInputEvent(kind, elapsed, position, Button: button);
    }

    /// <summary>
    /// Resolves the keycode to a keysym via <see cref="_keysymTable"/> — a pure array lookup, no
    /// Xlib call. Confirmed live (via a hang dump from a real deadlocked run, not just reasoning
    /// about it) that calling <c>XKeycodeToKeysym(_dataDisplay, ...)</c> directly from here
    /// deadlocks: that connection is already being read by the blocking <c>XRecordEnableContext</c>
    /// call this callback runs inside of, and <c>XKeycodeToKeysym</c> can itself need to issue a
    /// request on the same connection (to fetch the keyboard mapping) — a classic reentrant-I/O
    /// deadlock, only reachable via key events, which is exactly why a mouse-only smoke test didn't
    /// catch it. <see cref="CacheKeyboardMapping"/> fetches the whole mapping once, before recording
    /// starts, specifically to avoid this.
    /// </summary>
    private RecordedInputEvent ParseKeyEvent(byte type, byte keycode, ushort state, TimeSpan elapsed, ScreenPoint position)
    {
        const ushort shiftMask = 0x0001;
        var shifted = (state & shiftMask) != 0;

        var keysym = LookupKeysym(keycode, shifted ? 1 : 0);
        var (key, character) = KeysymMapper.FromKeysym(keysym);

        var kind = type == RecordNativeMethods.KeyPress ? RecordedInputEventKind.KeyDown : RecordedInputEventKind.KeyUp;
        return new RecordedInputEvent(kind, elapsed, position, Key: key, Character: character);
    }

    /// <summary>Fetches the active keyboard mapping via <see cref="_controlDisplay"/> — idle at this point, Start() hasn't spun up the recording thread yet — mirroring exactly how <see cref="X11InputController.FindScratchKeycode"/> reads the same table.</summary>
    private void CacheKeyboardMapping()
    {
        NativeMethods.XDisplayKeycodes(_controlDisplay, out var min, out var max);
        var mapPtr = NativeMethods.XGetKeyboardMapping(_controlDisplay, (byte)min, max - min + 1, out var keysymsPerKeycode);

        try
        {
            var count = (max - min + 1) * keysymsPerKeycode;
            var table = new nuint[count];
            for (var i = 0; i < count; i++)
            {
                table[i] = (nuint)Marshal.ReadIntPtr(mapPtr, i * IntPtr.Size);
            }

            _minKeycode = (byte)min;
            _keysymsPerKeycode = keysymsPerKeycode;
            _keysymTable = table;
        }
        finally
        {
            NativeMethods.XFree(mapPtr);
        }
    }

    /// <summary>Pure in-memory lookup — see <see cref="ParseKeyEvent"/> for why this must never call into Xlib. Falls back to the unshifted (index 0) keysym when the shifted slot is unbound, matching how most layouts leave secondary levels empty for keys with no shifted variant.</summary>
    private nuint LookupKeysym(byte keycode, int index)
    {
        if (_keysymsPerKeycode == 0 || keycode < _minKeycode)
        {
            return 0;
        }

        var row = (keycode - _minKeycode) * _keysymsPerKeycode;
        if (row < 0 || row >= _keysymTable.Length)
        {
            return 0;
        }

        if (index < _keysymsPerKeycode && row + index < _keysymTable.Length && _keysymTable[row + index] != 0)
        {
            return _keysymTable[row + index];
        }

        return _keysymTable[row];
    }
}
