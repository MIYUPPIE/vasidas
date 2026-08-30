namespace AutomationStudio.Bible.Services;

/// <summary>
/// Canonical KJV book names plus their standard abbreviations/aliases, so "jn 3:16", "1st john
/// 3:16" and "John 3:16" all resolve to the same book (DAS.md section 17's normalization).
/// </summary>
internal static class BibleBookNames
{
    public static readonly IReadOnlyList<string> CanonicalOrder =
    [
        "Genesis", "Exodus", "Leviticus", "Numbers", "Deuteronomy", "Joshua", "Judges", "Ruth",
        "1 Samuel", "2 Samuel", "1 Kings", "2 Kings", "1 Chronicles", "2 Chronicles", "Ezra",
        "Nehemiah", "Esther", "Job", "Psalms", "Proverbs", "Ecclesiastes", "Song of Solomon",
        "Isaiah", "Jeremiah", "Lamentations", "Ezekiel", "Daniel", "Hosea", "Joel", "Amos",
        "Obadiah", "Jonah", "Micah", "Nahum", "Habakkuk", "Zephaniah", "Haggai", "Zechariah",
        "Malachi", "Matthew", "Mark", "Luke", "John", "Acts", "Romans", "1 Corinthians",
        "2 Corinthians", "Galatians", "Ephesians", "Philippians", "Colossians", "1 Thessalonians",
        "2 Thessalonians", "1 Timothy", "2 Timothy", "Titus", "Philemon", "Hebrews", "James",
        "1 Peter", "2 Peter", "1 John", "2 John", "3 John", "Jude", "Revelation"
    ];

