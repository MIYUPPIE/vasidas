using System.Text.RegularExpressions;
using AutomationStudio.Abstractions;
using AutomationStudio.Bible.Parser;
using AutomationStudio.Voice.Commands;

namespace AutomationStudio.Voice.Intents;

/// <summary>
/// Rule-based intent matching for Phase 3's command set (DAS.md section 37) plus DAS.md section 15's
/// VideoPsalm/Bible commands: "Open OBS", "Start recording", "Show John 3:16", "Next verse", "Show
/// Amazing Grace", "Clear the screen" — plus reasonable synonyms (section 7 requires synonym
/// support). Deterministic pattern matching, not an LLM: DAS.md's own roadmap (Phase 10) is explicit
/// that local AI only enters the picture once this layer is stable. Recognizing an intent is as far
/// as this goes — routing DISPLAY_BIBLE_VERSE etc. to an actual <c>IVideoPsalmPlugin</c> call is
/// Phase 6 (Task Planner), not built yet.
/// </summary>
public sealed partial class DeterministicIntentRecognizer(IBibleService bibleService, ICommandRegistry? commandRegistry = null) : IIntentRecognizer
{
    private static readonly (Regex Pattern, Func<Match, Intent> Build)[] Rules =
    [
        (TeachCommandWithNamePattern(), m => new Intent("TEACH_COMMAND", new Dictionary<string, string> { ["name"] = m.Groups["name"].Value })),
        (TeachCommandPattern(), _ => Intent.Simple("TEACH_COMMAND")),
        (StopTeachingPattern(), _ => Intent.Simple("STOP_TEACHING")),
        (StartRecordingPattern(), _ => Intent.Simple("START_RECORDING")),
        (StopRecordingPattern(), _ => Intent.Simple("STOP_RECORDING")),
        (NextVersePattern(), _ => Intent.Simple("NEXT_VERSE")),
        (PreviousVersePattern(), _ => Intent.Simple("PREVIOUS_VERSE")),
        (NextSlidePattern(), _ => Intent.Simple("NEXT_SLIDE")),
        (PreviousSlidePattern(), _ => Intent.Simple("PREVIOUS_SLIDE")),
        (PausePattern(), _ => Intent.Simple("PAUSE_MEDIA")),
        (ResumePattern(), _ => Intent.Simple("PLAY_MEDIA")),
        (NextSongPattern(), _ => Intent.Simple("NEXT_SONG")),
        (PreviousSongPattern(), _ => Intent.Simple("PREVIOUS_SONG")),
        (ClearScreenPattern(), _ => Intent.Simple("CLEAR_SCREEN")),
        (HideOutputPattern(), _ => Intent.Simple("HIDE_OUTPUT")),

        // OBS (DAS.md section 14) — "start X"/"close X"-shaped patterns must precede the generic
        // OpenApplication/CloseApplication catch-alls below, or those would win instead.
        (StartStreamingPattern(), _ => Intent.Simple("START_STREAM")),
        (StopStreamingPattern(), _ => Intent.Simple("STOP_STREAM")),
        (SwitchScenePattern(), m => new Intent("SWITCH_SCENE", new Dictionary<string, string> { ["scene"] = m.Groups["scene"].Value })),

        // PowerPoint (DAS.md section 14) — NEXT_SLIDE/PREVIOUS_SLIDE already exist above.
        (GoToSlidePattern(), m => new Intent("GO_TO_SLIDE", new Dictionary<string, string> { ["slideNumber"] = m.Groups["slideNumber"].Value })),
        (StartPresentationPattern(), _ => Intent.Simple("START_PRESENTATION")),
        (EndPresentationPattern(), _ => Intent.Simple("END_PRESENTATION")),

        // VLC (DAS.md section 9's PLAY_MEDIA/PAUSE_MEDIA already exist above via Pause/ResumePattern).
        (StopMediaPattern(), _ => Intent.Simple("STOP_MEDIA")),
        (NextTrackPattern(), _ => Intent.Simple("NEXT_TRACK")),
        (PreviousTrackPattern(), _ => Intent.Simple("PREVIOUS_TRACK")),
        (ToggleMutePattern(), _ => Intent.Simple("TOGGLE_MUTE")),
        (ToggleFullscreenPattern(), _ => Intent.Simple("TOGGLE_FULLSCREEN")),

        // Chrome — CloseTab must precede the generic CloseApplication catch-all below. OpenUrl is
        // handled separately, on raw (pre-normalization) text — see TryRecognizeUrl.
        (NewTabPattern(), _ => Intent.Simple("NEW_TAB")),
        (CloseTabPattern(), _ => Intent.Simple("CLOSE_TAB")),
        (ReloadPagePattern(), _ => Intent.Simple("RELOAD_PAGE")),
        (GoBackPagePattern(), _ => Intent.Simple("GO_BACK")),
        (GoForwardPagePattern(), _ => Intent.Simple("GO_FORWARD")),
        (FindOnPagePattern(), m => new Intent("FIND_ON_PAGE", new Dictionary<string, string> { ["text"] = m.Groups["text"].Value })),

        // File Manager.
        (NewFolderPattern(), _ => Intent.Simple("NEW_FOLDER")),
        (CopyFilePattern(), _ => Intent.Simple("COPY_FILE")),
        (CutFilePattern(), _ => Intent.Simple("CUT_FILE")),
        (PasteFilePattern(), _ => Intent.Simple("PASTE_FILE")),
        (RenameFilePattern(), m => new Intent("RENAME_FILE", new Dictionary<string, string> { ["name"] = m.Groups["name"].Value })),
        (DeleteFilePattern(), _ => Intent.Simple("DELETE_FILE")),
        (SearchFilesPattern(), m => new Intent("SEARCH_FILES", new Dictionary<string, string> { ["query"] = m.Groups["query"].Value })),

        // Generic across Word/Excel (DAS.md section 9's SAVE_DOCUMENT).
        (SaveDocumentPattern(), m => new Intent("SAVE_DOCUMENT", new Dictionary<string, string> { ["application"] = m.Groups["application"].Value })),

        (OpenApplicationPattern(), m => new Intent("OPEN_APPLICATION", new Dictionary<string, string> { ["application"] = m.Groups["app"].Value })),
        (CloseApplicationPattern(), m => new Intent("CLOSE_APPLICATION", new Dictionary<string, string> { ["application"] = m.Groups["app"].Value })),
        (DisplaySongPattern(), m => new Intent("DISPLAY_SONG", new Dictionary<string, string> { ["title"] = m.Groups["title"].Value })),
    ];

