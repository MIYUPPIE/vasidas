using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using AutomationStudio.Abstractions;
using AutomationStudio.Windows.Win32;

namespace AutomationStudio.Windows.Input;

/// <summary>
/// Global mouse/keyboard capture via <c>WH_KEYBOARD_LL</c>/<c>WH_MOUSE_LL</c> (DAS.md section 25) —
/// the observation counterpart to <see cref="WindowsInputController"/>'s injection. Low-level hooks
/// only deliver events to the thread that installed them, and only while that thread is pumping
/// messages — not a background <c>Task</c>, a real message loop — so this owns a dedicated
/// <see cref="Thread"/> for its whole recording lifetime, matching the same
/// "single thread owns this OS resource end to end" shape as <see cref="AutomationStudio.Linux.X11.X11InputRecorder"/>.
/// Compile-verified only, like every other Windows-only component in this codebase (no Windows
/// machine available to run it on) — the live end-to-end verification this got on Linux (real XTest
/// events captured through the real RECORD extension) has no equivalent here yet.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsInputRecorder : IInputRecorder
{
    private readonly object _lock = new();
    private readonly ManualResetEventSlim _hooksInstalled = new(false);

    private Thread? _thread;
    private uint _hookThreadId;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private List<RecordedInputEvent>? _events;
    private long _startTimestampTicks;
    private ScreenPoint _lastMousePosition;
    private Exception? _installError;

    // Rooted for the lifetime of an active recording — same GC-lifetime lesson as X11InputRecorder's
    // _callback: the OS calls these repeatedly for as long as the hooks are installed.
    private NativeMethods.HookProc? _keyboardProc;
    private NativeMethods.HookProc? _mouseProc;

    public void Start()
    {
        lock (_lock)
        {
            if (_thread is not null)
            {
                throw new InvalidOperationException("Already recording.");
            }

            _events = [];
            _startTimestampTicks = DateTimeOffset.UtcNow.Ticks;
            _lastMousePosition = default;
            _hooksInstalled.Reset();
            _installError = null;
            _keyboardProc = KeyboardHookProc;
            _mouseProc = MouseHookProc;

            _thread = new Thread(RunMessageLoop) { IsBackground = true, Name = "WindowsInputRecorder" };
            _thread.Start();
        }

        if (!_hooksInstalled.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new InvalidOperationException("Timed out installing the global input hooks.");
        }

        if (_installError is { } error)
        {
            lock (_lock)
            {
                _thread = null;
            }

            throw new InvalidOperationException("Failed to install global input hooks.", error);
        }
    }

    public IReadOnlyList<RecordedInputEvent> Stop()
    {
        Thread thread;
        uint threadId;
        lock (_lock)
        {
            if (_thread is null)
            {
                throw new InvalidOperationException("Not currently recording.");
            }

            thread = _thread;
            threadId = _hookThreadId;
        }

        // Unblocks the GetMessage loop on the hook-owning thread, which then unhooks and returns.
        NativeMethods.PostThreadMessage(threadId, NativeMethods.WmQuit, IntPtr.Zero, IntPtr.Zero);
        thread.Join();

        lock (_lock)
        {
            _thread = null;
            _keyboardProc = null;
            _mouseProc = null;

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

        _hooksInstalled.Dispose();
    }

    private void RunMessageLoop()
    {
        _hookThreadId = NativeMethods.GetCurrentThreadId();

        var moduleHandle = NativeMethods.GetModuleHandle(null);
        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _keyboardProc!, moduleHandle, 0);
        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _mouseProc!, moduleHandle, 0);

        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            _installError = new InvalidOperationException($"SetWindowsHookEx failed (Win32 error {Marshal.GetLastWin32Error()}).");
            if (_keyboardHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            }

            if (_mouseHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_mouseHook);
            }

            _hooksInstalled.Set();
            return;
        }

        _hooksInstalled.Set();

        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        NativeMethods.UnhookWindowsHookEx(_keyboardHook);
        NativeMethods.UnhookWindowsHookEx(_mouseHook);
    }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var info = Marshal.PtrToStructure<NativeMethods.KbdllHookStruct>(lParam);
                var evt = BuildKeyEvent((int)wParam, info);
                if (evt is not null)
                {
                    lock (_lock)
                    {
                        _events?.Add(evt);
                    }
                }
            }
            catch
            {
                // A hook procedure must never throw back into the OS's hook-chain dispatch — same
                // lesson as X11InputRecorder's callback.
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var info = Marshal.PtrToStructure<NativeMethods.MsllHookStruct>(lParam);
                var position = new ScreenPoint(info.pt.X, info.pt.Y);
                _lastMousePosition = position;

                var evt = BuildMouseEvent((int)wParam, info, position);
                if (evt is not null)
                {
                    lock (_lock)
                    {
                        _events?.Add(evt);
                    }
                }
            }
            catch
            {
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private RecordedInputEvent? BuildKeyEvent(int message, NativeMethods.KbdllHookStruct info)
    {
        RecordedInputEventKind kind;
        if (message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown)
        {
            kind = RecordedInputEventKind.KeyDown;
        }
        else if (message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp)
        {
            kind = RecordedInputEventKind.KeyUp;
        }
        else
        {
            return null;
        }

        var vkCode = (ushort)info.vkCode;
        var key = VirtualKeyMapper.TryReverse(vkCode);
        var character = kind == RecordedInputEventKind.KeyDown ? ResolveCharacter(vkCode, info.scanCode) : null;

        var elapsed = TimeSpan.FromTicks(DateTimeOffset.UtcNow.Ticks - _startTimestampTicks);
        return new RecordedInputEvent(kind, elapsed, _lastMousePosition, Key: key, Character: character);
    }

    private RecordedInputEvent? BuildMouseEvent(int message, NativeMethods.MsllHookStruct info, ScreenPoint position)
    {
        var elapsed = TimeSpan.FromTicks(DateTimeOffset.UtcNow.Ticks - _startTimestampTicks);

        switch (message)
        {
            case NativeMethods.WmMouseMove:
                return new RecordedInputEvent(RecordedInputEventKind.MouseMove, elapsed, position);
            case NativeMethods.WmLButtonDown:
                return new RecordedInputEvent(RecordedInputEventKind.MouseDown, elapsed, position, Button: MouseButton.Left);
            case NativeMethods.WmLButtonUp:
                return new RecordedInputEvent(RecordedInputEventKind.MouseUp, elapsed, position, Button: MouseButton.Left);
            case NativeMethods.WmRButtonDown:
                return new RecordedInputEvent(RecordedInputEventKind.MouseDown, elapsed, position, Button: MouseButton.Right);
            case NativeMethods.WmRButtonUp:
                return new RecordedInputEvent(RecordedInputEventKind.MouseUp, elapsed, position, Button: MouseButton.Right);
            case NativeMethods.WmMButtonDown:
                return new RecordedInputEvent(RecordedInputEventKind.MouseDown, elapsed, position, Button: MouseButton.Middle);
            case NativeMethods.WmMButtonUp:
                return new RecordedInputEvent(RecordedInputEventKind.MouseUp, elapsed, position, Button: MouseButton.Middle);

            case NativeMethods.WmMouseWheel:
            {
                var ticks = unchecked((short)(info.mouseData >> 16)) / NativeMethods.WheelDelta;
                return ticks == 0 ? null : new RecordedInputEvent(RecordedInputEventKind.MouseScroll, elapsed, position, ScrollDeltaY: ticks);
            }

            case NativeMethods.WmMouseHWheel:
            {
                var ticks = unchecked((short)(info.mouseData >> 16)) / NativeMethods.WheelDelta;
                return ticks == 0 ? null : new RecordedInputEvent(RecordedInputEventKind.MouseScroll, elapsed, position, ScrollDeltaX: ticks);
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// <c>ToUnicode</c> is the simplest correct way to turn a captured VK code into the character it
    /// actually produces under the active keyboard layout and shift state (mirrors how a real
    /// keystroke gets translated). Known, documented trade-off: it consumes dead-key state, so a
    /// global hook calling it can disrupt an in-progress accent/diacritic sequence the *foreground*
    /// application was composing — the standard caveat every low-level keyboard hook that wants
    /// actual characters (not just VK codes) runs into, not something specific to this recorder.
    /// </summary>
    private static string? ResolveCharacter(ushort vkCode, uint scanCode)
    {
        var keyboardState = new byte[256];
        if (!NativeMethods.GetKeyboardState(keyboardState))
        {
            return null;
        }

        var buffer = new StringBuilder(8);
        var result = NativeMethods.ToUnicode(vkCode, scanCode, keyboardState, buffer, buffer.Capacity, 0);
        return result > 0 ? buffer.ToString(0, result) : null;
    }
}