    /// <summary>Alias (lowercase, no punctuation) → canonical name. Numbered-book prefixes ("1st", "first", "i") are handled separately in <see cref="Normalize"/>, not listed per-alias here.</summary>
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["gen"] = "Genesis", ["ge"] = "Genesis", ["gn"] = "Genesis",
        ["ex"] = "Exodus", ["exod"] = "Exodus", ["exo"] = "Exodus",
        ["lev"] = "Leviticus", ["le"] = "Leviticus", ["lv"] = "Leviticus",
        ["num"] = "Numbers", ["nu"] = "Numbers", ["nm"] = "Numbers", ["nb"] = "Numbers",
        ["deut"] = "Deuteronomy", ["de"] = "Deuteronomy", ["dt"] = "Deuteronomy",
        ["josh"] = "Joshua", ["jos"] = "Joshua",
        ["judg"] = "Judges", ["jdg"] = "Judges", ["jg"] = "Judges",
        ["ru"] = "Ruth", ["rth"] = "Ruth",
        ["sam"] = "Samuel",
        ["kgs"] = "Kings", ["kg"] = "Kings", ["ki"] = "Kings",
        ["chr"] = "Chronicles", ["ch"] = "Chronicles", ["chron"] = "Chronicles",
        ["ezr"] = "Ezra",
        ["neh"] = "Nehemiah", ["ne"] = "Nehemiah",
        ["est"] = "Esther", ["esth"] = "Esther",
        ["jb"] = "Job",
        ["ps"] = "Psalms", ["psa"] = "Psalms", ["psalm"] = "Psalms", ["pslm"] = "Psalms", ["psm"] = "Psalms",
        ["prov"] = "Proverbs", ["pro"] = "Proverbs", ["pr"] = "Proverbs", ["prv"] = "Proverbs",
        ["eccl"] = "Ecclesiastes", ["eccles"] = "Ecclesiastes", ["ecc"] = "Ecclesiastes", ["ec"] = "Ecclesiastes",
        ["song"] = "Song of Solomon", ["sos"] = "Song of Solomon", ["canticles"] = "Song of Solomon", ["song of songs"] = "Song of Solomon",
        ["isa"] = "Isaiah", ["is"] = "Isaiah",
        ["jer"] = "Jeremiah", ["je"] = "Jeremiah", ["jr"] = "Jeremiah",
        ["lam"] = "Lamentations", ["la"] = "Lamentations",
        ["ezek"] = "Ezekiel", ["eze"] = "Ezekiel", ["ezk"] = "Ezekiel",
        ["dan"] = "Daniel", ["da"] = "Daniel", ["dn"] = "Daniel",
        ["hos"] = "Hosea", ["ho"] = "Hosea",
        ["jl"] = "Joel",
        ["am"] = "Amos",
        ["obad"] = "Obadiah", ["ob"] = "Obadiah",
        ["jnh"] = "Jonah", ["jon"] = "Jonah",
        ["mic"] = "Micah", ["mc"] = "Micah",
        ["nah"] = "Nahum", ["na"] = "Nahum",
        ["hab"] = "Habakkuk", ["hb"] = "Habakkuk",
        ["zeph"] = "Zephaniah", ["zep"] = "Zephaniah", ["zp"] = "Zephaniah",
        ["hag"] = "Haggai", ["hg"] = "Haggai",
        ["zech"] = "Zechariah", ["zec"] = "Zechariah", ["zc"] = "Zechariah",
        ["mal"] = "Malachi", ["ml"] = "Malachi",
        ["matt"] = "Matthew", ["mt"] = "Matthew",
        ["mk"] = "Mark", ["mr"] = "Mark", ["mrk"] = "Mark",
        ["lk"] = "Luke", ["lu"] = "Luke",
        ["jn"] = "John", ["jhn"] = "John", ["joh"] = "John",
        ["ac"] = "Acts", ["act"] = "Acts",
        ["rom"] = "Romans", ["ro"] = "Romans", ["rm"] = "Romans",
        ["cor"] = "Corinthians", ["co"] = "Corinthians",
        ["gal"] = "Galatians", ["ga"] = "Galatians",
        ["eph"] = "Ephesians",
        ["phil"] = "Philippians", ["php"] = "Philippians", ["pp"] = "Philippians",
        ["col"] = "Colossians",
        ["thess"] = "Thessalonians", ["th"] = "Thessalonians", ["thes"] = "Thessalonians",
        ["tim"] = "Timothy", ["ti"] = "Timothy",
        ["tit"] = "Titus",
        ["philem"] = "Philemon", ["phm"] = "Philemon", ["pm"] = "Philemon",
        ["heb"] = "Hebrews",
        ["jas"] = "James", ["jm"] = "James",
        ["pet"] = "Peter", ["pt"] = "Peter",
        ["rev"] = "Revelation", ["re"] = "Revelation", ["revelations"] = "Revelation",
    };

    /// <summary>
    /// Resolves any reasonable spoken/typed spelling of a book name to its canonical KJV form, e.g.
    /// "jn" / "1st john" / "first john" / "i john" / "1 corinthians" all → the correct canonical
    /// name. Returns null if nothing matches.
    /// </summary>
    public static string? Resolve(string input)
    {
        var normalized = NormalizeNumberedPrefix(input.Trim().ToLowerInvariant());

        // Exact canonical match (case-insensitive).
        var exact = CanonicalOrder.FirstOrDefault(b => string.Equals(b, normalized, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        // "1 " / "2 " / "3 " prefix + an alias base (e.g. "1 sam" -> "1 Samuel").
        var (numberPrefix, rest) = SplitNumberPrefix(normalized);
        if (Aliases.TryGetValue(rest, out var aliasedBase))
        {
            var candidate = numberPrefix is null ? aliasedBase : $"{numberPrefix} {aliasedBase}";
            if (CanonicalOrder.Contains(candidate))
            {
                return candidate;
            }
        }

        // Non-numbered alias (e.g. "jn" -> "John").
        if (numberPrefix is null && Aliases.TryGetValue(rest, out var directAlias))
        {
            return directAlias;
        }

        return null;
    }

    private static string NormalizeNumberedPrefix(string input)
    {
        foreach (var (spoken, digit) in new[] { ("first", "1"), ("second", "2"), ("third", "3"), ("i ", "1 "), ("ii ", "2 "), ("iii ", "3 "), ("1st", "1"), ("2nd", "2"), ("3rd", "3") })
        {
            if (input.StartsWith(spoken, StringComparison.Ordinal))
            {
                return digit.TrimEnd() + " " + input[spoken.Length..].TrimStart();
            }
        }

        return input;
    }

    private static (string? NumberPrefix, string Remainder) SplitNumberPrefix(string input)
    {
        foreach (var prefix in new[] { "1", "2", "3" })
        {
            if (input.StartsWith(prefix + " ", StringComparison.Ordinal))
            {
                return (prefix, input[(prefix.Length + 1)..].Trim());
            }
        }

        return (null, input);
    }
}