    public async Task<Intent?> RecognizeAsync(string text, CancellationToken cancellationToken = default)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var normalized = CommandNormalizer.Normalize(text);

        // A taught command takes priority over everything else: if the user registered "clear the
        // screen" to mean something custom, saying it should run what they taught, not silently fall
        // through to the built-in CLEAR_SCREEN pattern below.
        if (commandRegistry is not null && normalized.Length > 0)
        {
            var workflowId = await commandRegistry.ResolveWorkflowIdAsync(normalized, cancellationToken).ConfigureAwait(false);
            if (workflowId is not null)
            {
                return new Intent("RUN_WORKFLOW", new Dictionary<string, string> { ["workflowId"] = workflowId });
            }
        }

        // Tried before the Rules loop: CommandNormalizer strips punctuation, including the colon
        // "John 3:16" depends on — Bible references need the raw text.
        var bibleIntent = await TryRecognizeBibleVerseAsync(trimmed, cancellationToken).ConfigureAwait(false);
        if (bibleIntent is not null)
        {
            return bibleIntent;
        }

        // Also tried on raw text for the same reason: normalization strips the dot "example.com"
        // depends on to read as a domain rather than an application name.
        var urlIntent = TryRecognizeUrl(trimmed);
        if (urlIntent is not null)
        {
            return urlIntent;
        }

        if (normalized.Length == 0)
        {
            return null;
        }

        foreach (var (pattern, build) in Rules)
        {
            var match = pattern.Match(normalized);
            if (match.Success)
            {
                return build(match);
            }
        }

