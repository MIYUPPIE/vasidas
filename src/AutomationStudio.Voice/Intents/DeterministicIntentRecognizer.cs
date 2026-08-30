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
public sealed partial class DeterministicIntentRecognizer(IBibleService bibleService) : IIntentRecognizer
{
    private static readonly (Regex Pattern, Func<Match, Intent> Build)[] Rules =
    [
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

        // Tried before normalization/the Rules loop: CommandNormalizer strips punctuation, including
        // the colon "John 3:16" depends on — Bible references need the raw text.
        var bibleIntent = await TryRecognizeBibleVerseAsync(trimmed, cancellationToken).ConfigureAwait(false);
        if (bibleIntent is not null)
        {
            return bibleIntent;
        }

        var normalized = CommandNormalizer.Normalize(text);
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
}
