using AutomationStudio.Abstractions;

namespace AutomationStudio.Linux.X11;

/// <summary>
/// Maps the platform-neutral <see cref="Key"/> enum to X11 keysyms, and arbitrary Unicode
/// characters to the "Unicode keysym" range X11 defines for exactly this purpose. Named keys
/// (Return, Escape, F1, ...) are resolved through <c>XStringToKeysym</c> against X11's own name
/// table rather than transcribed hex constants, so a typo here fails loudly instead of silently
/// binding the wrong key.
/// </summary>
internal static class KeysymMapper
{
    private static readonly Dictionary<Key, string> NamedKeysyms = new()
    {
        [Key.Enter] = "Return",
        [Key.Escape] = "Escape",
        [Key.Backspace] = "BackSpace",
        [Key.Tab] = "Tab",
        [Key.Left] = "Left",
        [Key.Right] = "Right",
        [Key.Up] = "Up",
        [Key.Down] = "Down",
        [Key.Home] = "Home",
        [Key.End] = "End",
        [Key.PageUp] = "Page_Up",
        [Key.PageDown] = "Page_Down",
        [Key.Insert] = "Insert",
        [Key.Delete] = "Delete",
        [Key.LeftControl] = "Control_L",
        [Key.RightControl] = "Control_R",
        [Key.LeftAlt] = "Alt_L",
        [Key.RightAlt] = "Alt_R",
        [Key.LeftShift] = "Shift_L",
        [Key.RightShift] = "Shift_R",
        [Key.LeftSuper] = "Super_L",
        [Key.RightSuper] = "Super_R",
        [Key.CapsLock] = "Caps_Lock",
        [Key.NumLock] = "Num_Lock",
        [Key.ScrollLock] = "Scroll_Lock",
        [Key.PrintScreen] = "Print",
        [Key.Pause] = "Pause",
        [Key.F1] = "F1",
        [Key.F2] = "F2",
        [Key.F3] = "F3",
        [Key.F4] = "F4",
        [Key.F5] = "F5",
        [Key.F6] = "F6",
        [Key.F7] = "F7",
        [Key.F8] = "F8",
        [Key.F9] = "F9",
        [Key.F10] = "F10",
        [Key.F11] = "F11",
        [Key.F12] = "F12",
    };

    private static readonly Dictionary<Key, char> DirectCharKeys = new()
    {
        [Key.A] = 'a', [Key.B] = 'b', [Key.C] = 'c', [Key.D] = 'd', [Key.E] = 'e',
        [Key.F] = 'f', [Key.G] = 'g', [Key.H] = 'h', [Key.I] = 'i', [Key.J] = 'j',
        [Key.K] = 'k', [Key.L] = 'l', [Key.M] = 'm', [Key.N] = 'n', [Key.O] = 'o',
        [Key.P] = 'p', [Key.Q] = 'q', [Key.R] = 'r', [Key.S] = 's', [Key.T] = 't',
        [Key.U] = 'u', [Key.V] = 'v', [Key.W] = 'w', [Key.X] = 'x', [Key.Y] = 'y',
        [Key.Z] = 'z',
        [Key.D0] = '0', [Key.D1] = '1', [Key.D2] = '2', [Key.D3] = '3', [Key.D4] = '4',
        [Key.D5] = '5', [Key.D6] = '6', [Key.D7] = '7', [Key.D8] = '8', [Key.D9] = '9',
        [Key.Space] = ' ',
        [Key.Minus] = '-', [Key.Equals] = '=', [Key.LeftBracket] = '[', [Key.RightBracket] = ']',
        [Key.Backslash] = '\\', [Key.Semicolon] = ';', [Key.Quote] = '\'', [Key.Comma] = ',',
        [Key.Period] = '.', [Key.Slash] = '/', [Key.Grave] = '`',
    };

    public static nuint ResolveKeysym(Key key)
    {
        if (DirectCharKeys.TryGetValue(key, out var ch))
        {
            return CharToKeysym(ch);
        }

        if (NamedKeysyms.TryGetValue(key, out var name))
        {
            var keysym = NativeMethods.XStringToKeysym(name);
            if (keysym == 0)
            {
                throw new InvalidOperationException($"X11 does not recognise keysym name '{name}' for {key}.");
            }

            return keysym;
        }

        throw new NotSupportedException($"Key '{key}' has no known X11 keysym mapping.");
    }

    /// <summary>
    /// Converts a Unicode code point to its X11 keysym per the "Unicode keysym" convention
    /// (keysymdef.h): Latin-1 code points map 1:1, everything above 0x00FF is <c>0x01000000 + cp</c>.
    /// </summary>
    public static nuint CodepointToKeysym(int codepoint)
    {
        if (codepoint is >= 0x20 and <= 0xFF)
        {
            return (nuint)codepoint;
        }

        return (nuint)(0x01000000 + codepoint);
    }

    private static nuint CharToKeysym(char ch) => CodepointToKeysym(ch);
}
