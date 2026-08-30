namespace AutomationStudio.Bible.Parser;

/// <summary>
/// Converts spoken number words to integers — needed for "John three sixteen" (DAS.md section 17).
/// Scoped to what Bible references actually need: chapter/verse numbers never exceed 176 (Psalm
/// 119's verse count is the Bible's largest chapter), so this only needs ones/teens/tens/hundred,
/// not a general-purpose numeral parser.
/// </summary>
internal static class EnglishNumberWords
{
    private static readonly Dictionary<string, int> Ones = new()
    {
        ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
        ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10,
        ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13, ["fourteen"] = 14, ["fifteen"] = 15,
        ["sixteen"] = 16, ["seventeen"] = 17, ["eighteen"] = 18, ["nineteen"] = 19,
    };

    private static readonly Dictionary<string, int> Tens = new()
    {
        ["twenty"] = 20, ["thirty"] = 30, ["forty"] = 40, ["fifty"] = 50,
        ["sixty"] = 60, ["seventy"] = 70, ["eighty"] = 80, ["ninety"] = 90,
    };

    /// <summary>True if this single token is a recognized number word (including "hundred").</summary>
    public static bool IsNumberWord(string token) =>
        Ones.ContainsKey(token) || Tens.ContainsKey(token) || token == "hundred";

    /// <summary>
    /// Greedily consumes number words starting at <paramref name="startIndex"/> (e.g.
    /// ["one", "hundred", "nineteen", "verse"] → 119, consuming 3 tokens) and returns the parsed
    /// value plus how many tokens it consumed, or null if <paramref name="startIndex"/> isn't a
    /// number word at all.
    /// </summary>
    public static (int Value, int TokensConsumed)? TryParse(IReadOnlyList<string> tokens, int startIndex)
    {
        if (startIndex >= tokens.Count || !IsNumberWord(tokens[startIndex]))
        {
            return null;
        }

        var i = startIndex;
        var total = 0;

        if (Ones.TryGetValue(tokens[i], out var onesValue) && onesValue >= 1 && onesValue <= 9 &&
            i + 1 < tokens.Count && tokens[i + 1] == "hundred")
        {
            total += onesValue * 100;
            i += 2;
        }

        if (i < tokens.Count && Tens.TryGetValue(tokens[i], out var tensValue))
        {
            total += tensValue;
            i++;
            if (i < tokens.Count && Ones.TryGetValue(tokens[i], out var trailingOnes) && trailingOnes is >= 1 and <= 9)
            {
                total += trailingOnes;
                i++;
            }
        }
        else if (i < tokens.Count && Ones.TryGetValue(tokens[i], out var onlyOnes))
        {
            total += onlyOnes;
            i++;
        }

        return i == startIndex ? null : (total, i - startIndex);
    }
}
