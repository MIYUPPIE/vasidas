using AutomationStudio.Abstractions;

namespace AutomationStudio.Storage.Repositories;

/// <summary>
/// Implements <see cref="ICommandRegistry"/> over <see cref="IWorkflowRepository"/> — a taught
/// command IS a stored workflow (DAS.md section 25: "Start church service" → StartChurchService.workflow,
/// then "the command is now registered"), so this needs no storage of its own, just a name-to-id
/// lookup over what's already persisted. The caller (<see cref="AutomationStudio.Voice.Intents.DeterministicIntentRecognizer"/>)
/// is responsible for normalizing before calling — see <see cref="ICommandRegistry"/>'s contract —
/// and <c>TeachingSession</c> names a workflow using that exact same normalized text, so an exact
/// match here is correct rather than a heuristic.
/// </summary>
public sealed class WorkflowCommandRegistry(IWorkflowRepository repository) : ICommandRegistry
{
    public async Task<string?> ResolveWorkflowIdAsync(string normalizedText, CancellationToken cancellationToken = default)
    {
        if (normalizedText.Length == 0)
        {
            return null;
        }

        var workflows = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var workflow in workflows)
        {
            if (string.Equals(Normalize(workflow.Name), normalizedText, StringComparison.Ordinal))
            {
                return workflow.Id;
            }
        }

        return null;
    }

    /// <summary>Trim + lowercase only, deliberately not full punctuation-stripping normalization: a taught workflow's name is itself already the normalized text <c>DeterministicIntentRecognizer</c> passed to <c>TeachingSession</c> when it was recorded, so this only needs to undo incidental casing/whitespace, not reimplement <c>CommandNormalizer</c> (which lives in Voice — a layer this project doesn't depend on).</summary>
    private static string Normalize(string text) => text.Trim().ToLowerInvariant();
}
