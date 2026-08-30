using System.Text.Json;
using AutomationStudio.Abstractions;

namespace AutomationStudio.Applications.VideoPsalm;

/// <summary>
/// Everything about VideoPsalm the plugin needs that isn't automation-engine logic (DAS.md section
/// 13: "Application profiles are now critical, not optional"). Every value here is documented,
/// sourced from VideoPsalm's own published shortcut list — see the doc comment on
/// <see cref="Default"/> for exactly what's confirmed vs. best-effort. Loadable from JSON so a
/// mismatch (a version update, a different keyboard layout) can be corrected without a code change.
/// </summary>
public sealed record VideoPsalmProfile
{
    public required string WindowTitleContains { get; init; }
    public required string ProcessName { get; init; }

    public required Key FocusSearchModifier { get; init; }
    public required Key FocusSearchKey { get; init; }

    public required Key BiblesPanelModifier { get; init; }
    public required Key BiblesPanelKey { get; init; }

    public required Key SongbooksPanelModifier { get; init; }
    public required Key SongbooksPanelKey { get; init; }

    public required Key ConfirmSelectionKey { get; init; }

    public required Key NextSlideKey { get; init; }
    public required Key PreviousSlideKey { get; init; }

    public required Key NextAgendaItemModifier { get; init; }
    public required Key NextAgendaItemKey { get; init; }
    public required Key PreviousAgendaItemKey { get; init; }

    public required Key BlackScreenKey { get; init; }
    public required Key ExitPresentationKey { get; init; }

    /// <summary>
    /// Real, published VideoPsalm keyboard shortcuts (https://myvideopsalm.weebly.com/blog/how-to-control-videopsalm-with-a-keyboard-without-a-mouse,
    /// https://myvideopsalm.weebly.com/how-to-display-song-lyrics-and-bible-verses.html), confirmed
    /// via research before use — this project has no VideoPsalm install to test against directly
    /// (it appears Windows-only; a Microsoft Office ribbon UI is mentioned on the product's own
    /// site, and no Linux build is advertised), so these are compile-verified/spec-driven, not
    /// live-verified, matching this project's established caveat for every other Windows-only path.
    /// <list type="bullet">
    /// <item><b>Confirmed from VideoPsalm's own documentation:</b> Ctrl+F (focus search), Alt+B
    /// (Bibles panel), Alt+S (Songbooks panel), Enter (display first result), Down/Up or Page
    /// Down/Page Up (next/previous slide), Plus/Minus (next/previous agenda item), Escape or End
    /// (exit presentation).</item>
    /// <item><b>Best-effort, not found in the docs actually read:</b> a dedicated "black screen
    /// while staying live" shortcut. VideoPsalm's site states it is compatible with PowerPoint's
    /// shortcut keys, and PowerPoint's own black-screen-during-a-slideshow key is "B" — so "B" is
    /// used here for <see cref="BlackScreenKey"/> on that documented compatibility claim, not a
    /// guess made up from nothing, but it has not been directly confirmed against VideoPsalm
    /// itself.</item>
    /// </list>
    /// </summary>
    public static VideoPsalmProfile Default { get; } = new()
    {
        WindowTitleContains = "VideoPsalm",
        ProcessName = "VideoPsalm",
        FocusSearchModifier = Key.LeftControl,
        FocusSearchKey = Key.F,
        BiblesPanelModifier = Key.LeftAlt,
        BiblesPanelKey = Key.B,
        SongbooksPanelModifier = Key.LeftAlt,
        SongbooksPanelKey = Key.S,
        ConfirmSelectionKey = Key.Enter,
        NextSlideKey = Key.Down,
        PreviousSlideKey = Key.Up,
        NextAgendaItemModifier = Key.LeftShift,
        NextAgendaItemKey = Key.Equals, // "+" is Shift+"=" on a standard layout.
        PreviousAgendaItemKey = Key.Minus,
        BlackScreenKey = Key.B,
        ExitPresentationKey = Key.Escape,
    };

    public static VideoPsalmProfile LoadFromJson(string json) =>
        JsonSerializer.Deserialize<VideoPsalmProfile>(json, JsonOptions)
        ?? throw new InvalidOperationException("VideoPsalm profile JSON deserialized to null.");

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}
