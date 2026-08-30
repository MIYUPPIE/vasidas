using AutomationStudio.Abstractions;

namespace AutomationStudio.Applications.Excel;

/// <summary>Everything about Excel the plugin needs beyond automation-engine logic (DAS.md section 13). Excel is a Windows-only product here (no native Linux build) — same caveat class as VideoPsalm/PowerPoint/Word, compile/logic-verified only.</summary>
public sealed record ExcelProfile
{
    public required string ProcessName { get; init; }

    public required Key NewWorkbookModifier { get; init; }
    public required Key NewWorkbookKey { get; init; }
    public required Key SaveModifier { get; init; }
    public required Key SaveKey { get; init; }
    public required Key GoToModifier { get; init; }
    public required Key GoToKey { get; init; }
    public required Key AutoSumModifier { get; init; }
    public required Key AutoSumKey { get; init; }
    public required Key BoldModifier { get; init; }
    public required Key BoldKey { get; init; }
    public required Key FindModifier { get; init; }
    public required Key FindKey { get; init; }

    /// <summary>Confirmed Microsoft-documented shortcuts (support.microsoft.com/.../keyboard-shortcuts-in-excel): Ctrl+N new workbook, Ctrl+S save, Ctrl+G opens "Go To" (type a cell reference, e.g. B14, then Enter), Alt+= inserts an AutoSum formula, Ctrl+B bold, Ctrl+F find.</summary>
    public static ExcelProfile Default { get; } = new()
    {
        ProcessName = "EXCEL",
        NewWorkbookModifier = Key.LeftControl, NewWorkbookKey = Key.N,
        SaveModifier = Key.LeftControl, SaveKey = Key.S,
        GoToModifier = Key.LeftControl, GoToKey = Key.G,
        AutoSumModifier = Key.LeftAlt, AutoSumKey = Key.Equals,
        BoldModifier = Key.LeftControl, BoldKey = Key.B,
        FindModifier = Key.LeftControl, FindKey = Key.F,
    };
}
