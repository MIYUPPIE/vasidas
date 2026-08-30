using System.Text;

namespace AutomationStudio.Voice.Commands;

/// <summary>
/// Normalizes raw speech-to-text output before intent matching (DAS.md section 5's
/// CommandNormalizer): lowercase, trimmed, punctuation stripped, whitespace collapsed. Pure
/// deterministic text processing — no model, no ambiguity, same input always produces the same
/// output.
/// </summary>
public static class CommandNormalizer
{
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var lastWasSpace = false;

        foreach (var ch in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasSpace = false;
            }
            else if (char.IsWhiteSpace(ch) && !lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
            // punctuation (periods, commas, etc.) is dropped entirely, not turned into a space.
        }

        return builder.ToString().Trim();
    }
}
