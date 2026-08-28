using System.Runtime.InteropServices;
using AutomationStudio.Abstractions;

namespace AutomationStudio.Linux.X11;

/// <summary>
/// Mouse and keyboard injection via the XTest extension. Requires an X11 display — either a
/// native X11 session or XWayland under a Wayland compositor. On a pure-Wayland session with no
/// XWayland, <see cref="X11Display.TryOpen"/> returns null upstream and
/// <see cref="AutomationStudio.Linux.Capabilities.LinuxCapabilityProvider"/> reports mouse/keyboard
/// injection as Restricted rather than this class being constructed at all (section 31).
/// </summary>
internal sealed class X11InputController
{
    private const uint LeftButton = 1;
    private const uint MiddleButton = 2;
    private const uint RightButton = 3;
    private const uint ScrollUpButton = 4;
    private const uint ScrollDownButton = 5;
    private const uint ScrollLeftButton = 6;
    private const uint ScrollRightButton = 7;

    private readonly X11Display _display;

    /// <summary>The keycode we temporarily remap to type characters not already bound in the active layout.</summary>
    private byte? _scratchKeycode;

    public X11InputController(X11Display display)
    {
        _display = display;
    }

    public void MoveMouse(ScreenPoint position)
    {
        lock (_display.Lock)
        {
            NativeMethods.XTestFakeMotionEvent(_display.Handle, _display.Screen, position.X, position.Y, 0);
            NativeMethods.XFlush(_display.Handle);
        }
    }

    public void MouseDown(MouseButton button, ScreenPoint? position)
    {
        lock (_display.Lock)
        {
            if (position is { } p)
            {
                NativeMethods.XTestFakeMotionEvent(_display.Handle, _display.Screen, p.X, p.Y, 0);
            }

            NativeMethods.XTestFakeButtonEvent(_display.Handle, ToButtonNumber(button), true, 0);
            NativeMethods.XFlush(_display.Handle);
        }
    }

    public void MouseUp(MouseButton button, ScreenPoint? position)
    {
        lock (_display.Lock)
        {
            if (position is { } p)
            {
                NativeMethods.XTestFakeMotionEvent(_display.Handle, _display.Screen, p.X, p.Y, 0);
            }

            NativeMethods.XTestFakeButtonEvent(_display.Handle, ToButtonNumber(button), false, 0);
            NativeMethods.XFlush(_display.Handle);
        }
    }

    public void Click(MouseButton button, ScreenPoint? position)
    {
        MouseDown(button, position);
        MouseUp(button, null);
    }

    public void DoubleClick(ScreenPoint? position)
    {
        Click(MouseButton.Left, position);
        Click(MouseButton.Left, null);
    }

    public void Scroll(int deltaX, int deltaY)
    {
        lock (_display.Lock)
        {
            FireWheel(deltaY < 0 ? ScrollDownButton : ScrollUpButton, Math.Abs(deltaY));
            FireWheel(deltaX < 0 ? ScrollLeftButton : ScrollRightButton, Math.Abs(deltaX));
            NativeMethods.XFlush(_display.Handle);
        }

        void FireWheel(uint button, int clicks)
        {
            for (var i = 0; i < clicks; i++)
            {
                NativeMethods.XTestFakeButtonEvent(_display.Handle, button, true, 0);
                NativeMethods.XTestFakeButtonEvent(_display.Handle, button, false, 0);
            }
        }
    }

    public void PressKey(Key key)
    {
        var keysym = KeysymMapper.ResolveKeysym(key);
        lock (_display.Lock)
        {
            TapKeysym(keysym);
        }
    }

    public void KeyCombination(IReadOnlyList<Key> keys)
    {
        var keycodes = new byte[keys.Count];

        lock (_display.Lock)
        {
            for (var i = 0; i < keys.Count; i++)
            {
                keycodes[i] = EnsureKeycodeForKeysym(KeysymMapper.ResolveKeysym(keys[i]));
            }

            for (var i = 0; i < keycodes.Length; i++)
            {
                NativeMethods.XTestFakeKeyEvent(_display.Handle, keycodes[i], true, 0);
            }

            for (var i = keycodes.Length - 1; i >= 0; i--)
            {
                NativeMethods.XTestFakeKeyEvent(_display.Handle, keycodes[i], false, 0);
            }

            NativeMethods.XFlush(_display.Handle);
        }
    }

    public void TypeText(string text)
    {
        lock (_display.Lock)
        {
            var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
            while (enumerator.MoveNext())
            {
                var element = (string)enumerator.Current;
                foreach (var rune in element.EnumerateRunes())
                {
                    TapKeysym(KeysymMapper.CodepointToKeysym(rune.Value));
                }
            }

            NativeMethods.XFlush(_display.Handle);
        }
    }

    /// <summary>Caller must hold <see cref="X11Display.Lock"/>.</summary>
    private void TapKeysym(nuint keysym)
    {
        var keycode = EnsureKeycodeForKeysym(keysym);
        NativeMethods.XTestFakeKeyEvent(_display.Handle, keycode, true, 0);
        NativeMethods.XTestFakeKeyEvent(_display.Handle, keycode, false, 0);
    }

    /// <summary>
    /// Returns a keycode bound to <paramref name="keysym"/>, remapping our scratch keycode to it
    /// on demand when the active layout has no key for it (e.g. most Unicode outside Latin-1).
    /// Caller must hold <see cref="X11Display.Lock"/>.
    /// </summary>
    private byte EnsureKeycodeForKeysym(nuint keysym)
    {
        var existing = NativeMethods.XKeysymToKeycode(_display.Handle, keysym);
        if (existing != 0)
        {
            return existing;
        }

        var scratch = _scratchKeycode ??= FindScratchKeycode();
        NativeMethods.XChangeKeyboardMapping(_display.Handle, scratch, 1, [keysym], 1);
        NativeMethods.XSync(_display.Handle, false);
        return scratch;
    }

    /// <summary>Finds a keycode the active layout has left unbound, to reuse as our remap slot. Caller must hold the lock.</summary>
    private byte FindScratchKeycode()
    {
        NativeMethods.XDisplayKeycodes(_display.Handle, out var min, out var max);
        var mapPtr = NativeMethods.XGetKeyboardMapping(_display.Handle, (byte)min, max - min + 1, out var keysymsPerKeycode);

        try
        {
            for (var keycode = max; keycode >= min; keycode--)
            {
                var offset = (keycode - min) * keysymsPerKeycode;
                var firstKeysym = (nuint)Marshal.ReadIntPtr(mapPtr, offset * IntPtr.Size);
                if (firstKeysym == 0)
                {
                    return (byte)keycode;
                }
            }
        }
        finally
        {
            NativeMethods.XFree(mapPtr);
        }

        // Every keycode in the layout is bound to something; fall back to the highest one.
        // Typing will temporarily steal whatever key lives there, which is the same trade-off
        // tools like xdotool make when a keyboard has no spare keycodes at all.
        return (byte)max;
    }

    private static uint ToButtonNumber(MouseButton button) => button switch
    {
        MouseButton.Left => LeftButton,
        MouseButton.Middle => MiddleButton,
        MouseButton.Right => RightButton,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, null)
    };
}
