namespace AutomationStudio.Applications.PowerPoint;

/// <summary>High-level PowerPoint capabilities (DAS.md section 14) — the exact method set DAS.md's own worked example lists for PowerPoint.</summary>
public interface IPowerPointPlugin
{
    Task<PluginActionResult> NextSlideAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> PreviousSlideAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> GoToSlideAsync(int slideNumber, CancellationToken cancellationToken = default);
    Task<PluginActionResult> StartPresentationAsync(CancellationToken cancellationToken = default);
    Task<PluginActionResult> EndPresentationAsync(CancellationToken cancellationToken = default);
}
