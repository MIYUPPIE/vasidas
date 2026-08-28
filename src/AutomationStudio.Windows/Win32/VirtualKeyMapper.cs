using AutomationStudio.Abstractions;

namespace AutomationStudio.Windows.Win32;

/// <summary>
/// Maps the platform-neutral <see cref="Key"/> enum to Win32 virtual-key codes for
/// <c>PressKey</c>/<c>KeyCombination</c>. Free-text typing does not go through this table — it uses
/// <c>KEYEVENTF_UNICODE</c> instead (see <see cref="AutomationStudio.Windows.Input.WindowsInputController"/>),
/// which sidesteps keyboard-layout dependence entirely.
/// </summary>
internal static class VirtualKeyMapper
{
    public readonly record struct VirtualKey(ushort Code, bool IsExtended);

    private static readonly Dictionary<Key, VirtualKey> Map = new()
    {
        [Key.A] = new(0x41, false), [Key.B] = new(0x42, false), [Key.C] = new(0x43, false),
        [Key.D] = new(0x44, false), [Key.E] = new(0x45, false), [Key.F] = new(0x46, false),
        [Key.G] = new(0x47, false), [Key.H] = new(0x48, false), [Key.I] = new(0x49, false),
        [Key.J] = new(0x4A, false), [Key.K] = new(0x4B, false), [Key.L] = new(0x4C, false),
        [Key.M] = new(0x4D, false), [Key.N] = new(0x4E, false), [Key.O] = new(0x4F, false),
        [Key.P] = new(0x50, false), [Key.Q] = new(0x51, false), [Key.R] = new(0x52, false),
        [Key.S] = new(0x53, false), [Key.T] = new(0x54, false), [Key.U] = new(0x55, false),
        [Key.V] = new(0x56, false), [Key.W] = new(0x57, false), [Key.X] = new(0x58, false),
        [Key.Y] = new(0x59, false), [Key.Z] = new(0x5A, false),

        [Key.D0] = new(0x30, false), [Key.D1] = new(0x31, false), [Key.D2] = new(0x32, false),
        [Key.D3] = new(0x33, false), [Key.D4] = new(0x34, false), [Key.D5] = new(0x35, false),
        [Key.D6] = new(0x36, false), [Key.D7] = new(0x37, false), [Key.D8] = new(0x38, false),
        [Key.D9] = new(0x39, false),

        [Key.F1] = new(0x70, false), [Key.F2] = new(0x71, false), [Key.F3] = new(0x72, false),
        [Key.F4] = new(0x73, false), [Key.F5] = new(0x74, false), [Key.F6] = new(0x75, false),
        [Key.F7] = new(0x76, false), [Key.F8] = new(0x77, false), [Key.F9] = new(0x78, false),
        [Key.F10] = new(0x79, false), [Key.F11] = new(0x7A, false), [Key.F12] = new(0x7B, false),

        [Key.Enter] = new(0x0D, false),
        [Key.Escape] = new(0x1B, false),
        [Key.Backspace] = new(0x08, false),
        [Key.Tab] = new(0x09, false),
        [Key.Space] = new(0x20, false),

        // Navigation keys are "extended" on the standard 101/104-key layout (they exist twice —
        // main block and numpad — and the extended flag is how Windows tells them apart).
        [Key.Left] = new(0x25, true),
        [Key.Up] = new(0x26, true),
        [Key.Right] = new(0x27, true),
        [Key.Down] = new(0x28, true),
        [Key.Home] = new(0x24, true),
        [Key.End] = new(0x23, true),
        [Key.PageUp] = new(0x21, true),
        [Key.PageDown] = new(0x22, true),
        [Key.Insert] = new(0x2D, true),
        [Key.Delete] = new(0x2E, true),

        [Key.LeftControl] = new(0xA2, false),
        [Key.RightControl] = new(0xA3, true),
        [Key.LeftAlt] = new(0xA4, false),
        [Key.RightAlt] = new(0xA5, true),
        [Key.LeftShift] = new(0xA0, false),
        [Key.RightShift] = new(0xA1, false),
        [Key.LeftSuper] = new(0x5B, true),
        [Key.RightSuper] = new(0x5C, true),

        [Key.CapsLock] = new(0x14, false),
        [Key.NumLock] = new(0x90, true),
        [Key.ScrollLock] = new(0x91, false),
        [Key.PrintScreen] = new(0x2C, true),
        [Key.Pause] = new(0x13, false),

        [Key.Minus] = new(0xBD, false),
        [Key.Equals] = new(0xBB, false),
        [Key.LeftBracket] = new(0xDB, false),
        [Key.RightBracket] = new(0xDD, false),
        [Key.Backslash] = new(0xDC, false),
        [Key.Semicolon] = new(0xBA, false),
        [Key.Quote] = new(0xDE, false),
        [Key.Comma] = new(0xBC, false),
        [Key.Period] = new(0xBE, false),
        [Key.Slash] = new(0xBF, false),
        [Key.Grave] = new(0xC0, false),
    };

    public static VirtualKey Resolve(Key key) =>
        Map.TryGetValue(key, out var vk)
            ? vk
            : throw new NotSupportedException($"Key '{key}' has no known Windows virtual-key mapping.");
}
