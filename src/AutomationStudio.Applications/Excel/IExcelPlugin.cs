namespace AutomationStudio.Applications.Excel;

/// <summary>High-level Microsoft Excel capabilities (DAS.md section 14's "More Applications" list).</summary>
public interface IExcelPlugin
{
    Task<PluginActionResult> NewWorkbookAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> SaveAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> GoToCellAsync(string cellReference, CancellationToken cancellationToken = default);
    Task<PluginActionResult> InsertAutoSumAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> ToggleBoldAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> FindAsync(string text, CancellationToken cancellationToken = default);
}
