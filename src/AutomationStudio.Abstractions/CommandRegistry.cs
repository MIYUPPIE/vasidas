namespace AutomationStudio.Abstractions;

/// <summary>
/// Resolves an already-normalized spoken/typed phrase to the id of a workflow taught for it (DAS.md
/// section 25 — "the command is now registered": "Start church service" → <c>StartChurchService.workflow</c>).
/// The caller is responsible for normalizing (<c>CommandNormalizer.Normalize</c>) before calling —
/// this contract deliberately doesn't re-normalize, so there is exactly one place that decides what
/// "the same phrase" means.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>Returns the workflow id registered for <paramref name="normalizedText"/>, or null if no taught command matches it.</summary>
    Task<string?> ResolveWorkflowIdAsync(string normalizedText, CancellationToken cancellationToken = default);
}
