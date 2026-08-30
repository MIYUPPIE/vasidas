namespace AutomationStudio.Abstractions;

/// <summary>
/// Short-lived execution context (DAS.md section 8) — what's currently on screen / running, so a
/// command like "Next verse" can be resolved without repeating the reference. In-memory and
/// session-scoped by design ("short-lived"), not persisted.
/// </summary>
public sealed record ExecutionContextSnapshot(
    string? CurrentApplication = null,
    string? CurrentWorkflowId = null,
    BibleReference? CurrentVerse = null,
    int? CurrentSlide = null,
    string? CurrentMedia = null,
    string? CurrentScene = null,
    string? CurrentTask = null,
    Intent? PreviousCommand = null)
{
    public static ExecutionContextSnapshot Empty { get; } = new();
}

/// <summary>Tracks <see cref="ExecutionContextSnapshot"/> across commands (DAS.md section 8/31 — sits between the Intent Engine and the Task Planner).</summary>
public interface IContextManager
{
    ExecutionContextSnapshot Current { get; }

    void RecordCommand(Intent intent);
    void SetCurrentApplication(string? application);
    void SetCurrentWorkflow(string? workflowId);
    void SetCurrentVerse(BibleReference? reference);
    void SetCurrentSlide(int? slide);
    void SetCurrentMedia(string? media);
    void SetCurrentScene(string? scene);
    void SetCurrentTask(string? task);

    void Clear();
}
