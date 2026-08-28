using System.Runtime.Versioning;
using AutomationStudio.Abstractions;
using AutomationStudio.Windows.Win32;

namespace AutomationStudio.Windows.Input;

/// <summary>
/// Mouse and keyboard injection via <c>SendInput</c>. Free text goes through <c>KEYEVENTF_UNICODE</c>
/// rather than virtual-key codes, so typing is correct for any Unicode string regardless of the
/// active keyboard layout (surrogate pairs are sent as two consecutive UTF-16 code units, matching
/// how Windows itself expects supplementary-plane characters).
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsInputController
{
    public void MoveMouse(ScreenPoint position) => SendMouseMove(position);

    public void MouseDown(MouseButton button, ScreenPoint? position)
    {
        if (position is { } p)
        {
            SendMouseMove(p);
        }

        SendMouseButton(button, isDown: true);
    }

    public void MouseUp(MouseButton button, ScreenPoint? position)
    {
        if (position is { } p)
        {
            SendMouseMove(p);
        }

        SendMouseButton(button, isDown: false);
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
        var inputs = new List<NativeMethods.Input>(2);
        if (deltaY != 0)
        {
            inputs.Add(MouseWheelInput(NativeMethods.MouseEventFWheel, deltaY));
        }

        if (deltaX != 0)
        {
            inputs.Add(MouseWheelInput(NativeMethods.MouseEventFHWheel, deltaX));
        }

        if (inputs.Count > 0)
        {
            Send(inputs.ToArray());
        }
    }

    public void PressKey(Key key)
    {
        var vk = VirtualKeyMapper.Resolve(key);
        Send(
            KeyboardInput(vk.Code, vk.IsExtended, isDown: true),
            KeyboardInput(vk.Code, vk.IsExtended, isDown: false));
    }

    public void KeyCombination(IReadOnlyList<Key> keys)
    {
        var resolved = keys.Select(VirtualKeyMapper.Resolve).ToArray();
        var inputs = new NativeMethods.Input[resolved.Length * 2];

        for (var i = 0; i < resolved.Length; i++)
        {
            inputs[i] = KeyboardInput(resolved[i].Code, resolved[i].IsExtended, isDown: true);
        }

        for (var i = 0; i < resolved.Length; i++)
        {
            var key = resolved[resolved.Length - 1 - i];
            inputs[resolved.Length + i] = KeyboardInput(key.Code, key.IsExtended, isDown: false);
        }

        Send(inputs);
    }

    public void TypeText(string text)
    {
        var inputs = new NativeMethods.Input[text.Length * 2];
        for (var i = 0; i < text.Length; i++)
        {
            inputs[i * 2] = UnicodeCharInput(text[i], isDown: true);
            inputs[i * 2 + 1] = UnicodeCharInput(text[i], isDown: false);
        }

        if (inputs.Length > 0)
        {
            Send(inputs);
        }
    }

    private static void SendMouseMove(ScreenPoint position)
    {
        var virtualLeft = NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen);
        var virtualTop = NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen);
        var virtualWidth = NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen);
        var virtualHeight = NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen);

        var normalizedX = (int)(((position.X - virtualLeft) * 65535.0) / Math.Max(1, virtualWidth - 1));
        var normalizedY = (int)(((position.Y - virtualTop) * 65535.0) / Math.Max(1, virtualHeight - 1));

        Send(new NativeMethods.Input
        {
            type = NativeMethods.InputMouse,
            u = new NativeMethods.InputUnion
            {
                mi = new NativeMethods.MouseInput
                {
                    dx = normalizedX,
                    dy = normalizedY,
                    dwFlags = NativeMethods.MouseEventFMove | NativeMethods.MouseEventFAbsolute | NativeMethods.MouseEventFVirtualDesk
                }
            }
        });
    }

    private static void SendMouseButton(MouseButton button, bool isDown)
    {
        var flag = (button, isDown) switch
        {
            (MouseButton.Left, true) => NativeMethods.MouseEventFLeftDown,
            (MouseButton.Left, false) => NativeMethods.MouseEventFLeftUp,
            (MouseButton.Right, true) => NativeMethods.MouseEventFRightDown,
            (MouseButton.Right, false) => NativeMethods.MouseEventFRightUp,
            (MouseButton.Middle, true) => NativeMethods.MouseEventFMiddleDown,
            (MouseButton.Middle, false) => NativeMethods.MouseEventFMiddleUp,
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, null)
        };

        Send(new NativeMethods.Input
        {
            type = NativeMethods.InputMouse,
            u = new NativeMethods.InputUnion { mi = new NativeMethods.MouseInput { dwFlags = flag } }
        });
    }

    private static NativeMethods.Input MouseWheelInput(uint eventFlag, int delta) => new()
    {
        type = NativeMethods.InputMouse,
        u = new NativeMethods.InputUnion
        {
            mi = new NativeMethods.MouseInput
            {
                dwFlags = eventFlag,
                mouseData = unchecked((uint)(delta * NativeMethods.WheelDelta))
            }
        }
    };

    private static NativeMethods.Input KeyboardInput(ushort vk, bool isExtended, bool isDown) => new()
    {
        type = NativeMethods.InputKeyboard,
        u = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KeybdInput
            {
                wVk = vk,
                dwFlags = (isExtended ? NativeMethods.KeyEventFExtendedKey : 0) | (isDown ? 0 : NativeMethods.KeyEventFKeyUp)
            }
        }
    };

    private static NativeMethods.Input UnicodeCharInput(char ch, bool isDown) => new()
    {
        type = NativeMethods.InputKeyboard,
        u = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KeybdInput
            {
                wVk = 0,
                wScan = ch,
                dwFlags = NativeMethods.KeyEventFUnicode | (isDown ? 0 : NativeMethods.KeyEventFKeyUp)
            }
        }
    };

    private static void Send(params NativeMethods.Input[] inputs)
    {
        if (inputs.Length == 0)
        {
            return;
        }

        var structSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.Input>();
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, structSize);
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException(
                $"SendInput only accepted {sent} of {inputs.Length} events (Win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).");
        }
    }
}
