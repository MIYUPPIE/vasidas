using System.Text.RegularExpressions;
using AutomationStudio.Abstractions;

namespace AutomationStudio.Bible.Parser;

/// <summary>
/// Normalizes every spoken/typed shape of a Bible reference into a structured
/// <see cref="BibleReference"/> before automation begins (DAS.md section 17): "John 3:16", "John
/// chapter 3 verse 16", "John three sixteen", "John chapter three, verse sixteen", and
/// "Psalm 91 verses 1 to 5" must all resolve to the same result.
/// </summary>
public static partial class BibleReferenceParser
{
    public static BibleReference? Parse(string input, IBibleService bibleService)
    {
        var text = input.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        return TryParseNumericShorthand(text, bibleService)
            ?? TryParseWordBased(text, bibleService);
    }

    /// <summary>"John 3:16", "John 3:16-18", "Psalm 91:1 to 5", "Psalm 23" (chapter only, no colon).</summary>
    private static BibleReference? TryParseNumericShorthand(string text, IBibleService bibleService)
    {
        var match = NumericWithVersePattern().Match(text);
        if (match.Success)
        {
            var bookName = bibleService.ResolveBookName(match.Groups["book"].Value.Trim());
            if (bookName is null)
            {
                return null;
            }

            var chapter = int.Parse(match.Groups["chapter"].Value);
            var start = int.Parse(match.Groups["start"].Value);
            var end = match.Groups["end"].Success ? int.Parse(match.Groups["end"].Value) : start;
            return new BibleReference(bookName, chapter, start, end);
        }

        var chapterOnlyMatch = NumericChapterOnlyPattern().Match(text);
        if (chapterOnlyMatch.Success)
        {
            var bookName = bibleService.ResolveBookName(chapterOnlyMatch.Groups["book"].Value.Trim());
            if (bookName is null)
            {
                return null;
            }

            var chapter = int.Parse(chapterOnlyMatch.Groups["chapter"].Value);
            return new BibleReference(bookName, chapter, null, null);
        }

        return null;
    }

    /// <summary>"John chapter 3 verse 16", "John three sixteen", "Psalm ninety-one verses 1 to 5", "Psalm chapter 23".</summary>
    private static BibleReference? TryParseWordBased(string text, IBibleService bibleService)
    {
        var tokens = TokenizePattern().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => t.Length > 0)
            .ToList();
        if (tokens.Count < 2)
        {
            return null;
        }

        var bookTokenCount = FindBookTokenCount(tokens, bibleService, out var bookName);
        if (bookTokenCount == 0 || bookName is null)
        {
            return null;
        }

        var rest = tokens.Skip(bookTokenCount).ToList();
        var index = 0;

        if (index < rest.Count && rest[index] == "chapter")
        {
            index++;
        }

        var chapterNumber = ReadNumber(rest, ref index);
        if (chapterNumber is null)
        {
            return null;
        }

        if (index < rest.Count && (rest[index] == "verse" || rest[index] == "verses"))
        {
            index++;
        }

        var startVerse = ReadNumber(rest, ref index);
        if (startVerse is null)
        {
            // "John chapter three" / "Psalm ninety one" — a bare chapter reference is valid.
            return new BibleReference(bookName, chapterNumber.Value, null, null);
        }

        if (index < rest.Count && (rest[index] == "to" || rest[index] == "through" || rest[index] == "-"))
        {
            index++;
        }

        var endVerse = ReadNumber(rest, ref index) ?? startVerse;
        return new BibleReference(bookName, chapterNumber.Value, startVerse, endVerse);
    }

    /// <summary>Reads a number at the current token position — either a bare digit token or one or more spoken number words — advancing <paramref name="index"/> past whatever it consumed.</summary>
    private static int? ReadNumber(List<string> tokens, ref int index)
    {
        if (index >= tokens.Count)
        {
            return null;
        }

        if (int.TryParse(tokens[index], out var digitValue))
        {
            index++;
            return digitValue;
        }

        var spoken = EnglishNumberWords.TryParse(tokens, index);
        if (spoken is null)
        {
            return null;
        }

        index += spoken.Value.TokensConsumed;
        return spoken.Value.Value;
    }

    /// <summary>
    /// Tries the longest plausible book-name prefix first (longest match wins — "song of solomon"
    /// must not be mistaken for just "song"), down to a single token.
    /// </summary>
    private static int FindBookTokenCount(List<string> tokens, IBibleService bibleService, out string? bookName)
    {
        for (var length = Math.Min(4, tokens.Count - 1); length >= 1; length--)
        {
            var candidate = string.Join(' ', tokens.Take(length));
            var resolved = bibleService.ResolveBookName(candidate);
            if (resolved is not null)
            {
                bookName = resolved;
                return length;
            }
        }

        bookName = null;
        return 0;
    }

    [GeneratedRegex(@"^(?<book>.+?)\s+(?<chapter>\d+)\s*:\s*(?<start>\d+)(\s*(-|to)\s*(?<end>\d+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex NumericWithVersePattern();

    [GeneratedRegex(@"^(?<book>.+?)\s+(?<chapter>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex NumericChapterOnlyPattern();

    // Ordinal suffixes ("1st", "2nd", "3rd") must stay attached to their digit — otherwise "1st
    // corinthians" tokenizes as ["1", "st", "corinthians"] and book-name resolution (which expects
    // "1st" as one contiguous alias) never matches.
    [GeneratedRegex(@"\d+(?:st|nd|rd|th)?|[a-z]+")]
    private static partial Regex TokenizePattern();
}