        return null;
    }

    /// <summary>Parses, then validates against the real Bible data before committing to this interpretation (DAS.md section 16: validate before automation, applied as early as possible) — a grammatically valid but nonexistent reference (e.g. "John chapter 99") falls through to whatever else the text might mean instead of confidently emitting a bogus DISPLAY_BIBLE_VERSE.</summary>
    private async Task<Intent?> TryRecognizeBibleVerseAsync(string text, CancellationToken cancellationToken)
    {
        var withoutVerb = DisplayVerbPrefixPattern().Replace(text, "").Trim();
        var candidate = withoutVerb.Length > 0 ? withoutVerb : text;

        var reference = BibleReferenceParser.Parse(candidate, bibleService);
        if (reference is null)
        {
            return null;
        }

        var exists = reference.StartVerse is { } verse
            ? await bibleService.GetVerseAsync(reference.Book, reference.Chapter, verse, cancellationToken).ConfigureAwait(false) is not null
            : (await bibleService.GetChapterAsync(reference.Book, reference.Chapter, cancellationToken).ConfigureAwait(false)).Count > 0;
        if (!exists)
        {
            return null;
        }

        var parameters = new Dictionary<string, string>
        {
            ["book"] = reference.Book,
            ["chapter"] = reference.Chapter.ToString()
        };
        if (reference.StartVerse is not null)
        {
            parameters["startVerse"] = reference.StartVerse.Value.ToString();
        }

        if (reference.EndVerse is not null)
        {
            parameters["endVerse"] = reference.EndVerse.Value.ToString();
        }

        return new Intent("DISPLAY_BIBLE_VERSE", parameters);
    }

    /// <summary>Disambiguates from OPEN_APPLICATION by requiring what follows the verb to look like a domain (has a dot, no spaces) rather than an app name — checked on raw text since <see cref="AutomationStudio.Voice.Commands.CommandNormalizer"/> strips the dot this depends on.</summary>
    private static Intent? TryRecognizeUrl(string text)
    {
        var match = OpenUrlRawPattern().Match(text);
        return match.Success
            ? new Intent("OPEN_URL", new Dictionary<string, string> { ["url"] = match.Groups["url"].Value })
            : null;
    }

    [GeneratedRegex(@"^teach\s+(a\s+)?new\s+command\s+(?<name>.+)$")]
    private static partial Regex TeachCommandWithNamePattern();

    [GeneratedRegex(@"^teach\s+(a\s+)?new\s+command$")]
    private static partial Regex TeachCommandPattern();

    [GeneratedRegex(@"^(stop|done|finished)\s+teaching$")]
    private static partial Regex StopTeachingPattern();

    [GeneratedRegex(@"^(start|begin)\s+recording$")]
    private static partial Regex StartRecordingPattern();

    [GeneratedRegex(@"^stop\s+recording$")]
    private static partial Regex StopRecordingPattern();

    [GeneratedRegex(@"^next\s+verse$")]
    private static partial Regex NextVersePattern();

    [GeneratedRegex(@"^(previous|back)\s+verse$")]
    private static partial Regex PreviousVersePattern();

    [GeneratedRegex(@"^next(\s+slide)?$")]
    private static partial Regex NextSlidePattern();

    [GeneratedRegex(@"^(previous|back|go\s+back)(\s+slide)?$")]
    private static partial Regex PreviousSlidePattern();

    [GeneratedRegex(@"^pause( it)?$")]
    private static partial Regex PausePattern();

    [GeneratedRegex(@"^(resume|play|continue|unpause)( it)?$")]
    private static partial Regex ResumePattern();

    [GeneratedRegex(@"^next\s+song$")]
    private static partial Regex NextSongPattern();

    [GeneratedRegex(@"^(previous|back)\s+song$")]
    private static partial Regex PreviousSongPattern();

    [GeneratedRegex(@"^clear(\s+the)?\s+screen$")]
    private static partial Regex ClearScreenPattern();

    [GeneratedRegex(@"^hide(\s+the)?\s+(presentation|output)$")]
    private static partial Regex HideOutputPattern();

    [GeneratedRegex(@"^(open|launch|start)\s+(?<app>.+)$")]
    private static partial Regex OpenApplicationPattern();

    [GeneratedRegex(@"^close\s+(?<app>.+)$")]
    private static partial Regex CloseApplicationPattern();

    [GeneratedRegex(@"^(show|display)\s+(?<title>.+)$")]
    private static partial Regex DisplaySongPattern();

    [GeneratedRegex(@"^(show|display|put|bring\s+up)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex DisplayVerbPrefixPattern();

    // --- OBS (DAS.md section 14) ---

    [GeneratedRegex(@"^(start|begin)\s+stream(ing)?$")]
    private static partial Regex StartStreamingPattern();

    [GeneratedRegex(@"^stop\s+stream(ing)?$")]
    private static partial Regex StopStreamingPattern();

    [GeneratedRegex(@"^switch\s+(to\s+)?scene\s+(?<scene>.+)$")]
    private static partial Regex SwitchScenePattern();

    // --- PowerPoint (DAS.md section 14) ---

    [GeneratedRegex(@"^go\s+to\s+slide\s+(?<slideNumber>\d+)$")]
    private static partial Regex GoToSlidePattern();

    [GeneratedRegex(@"^start\s+(the\s+)?presentation$")]
    private static partial Regex StartPresentationPattern();

    [GeneratedRegex(@"^end\s+(the\s+)?presentation$")]
    private static partial Regex EndPresentationPattern();

    // --- VLC (DAS.md section 9) ---

    [GeneratedRegex(@"^stop(\s+media)?$")]
    private static partial Regex StopMediaPattern();

    [GeneratedRegex(@"^next\s+track$")]
    private static partial Regex NextTrackPattern();

    [GeneratedRegex(@"^(previous|back)\s+track$")]
    private static partial Regex PreviousTrackPattern();

    [GeneratedRegex(@"^(un)?mute( it)?$")]
    private static partial Regex ToggleMutePattern();

    [GeneratedRegex(@"^(toggle\s+)?full\s*screen$")]
    private static partial Regex ToggleFullscreenPattern();

    // --- Chrome ---

    [GeneratedRegex(@"^(open|go\s+to|navigate\s+to)\s+(?<url>\S+\.\S+)$", RegexOptions.IgnoreCase)]
    private static partial Regex OpenUrlRawPattern();

    [GeneratedRegex(@"^new\s+tab$")]
    private static partial Regex NewTabPattern();

    [GeneratedRegex(@"^close\s+tab$")]
    private static partial Regex CloseTabPattern();

    [GeneratedRegex(@"^reload(\s+(the\s+)?page)?$")]
    private static partial Regex ReloadPagePattern();

    /// <summary>Deliberately does not include bare "go back" — PreviousSlidePattern (checked earlier in Rules) already claims that phrase for PREVIOUS_SLIDE, so it would never reach here.</summary>
    [GeneratedRegex(@"^(previous\s+page|back\s+page)$")]
    private static partial Regex GoBackPagePattern();

    [GeneratedRegex(@"^(go\s+forward|next\s+page|forward\s+page)$")]
    private static partial Regex GoForwardPagePattern();

    [GeneratedRegex(@"^find\s+(?<text>.+)$")]
    private static partial Regex FindOnPagePattern();

    // --- File Manager ---

    [GeneratedRegex(@"^(new|create\s+(a\s+)?new)\s+folder$")]
    private static partial Regex NewFolderPattern();

    [GeneratedRegex(@"^copy(\s+(this|it|file))?$")]
    private static partial Regex CopyFilePattern();

    [GeneratedRegex(@"^cut(\s+(this|it|file))?$")]
    private static partial Regex CutFilePattern();

    [GeneratedRegex(@"^paste(\s+(this|it|file))?$")]
    private static partial Regex PasteFilePattern();

    [GeneratedRegex(@"^rename(\s+(this|it))?\s+to\s+(?<name>.+)$")]
    private static partial Regex RenameFilePattern();

    [GeneratedRegex(@"^delete(\s+(this|it|file))?$")]
    private static partial Regex DeleteFilePattern();

    [GeneratedRegex(@"^search(\s+files)?\s+for\s+(?<query>.+)$")]
    private static partial Regex SearchFilesPattern();

    // --- Generic (DAS.md section 9's SAVE_DOCUMENT) ---

    [GeneratedRegex(@"^save\s+(the\s+)?(?<application>word|excel)(\s+(document|file|workbook))?$")]
    private static partial Regex SaveDocumentPattern();
}
