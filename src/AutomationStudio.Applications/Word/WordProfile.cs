using AutomationStudio.Abstractions;

namespace AutomationStudio.Applications.Word;

/// <summary>Everything about Word the plugin needs beyond automation-engine logic (DAS.md section 13). Word is a Windows-only product here (no native Linux build) — same caveat class as VideoPsalm/PowerPoint, compile/logic-verified only.</summary>
public sealed record WordProfile
{
    public required string ProcessName { get; init; }

    public required Key NewDocumentModifier { get; init; }
    public required Key NewDocumentKey { get; init; }
    public required Key SaveModifier { get; init; }
    public required Key SaveKey { get; init; }
    public required Key PrintModifier { get; init; }
    public required Key PrintKey { get; init; }
    public required Key BoldModifier { get; init; }
    public required Key BoldKey { get; init; }
    public required Key ItalicModifier { get; init; }
    public required Key ItalicKey { get; init; }
    public required Key UnderlineModifier { get; init; }
    public required Key UnderlineKey { get; init; }
    public required Key FindModifier { get; init; }
    public required Key FindKey { get; init; }

    /// <summary>Confirmed Microsoft-documented shortcuts (support.microsoft.com/.../keyboard-shortcuts-in-word), standard across Word versions: Ctrl+N new document, Ctrl+S save, Ctrl+P print, Ctrl+B/I/U bold/italic/underline (toggle the selection, or the next typed text if nothing is selected), Ctrl+F find.</summary>
    public static WordProfile Default { get; } = new()
    {
        ProcessName = "WINWORD",
        NewDocumentModifier = Key.LeftControl, NewDocumentKey = Key.N,
        SaveModifier = Key.LeftControl, SaveKey = Key.S,
        PrintModifier = Key.LeftControl, PrintKey = Key.P,
        BoldModifier = Key.LeftControl, BoldKey = Key.B,
        ItalicModifier = Key.LeftControl, ItalicKey = Key.I,
        UnderlineModifier = Key.LeftControl, UnderlineKey = Key.U,
        FindModifier = Key.LeftControl, FindKey = Key.F,
    };
}
