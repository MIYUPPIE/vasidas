using AutomationStudio.Abstractions;

namespace AutomationStudio.Voice.Context;

/// <summary>
/// Resolves a raw recognized <see cref="Intent"/> against current <see cref="ExecutionContextSnapshot"/>
/// (DAS.md section 8/31 — sits between the Intent Engine and the Task Planner). "Next verse" alone
/// doesn't say which verse; combined with <see cref="ExecutionContextSnapshot.CurrentVerse"/> it
/// resolves to a fully-specified DISPLAY_BIBLE_VERSE, the same shape <see cref="IIntentRecognizer"/>
/// produces for an explicit reference like "John 3:16" — callers downstream never need to know
/// whether a reference was stated outright or inferred from context.
/// </summary>
public sealed class IntentContextResolver(IContextManager contextManager, IBibleService bibleService)
{
    /// <summary>Returns null when the intent needs context that isn't available (e.g. "next verse" with nothing currently displayed) — the same "couldn't act on this" signal as a failed recognition.</summary>
    public async Task<Intent?> ResolveAsync(Intent intent, CancellationToken cancellationToken = default)
    {
        var resolved = intent.Name switch
        {
            "NEXT_VERSE" => await ResolveRelativeVerseAsync(forward: true, cancellationToken).ConfigureAwait(false),
            "PREVIOUS_VERSE" => await ResolveRelativeVerseAsync(forward: false, cancellationToken).ConfigureAwait(false),
            _ => intent
        };

        if (resolved is null)
        {
            return null;
        }

        contextManager.RecordCommand(resolved);
        if (resolved.Name == "DISPLAY_BIBLE_VERSE")
        {
            contextManager.SetCurrentVerse(ParseReference(resolved));
        }

        return resolved;
    }

    private async Task<Intent?> ResolveRelativeVerseAsync(bool forward, CancellationToken cancellationToken)
    {
        var currentReference = contextManager.Current.CurrentVerse;
        if (currentReference is null || currentReference.StartVerse is not { } verseNumber)
        {
            return null; // nothing currently displayed (or only a whole chapter) to move relative to.
        }

        var currentVerse = await bibleService.GetVerseAsync(currentReference.Book, currentReference.Chapter, verseNumber, cancellationToken).ConfigureAwait(false);
        if (currentVerse is null)
        {
            return null;
        }

        var target = forward
            ? await bibleService.GetNextVerseAsync(currentVerse, cancellationToken).ConfigureAwait(false)
            : await bibleService.GetPreviousVerseAsync(currentVerse, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return null; // at the very start/end of the Bible.
        }

        return new Intent("DISPLAY_BIBLE_VERSE", new Dictionary<string, string>
        {
            ["book"] = target.Book,
            ["chapter"] = target.Chapter.ToString(),
            ["startVerse"] = target.Number.ToString(),
            ["endVerse"] = target.Number.ToString()
        });
    }

    private static BibleReference? ParseReference(Intent intent)
    {
        var book = intent.Parameter("book");
        var chapterText = intent.Parameter("chapter");
        if (book is null || chapterText is null || !int.TryParse(chapterText, out var chapter))
        {
            return null;
        }

        int? startVerse = int.TryParse(intent.Parameter("startVerse"), out var s) ? s : null;
        int? endVerse = int.TryParse(intent.Parameter("endVerse"), out var e) ? e : null;
        return new BibleReference(book, chapter, startVerse, endVerse);
    }
}
