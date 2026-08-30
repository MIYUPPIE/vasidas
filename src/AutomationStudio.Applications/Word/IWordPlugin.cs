namespace AutomationStudio.Applications.Word;

/// <summary>High-level Microsoft Word capabilities (DAS.md section 14's "More Applications" list).</summary>
public interface IWordPlugin
{
    Task<PluginActionResult> NewDocumentAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> SaveAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> PrintAsync(CancellationToken cancellationToken = default);

    Task<PluginActionResult> ToggleBoldAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> ToggleItalicAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> ToggleUnderlineAsync(CancellationToken cancellationToken = default);

    Task<PluginActionResult> FindAsync(string text, CancellationToken cancellationToken = default);
}
