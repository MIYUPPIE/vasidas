using AutomationStudio.Abstractions;

namespace AutomationStudio.Applications.Chrome;

/// <summary>Everything about Chrome the plugin needs beyond automation-engine logic (DAS.md section 13). Process name is the common Linux/Windows binary name — a different distro packaging (e.g. Chromium under a different name) isn't independently verified.</summary>
public sealed record ChromeProfile
{
    public required string ProcessName { get; init; }

    public required Key NewTabModifier { get; init; }
    public required Key NewTabKey { get; init; }
    public required Key CloseTabModifier { get; init; }
    public required Key CloseTabKey { get; init; }
    public required Key ReloadModifier { get; init; }
    public required Key ReloadKey { get; init; }
    public required Key BackModifier { get; init; }
    public required Key BackKey { get; init; }
    public required Key ForwardModifier { get; init; }
    public required Key ForwardKey { get; init; }
    public required Key AddressBarModifier { get; init; }
    public required Key AddressBarKey { get; init; }
    public required Key FindModifier { get; init; }
    public required Key FindKey { get; init; }

    /// <summary>
    /// Confirmed Chrome default shortcuts (Google's own support page,
    /// support.google.com/chrome/answer/157179): Ctrl+T new tab, Ctrl+W close tab, Ctrl+R reload,
    /// Alt+Left/Right back/forward, Ctrl+L focuses and selects everything in the address bar (so a
    /// typed URL replaces it outright), Ctrl+F finds on the current page.
    /// </summary>
    public static ChromeProfile Default { get; } = new()
    {
        ProcessName = "chrome",
        NewTabModifier = Key.LeftControl, NewTabKey = Key.T,
        CloseTabModifier = Key.LeftControl, CloseTabKey = Key.W,
        ReloadModifier = Key.LeftControl, ReloadKey = Key.R,
        BackModifier = Key.LeftAlt, BackKey = Key.Left,
        ForwardModifier = Key.LeftAlt, ForwardKey = Key.Right,
        AddressBarModifier = Key.LeftControl, AddressBarKey = Key.L,
        FindModifier = Key.LeftControl, FindKey = Key.F,
    };
}
