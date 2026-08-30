using AutomationStudio.Abstractions;
using AutomationStudio.Voice;

namespace AutomationStudio.Applications.Planning;

/// <summary>
/// The culmination of DAS.md sections 5-10 in one call: microphone → voice activity detection →
/// speech-to-text → intent recognition → context resolution → task planning → execution. Every
/// earlier phase deliberately stopped at "here is what the user meant" (an <see cref="Intent"/>);
/// this is the first point where that intent actually does something.
/// </summary>
public sealed class VoiceAssistant(VoiceCommandListener listener, ITaskPlanner taskPlanner)
{
    /// <summary>Listens for one command and carries it out. Returns null if nothing was understood (nothing said, unrecognized speech, or a context-dependent command with nothing to resolve against) — otherwise the outcome of actually executing it.</summary>
    public async Task<TaskExecutionResult?> ListenAndExecuteAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var intent = await listener.ListenAsync(timeout, cancellationToken).ConfigureAwait(false);
        return intent is null ? null : await taskPlanner.ExecuteAsync(intent, cancellationToken).ConfigureAwait(false);
    }
}
