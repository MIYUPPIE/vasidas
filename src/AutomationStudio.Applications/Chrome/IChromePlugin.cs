namespace AutomationStudio.Applications.Chrome;

/// <summary>High-level Chrome browser capabilities (DAS.md section 14's "More Applications" list).</summary>
public interface IChromePlugin
{
    Task<PluginActionResult> NewTabAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> CloseTabAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> ReloadAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> GoBackAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> GoForwardAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> OpenUrlAsync(string url, CancellationToken cancellationToken = default);
    Task<PluginActionResult> FindOnPageAsync(string text, CancellationToken cancellationToken = default);
}
