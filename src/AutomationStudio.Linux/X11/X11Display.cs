namespace AutomationStudio.Linux.X11;

/// <summary>
/// Owns one Xlib <c>Display*</c> connection. Xlib itself is not thread-safe unless
/// <c>XInitThreads</c> is called; since the workflow engine executes actions sequentially
/// (design doc section 7) that has been unnecessary so far, but every call still goes through
/// <see cref="Lock"/> as cheap insurance against a future caller invoking two actions concurrently.
/// </summary>
internal sealed class X11Display : IDisposable
{
    public readonly object Lock = new();
    public IntPtr Handle { get; }
    public int Screen { get; }
    public IntPtr RootWindow { get; }
    public int ScreenWidth { get; }
    public int ScreenHeight { get; }

    private X11Display(IntPtr handle)
    {
        Handle = handle;
        Screen = NativeMethods.XDefaultScreen(handle);
        RootWindow = NativeMethods.XDefaultRootWindow(handle);
        ScreenWidth = NativeMethods.XDisplayWidth(handle, Screen);
        ScreenHeight = NativeMethods.XDisplayHeight(handle, Screen);
    }

    /// <summary>
    /// Opens the X11 display named by $DISPLAY. Returns null (rather than throwing) when no X11
    /// display is reachable — a purely-native-Wayland session with no XWayland — so callers can
    /// report that clearly via <see cref="AutomationStudio.Abstractions.ICapabilityProvider"/>
    /// instead of crashing (section 31/32).
    /// </summary>
    public static X11Display? TryOpen()
    {
        X11ErrorHandling.EnsureInstalled();
        var handle = NativeMethods.XOpenDisplay(null);
        return handle == IntPtr.Zero ? null : new X11Display(handle);
    }

    public bool HasXTest()
    {
        lock (Lock)
        {
            return NativeMethods.XTestQueryExtension(Handle, out _, out _, out _, out _);
        }
    }

    public void Dispose()
    {
        lock (Lock)
        {
            NativeMethods.XCloseDisplay(Handle);
        }
    }
}
