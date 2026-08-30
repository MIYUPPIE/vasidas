using AutomationStudio.Abstractions;

namespace AutomationStudio.Voice.Context;

/// <summary>In-memory, thread-safe short-lived execution context (DAS.md section 8). One instance is meant to live for the app's session — nothing here is persisted, matching "short-lived" from the spec.</summary>
public sealed class ContextManager : IContextManager
{
    private readonly Lock _lock = new();
    private ExecutionContextSnapshot _current = ExecutionContextSnapshot.Empty;

    public ExecutionContextSnapshot Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    public void RecordCommand(Intent intent) => Mutate(c => c with { PreviousCommand = intent });
    public void SetCurrentApplication(string? application) => Mutate(c => c with { CurrentApplication = application });
    public void SetCurrentWorkflow(string? workflowId) => Mutate(c => c with { CurrentWorkflowId = workflowId });
    public void SetCurrentVerse(BibleReference? reference) => Mutate(c => c with { CurrentVerse = reference });
    public void SetCurrentSlide(int? slide) => Mutate(c => c with { CurrentSlide = slide });
    public void SetCurrentMedia(string? media) => Mutate(c => c with { CurrentMedia = media });
    public void SetCurrentScene(string? scene) => Mutate(c => c with { CurrentScene = scene });
    public void SetCurrentTask(string? task) => Mutate(c => c with { CurrentTask = task });

    public void Clear() => Mutate(_ => ExecutionContextSnapshot.Empty);

    private void Mutate(Func<ExecutionContextSnapshot, ExecutionContextSnapshot> update)
    {
        lock (_lock)
        {
            _current = update(_current);
        }
    }
}
