namespace AutomationStudio.Core.Execution;

/// <summary>
/// The RUN / PAUSE / STOP / EMERGENCY STOP control surface required by section 28. Handed to the
/// caller when a workflow starts so the UI (or a test) can pause or abort a run that is already
/// controlling the mouse and keyboard.
/// </summary>
/// <remarks>
/// The pause gate is a <see cref="TaskCompletionSource"/> the engine awaits, not a blocking
/// <c>ManualResetEventSlim.Wait</c> — a blocking wait would tie up a thread-pool thread for as long
/// as the workflow stays paused, which is wasteful at best and, under thread-pool pressure (many
/// concurrent workflow runs, or a constrained host), can stall unrelated work for a long time.
/// </remarks>
public sealed class WorkflowRunControl : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _gateLock = new();
    private TaskCompletionSource? _pauseGate;

    public CancellationToken Token => _cts.Token;

    public bool IsPaused => Volatile.Read(ref _pauseGate) is not null;

    /// <summary>Stops the workflow. Cannot be undone — matches the "Emergency Stop" requirement.</summary>
    public void Stop() => _cts.Cancel();

    public void Pause()
    {
        lock (_gateLock)
        {
            _pauseGate ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Resume()
    {
        TaskCompletionSource? gate;
        lock (_gateLock)
        {
            gate = _pauseGate;
            _pauseGate = null;
        }

        gate?.TrySetResult();
    }

    /// <summary>Awaits (without blocking a thread) while paused; completes immediately once resumed, stopped, or if not paused.</summary>
    internal Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        Task? waitTask;
        lock (_gateLock)
        {
            waitTask = _pauseGate?.Task;
        }

        return waitTask is null ? Task.CompletedTask : waitTask.WaitAsync(cancellationToken);
    }

    public void Dispose() => _cts.Dispose();
}
