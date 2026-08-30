using AutomationStudio.Abstractions;
using AutomationStudio.Voice;

namespace AutomationStudio.Applications.Planning;

/// <summary>
/// The culmination of DAS.md sections 5-10 in one call: microphone → voice activity detection →
/// speech-to-text → intent recognition → context resolution → task planning → execution. Every
/// earlier phase deliberately stopped at "here is what the user meant" (an <see cref="Intent"/>);
/// this is the first point where that intent actually does something.
///
/// Optionally also drives DAS.md section 25's "teach new command" dialogue: when
/// <paramref name="teaching"/> is supplied, TEACH_COMMAND/STOP_TEACHING intents are intercepted here
/// instead of reaching <see cref="ITaskPlanner"/> (which has no plan for them — they're not things
/// a plugin does, they're this loop changing what it listens for next), and while
/// <see cref="TeachingState.AwaitingCommandName"/>, the next utterance is taken as a raw command
/// name rather than run through intent recognition at all — "Start church service" said as a
/// command *name* must not be matched against the OPEN_APPLICATION pattern it would otherwise trigger.
/// </summary>
public sealed class VoiceAssistant(VoiceCommandListener listener, ITaskPlanner taskPlanner, TeachingCoordinator? teaching = null)
{
    /// <summary>Listens for one command and carries it out. Returns null if nothing was understood (nothing said, unrecognized speech, or a context-dependent command with nothing to resolve against) — otherwise the outcome of actually executing it (which, mid-teaching, may just be an acknowledgement rather than a plugin/workflow result).</summary>
    public async Task<TaskExecutionResult?> ListenAndExecuteAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (teaching is { State: TeachingState.AwaitingCommandName })
        {
            var name = await listener.TranscribeAsync(timeout, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            await teaching.StartRecordingAsync(name, cancellationToken).ConfigureAwait(false);
            return TaskExecutionResult.Ok($"Recording '{name}'. Say \"stop teaching\" when done.");
        }

        var intent = await listener.ListenAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (intent is null)
        {
            return null;
        }

        if (teaching is { State: TeachingState.Idle } && intent.Name == "TEACH_COMMAND")
        {
            var inlineName = intent.Parameter("name");
            if (!string.IsNullOrWhiteSpace(inlineName))
            {
                await teaching.StartRecordingAsync(inlineName, cancellationToken).ConfigureAwait(false);
                return TaskExecutionResult.Ok($"Recording '{inlineName}'. Say \"stop teaching\" when done.");
            }

            teaching.BeginTeaching();
            return TaskExecutionResult.Ok("Command name?");
        }

        if (teaching is { State: TeachingState.Recording } && intent.Name == "STOP_TEACHING")
        {
            var workflow = await teaching.FinishTeachingAsync(cancellationToken).ConfigureAwait(false);
            return TaskExecutionResult.Ok($"Taught '{workflow.Name}'.");
        }

        return await taskPlanner.ExecuteAsync(intent, cancellationToken).ConfigureAwait(false);
    }
}
