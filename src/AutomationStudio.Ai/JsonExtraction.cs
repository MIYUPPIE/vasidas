namespace AutomationStudio.Ai;

/// <summary>Pulls the JSON object out of a local model's raw completion text — a small model doesn't always perfectly follow "respond with ONLY the JSON" (stray commentary, markdown code fences), so this takes everything between the first <c>{</c> and the last <c>}</c> rather than assuming the whole response is clean JSON.</summary>
internal static class JsonExtraction
{
    public static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start < 0 || end < start ? null : text[start..(end + 1)];
    }
}
