using AutomationStudio.Abstractions;

namespace AutomationStudio.Applications.FileManager;

/// <summary>
/// Everything about the file manager the plugin needs beyond automation-engine logic (DAS.md
/// section 13). Unlike every other profile in this project, "the file manager" genuinely differs by
/// OS — Windows File Explorer (process "explorer") vs. GNOME Files/Nautilus on Linux (process
/// "nautilus") — so <see cref="Default"/> picks between <see cref="Windows"/> and
/// <see cref="Linux"/> at startup, mirroring how <c>PlatformAutomationEnvironment</c> already picks
/// an OS-appropriate backend. The keyboard shortcuts themselves are confirmed identical between the
/// two (Microsoft's own docs for Explorer; GNOME's own docs / omgubuntu.co.uk for Nautilus): Ctrl+
/// Shift+N new folder, Ctrl+C/X/V copy/cut/paste, F2 rename (auto-selects the existing name so a
/// typed replacement overwrites it), Delete removes the selection, Ctrl+F searches — only the
/// process name differs, so one shared field set covers both. A KDE Dolphin or other third-party
/// Linux file manager may bind some of these differently; not independently verified here.
/// </summary>
public sealed record FileManagerProfile
{
    public required string ProcessName { get; init; }

    public required Key NewFolderModifier1 { get; init; }
    public required Key NewFolderModifier2 { get; init; }
    public required Key NewFolderKey { get; init; }

    public required Key CopyModifier { get; init; }
    public required Key CopyKey { get; init; }
    public required Key CutModifier { get; init; }
    public required Key CutKey { get; init; }
    public required Key PasteModifier { get; init; }
    public required Key PasteKey { get; init; }

    public required Key RenameKey { get; init; }
    public required Key DeleteKey { get; init; }

    public required Key SearchModifier { get; init; }
    public required Key SearchKey { get; init; }

    public static FileManagerProfile Windows { get; } = new()
    {
        ProcessName = "explorer",
        NewFolderModifier1 = Key.LeftControl, NewFolderModifier2 = Key.LeftShift, NewFolderKey = Key.N,
        CopyModifier = Key.LeftControl, CopyKey = Key.C,
        CutModifier = Key.LeftControl, CutKey = Key.X,
        PasteModifier = Key.LeftControl, PasteKey = Key.V,
        RenameKey = Key.F2,
        DeleteKey = Key.Delete,
        SearchModifier = Key.LeftControl, SearchKey = Key.F,
    };

    public static FileManagerProfile Linux { get; } = Windows with { ProcessName = "nautilus" };

    public static FileManagerProfile Default { get; } = OperatingSystem.IsWindows() ? Windows : Linux;
}
