namespace AutomationStudio.Abstractions;

/// <summary>A point on the virtual screen, in physical pixels, origin top-left.</summary>
public readonly record struct ScreenPoint(int X, int Y);

/// <summary>A rectangle on the virtual screen, in physical pixels.</summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public ScreenPoint Center => new(X + Width / 2, Y + Height / 2);
}

public enum MouseButton
{
    Left,
    Right,
    Middle
}

/// <summary>
/// A platform-neutral logical key. Backends translate this to the native key code / keysym.
/// Covers the keys needed for Phase 1 (PressKey / KeyCombination); more are added as later
/// phases need them.
/// </summary>
public enum Key
{
    None = 0,

    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    Enter, Escape, Backspace, Tab, Space,
    Left, Right, Up, Down,
    Home, End, PageUp, PageDown, Insert, Delete,

    LeftControl, RightControl,
    LeftAlt, RightAlt,
    LeftShift, RightShift,
    LeftSuper, RightSuper,

    CapsLock, NumLock, ScrollLock,
    PrintScreen, Pause,

    Minus, Equals, LeftBracket, RightBracket, Backslash,
    Semicolon, Quote, Comma, Period, Slash, Grave
}

public enum ActionStatus
{
    Success,
    Failed,
    Timeout,
    Skipped,
    Cancelled
}
