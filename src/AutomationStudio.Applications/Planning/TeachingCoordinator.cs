using AutomationStudio.Core.Workflows;
using AutomationStudio.Recorder;
using AutomationStudio.Storage;
using AutomationStudio.Storage.Repositories;

namespace AutomationStudio.Applications.Planning;

public enum TeachingState
{
    Idle,
    AwaitingCommandName,
    Recording
}

/// <summary>
/// Drives DAS.md section 25's "teach new command" dialogue end to end: "Teach new command" →
/// "Command name: ..." → the user performs the process manually (captured by
/// <see cref="TeachingSession"/>) → "stop teaching" → the demonstration is normalized into a
/// <see cref="Workflow"/>, persisted, and immediately runnable by saying the taught command name
/// again (resolved via <see cref="WorkflowCommandRegistry"/>/RUN_WORKFLOW). <see cref="VoiceAssistant"/>
/// is the caller that routes TEACH_COMMAND/STOP_TEACHING intents here instead of to
/// <see cref="ITaskPlanner"/>, and — while <see cref="State"/> is <see cref="TeachingState.AwaitingCommandName"/> —
/// routes the next raw transcription (not an intent) to <see cref="SetCommandNameAndStartRecording"/>,
/// since a command name must never be matched against a known command pattern.
/// </summary>
public sealed class TeachingCoordinator(TeachingSession session, IWorkflowRepository repository)
{
    public TeachingState State { get; private set; } = TeachingState.Idle;

    public string? PendingCommandName { get; private set; }

    /// <summary>"Teach new command" was heard with no name in the same breath — the next utterance is the name.</summary>
    public void BeginTeaching()
    {
        if (State != TeachingState.Idle)
        {
            throw new InvalidOperationException($"Cannot begin teaching while in state '{State}'.");
        }

        State = TeachingState.AwaitingCommandName;
    }

    /// <summary>Starts capturing the demonstration for <paramref name="commandName"/> — either the name given inline with "teach new command", or the following utterance after <see cref="BeginTeaching"/>.</summary>
    public async Task StartRecordingAsync(string commandName, CancellationToken cancellationToken = default)
    {
        if (State is not (TeachingState.Idle or TeachingState.AwaitingCommandName))
        {
            throw new InvalidOperationException($"Cannot start recording while in state '{State}'.");
        }

        PendingCommandName = commandName;
        await session.StartAsync(cancellationToken).ConfigureAwait(false);
        State = TeachingState.Recording;
    }

    /// <summary>Stops capturing, normalizes and persists the taught workflow, and returns it. The command name used to start recording is immediately usable — saying it again resolves to RUN_WORKFLOW via <see cref="WorkflowCommandRegistry"/>, since that registry matches directly against stored workflow names.</summary>
    public async Task<Workflow> FinishTeachingAsync(CancellationToken cancellationToken = default)
    {
        if (State != TeachingState.Recording || PendingCommandName is not { } commandName)
        {
            throw new InvalidOperationException($"Cannot finish teaching while in state '{State}'.");
        }

        var workflow = session.Stop(commandName);
        await repository.SaveAsync(new WorkflowRecord
        {
            Workflow = workflow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        }, cancellationToken).ConfigureAwait(false);

        State = TeachingState.Idle;
        PendingCommandName = null;
        return workflow;
    }

    /// <summary>Abandons teaching in progress, discarding whatever was captured. Safe to call from any state.</summary>
    public void CancelTeaching()
    {
        if (State == TeachingState.Recording && PendingCommandName is { } commandName)
        {
            session.Stop(commandName); // drains the recorder; the result is deliberately discarded.
        }

        State = TeachingState.Idle;
        PendingCommandName = null;
    }
}
