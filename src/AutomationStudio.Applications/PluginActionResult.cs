namespace AutomationStudio.Applications;

/// <summary>
/// Result of any application plugin capability (DAS.md section 14) — success/failure with an
/// explanation, not an exception: "the target application isn't running" or "nothing is selected to
/// rename" are expected, routine outcomes a caller (voice command handling, a taught workflow) should
/// branch on, not catch. Shared across every plugin (VideoPsalm, OBS, PowerPoint, VLC, Chrome, Word,
/// Excel, File Manager) rather than each defining its own near-identical result type.
/// </summary>
public sealed record PluginActionResult(bool Success, string? Message = null)
{
    public static PluginActionResult Ok() => new(true);
    public static PluginActionResult Fail(string message) => new(false, message);
}
