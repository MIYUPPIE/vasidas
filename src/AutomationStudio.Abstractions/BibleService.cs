namespace AutomationStudio.Abstractions;

/// <summary>One verse of scripture (DAS.md section 16).</summary>
public sealed record Verse(string Book, int Chapter, int Number, string Text);

/// <summary>
/// A parsed, structured reference (DAS.md section 17: "John 3:16" / "John chapter 3 verse 16" /
/// "John three sixteen" all normalize to the same <see cref="BibleReference"/> before automation
/// begins). <see cref="EndVerse"/> is null for a single verse or a whole-chapter reference (in which
/// case <see cref="StartVerse"/> is also null).
/// </summary>
public sealed record BibleReference(string Book, int Chapter, int? StartVerse, int? EndVerse)
{
    public bool IsWholeChapter => StartVerse is null;
    public bool IsRange => StartVerse is not null && EndVerse is not null && EndVerse != StartVerse;

    public override string ToString()
    {
        if (StartVerse is null)
        {
            return $"{Book} {Chapter}";
        }

        return EndVerse is null || EndVerse == StartVerse
            ? $"{Book} {Chapter}:{StartVerse}"
            : $"{Book} {Chapter}:{StartVerse}-{EndVerse}";
    }
}

/// <summary>
/// Local Bible text lookup (DAS.md section 16) — deliberately separate from the automation engine
/// ("do not make the automation engine responsible for Bible data"), so a voice command can be
/// validated against real scripture *before* any automation runs against VideoPsalm.
/// </summary>
public interface IBibleService
{
    Task<Verse?> GetVerseAsync(string book, int chapter, int verse, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Verse>> GetVersesAsync(string book, int chapter, int startVerse, int endVerse, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Verse>> GetChapterAsync(string book, int chapter, CancellationToken cancellationToken = default);

    /// <summary>Resolves a user-typed/spoken book name (any case, abbreviated, or numbered-book variant like "1st john") to its canonical KJV name, or null if it matches nothing.</summary>
    string? ResolveBookName(string input);

    /// <summary>The 66 canonical book names in Bible order (Genesis first, Revelation last) — needed to cross a book boundary when navigating verse-by-verse (DAS.md section 8's "Next verse").</summary>
    IReadOnlyList<string> BookOrder { get; }

    /// <summary>The verse after this one, crossing into the next chapter or book as needed. Null at the very end of the Bible (Revelation 22:21).</summary>
    Task<Verse?> GetNextVerseAsync(Verse current, CancellationToken cancellationToken = default);

    /// <summary>The verse before this one, crossing into the previous chapter or book as needed. Null at the very start of the Bible (Genesis 1:1).</summary>
    Task<Verse?> GetPreviousVerseAsync(Verse current, CancellationToken cancellationToken = default);
}
