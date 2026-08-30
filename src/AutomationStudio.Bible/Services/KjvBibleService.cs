using System.Reflection;
using System.Text.Json;
using AutomationStudio.Abstractions;

namespace AutomationStudio.Bible.Services;

/// <summary>
/// Local, offline Bible text lookup (DAS.md section 16) over the embedded public-domain KJV dataset
/// (31,102 verses, https://github.com/aruljohn/Bible-kjv). Loaded once and kept in memory — at
/// ~4.2MB of text this is cheap, and repeated startup-time-only parsing would be pointless work.
/// </summary>
public sealed class KjvBibleService : IBibleService
{
    private readonly Lazy<Dictionary<string, string[][]>> _books = new(LoadBooks);

    public Task<Verse?> GetVerseAsync(string book, int chapter, int verse, CancellationToken cancellationToken = default)
    {
        var resolved = ResolveBookName(book);
        if (resolved is null || !_books.Value.TryGetValue(resolved, out var chapters))
        {
            return Task.FromResult<Verse?>(null);
        }

        if (chapter < 1 || chapter > chapters.Length)
        {
            return Task.FromResult<Verse?>(null);
        }

        var verses = chapters[chapter - 1];
        if (verse < 1 || verse > verses.Length)
        {
            return Task.FromResult<Verse?>(null);
        }

        return Task.FromResult<Verse?>(new Verse(resolved, chapter, verse, verses[verse - 1]));
    }

    public async Task<IReadOnlyList<Verse>> GetVersesAsync(string book, int chapter, int startVerse, int endVerse, CancellationToken cancellationToken = default)
    {
        if (endVerse < startVerse)
        {
            (startVerse, endVerse) = (endVerse, startVerse);
        }

        var results = new List<Verse>(endVerse - startVerse + 1);
        for (var v = startVerse; v <= endVerse; v++)
        {
            var verse = await GetVerseAsync(book, chapter, v, cancellationToken).ConfigureAwait(false);
            if (verse is not null)
            {
                results.Add(verse);
            }
        }

        return results;
    }

    public Task<IReadOnlyList<Verse>> GetChapterAsync(string book, int chapter, CancellationToken cancellationToken = default)
    {
        var resolved = ResolveBookName(book);
        if (resolved is null || !_books.Value.TryGetValue(resolved, out var chapters) || chapter < 1 || chapter > chapters.Length)
        {
            return Task.FromResult<IReadOnlyList<Verse>>([]);
        }

        var verses = chapters[chapter - 1];
        var results = new List<Verse>(verses.Length);
        for (var i = 0; i < verses.Length; i++)
        {
            results.Add(new Verse(resolved, chapter, i + 1, verses[i]));
        }

        return Task.FromResult<IReadOnlyList<Verse>>(results);
    }

    public string? ResolveBookName(string input) => BibleBookNames.Resolve(input);

    private static Dictionary<string, string[][]> LoadBooks()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("AutomationStudio.Bible.Data.kjv.json")
            ?? throw new InvalidOperationException("Embedded KJV dataset resource not found.");

        using var document = JsonDocument.Parse(stream);
        var books = new Dictionary<string, string[][]>(66, StringComparer.Ordinal);
        foreach (var bookElement in document.RootElement.EnumerateArray())
        {
            var name = bookElement.GetProperty("name").GetString()!;
            var chaptersElement = bookElement.GetProperty("chapters");
            var chapters = new string[chaptersElement.GetArrayLength()][];
            var chapterIndex = 0;
            foreach (var chapterElement in chaptersElement.EnumerateArray())
            {
                var verses = new string[chapterElement.GetArrayLength()];
                var verseIndex = 0;
                foreach (var verseElement in chapterElement.EnumerateArray())
                {
                    verses[verseIndex++] = verseElement.GetString()!;
                }

                chapters[chapterIndex++] = verses;
            }

            books[name] = chapters;
        }

        return books;
    }
}
