using AutomationStudio.Abstractions;
using AutomationStudio.Applications.VideoPsalm;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Core.Workflows;
using AutomationStudio.Storage.Repositories;

namespace AutomationStudio.Applications.Planning;

/// <summary>
/// Dispatches a resolved <see cref="Intent"/> to whichever plugin action or generic workflow
/// actually carries it out (DAS.md section 10) — the intent never touches the mouse/keyboard
/// directly. Every DISPLAY_BIBLE_VERSE/NEXT_SONG/CLEAR_SCREEN/etc. intent DAS.md section 9 defines
/// maps 1:1 onto an <see cref="IVideoPsalmPlugin"/> method (matching DAS section 10's own worked
/// example almost exactly: "Identify presentation application → Activate → Locate Bible search →
/// Enter book/chapter/verse → Select → Display" is precisely what
/// <see cref="IVideoPsalmPlugin.DisplayBibleVerseAsync"/> already does). Intents this planner has no
/// plan for yet (OBS/PowerPoint-shaped ones from Phase 3's original command set — no plugin for
/// either exists) fail clearly rather than silently no-op or guess; that's deliberate, matching
/// section 10's own framing — the planner picks between *existing* workflows/plugin actions, it
/// doesn't invent one (dynamically generated workflows are Phase 10, gated behind local AI).
/// </summary>
public sealed class TaskPlanner(
    IVideoPsalmPlugin videoPsalm,
    WorkflowExecutionEngine engine,
    ApplicationLaunchRegistry? launchRegistry = null,
    IWorkflowRepository? workflowRepository = null) : ITaskPlanner
{
    private readonly ApplicationLaunchRegistry _launchRegistry = launchRegistry ?? new ApplicationLaunchRegistry();

    public Task<TaskExecutionResult> ExecuteAsync(Intent intent, CancellationToken cancellationToken = default) => intent.Name switch
    {
        "DISPLAY_BIBLE_VERSE" => DisplayBibleVerseAsync(intent, cancellationToken),
        "RUN_WORKFLOW" => RunTaughtWorkflowAsync(intent, cancellationToken),
        "NEXT_VERSE" => Task.FromResult(TaskExecutionResult.Fail(
            "NEXT_VERSE must be resolved to DISPLAY_BIBLE_VERSE (via IntentContextResolver) before reaching the task planner.")),
        "PREVIOUS_VERSE" => Task.FromResult(TaskExecutionResult.Fail(
            "PREVIOUS_VERSE must be resolved to DISPLAY_BIBLE_VERSE (via IntentContextResolver) before reaching the task planner.")),
        "DISPLAY_SONG" => DisplaySongAsync(intent, cancellationToken),
        "NEXT_SONG" => ToResultAsync(videoPsalm.NextSongAsync(cancellationToken)),
        "PREVIOUS_SONG" => ToResultAsync(videoPsalm.PreviousSongAsync(cancellationToken)),
        "CLEAR_SCREEN" => ToResultAsync(videoPsalm.ClearScreenAsync(cancellationToken)),
        "HIDE_OUTPUT" => ToResultAsync(videoPsalm.HideOutputAsync(cancellationToken)),
        "OPEN_APPLICATION" => OpenApplicationAsync(intent, cancellationToken),
        "CLOSE_APPLICATION" => CloseApplicationAsync(intent, cancellationToken),
        _ => Task.FromResult(TaskExecutionResult.Fail($"No task plan is registered for intent '{intent.Name}' yet."))
    };

    private Task<TaskExecutionResult> DisplayBibleVerseAsync(Intent intent, CancellationToken cancellationToken)
    {
        var book = intent.Parameter("book");
        var chapterText = intent.Parameter("chapter");
        if (book is null || chapterText is null || !int.TryParse(chapterText, out var chapter))
        {
            return Task.FromResult(TaskExecutionResult.Fail("DISPLAY_BIBLE_VERSE intent is missing 'book'/'chapter'."));
        }

        var startVerse = int.TryParse(intent.Parameter("startVerse"), out var s) ? s : (int?)null;
        var endVerse = int.TryParse(intent.Parameter("endVerse"), out var e) ? e : (int?)null;
        var reference = new BibleReference(book, chapter, startVerse, endVerse);

        return ToResultAsync(videoPsalm.DisplayBibleVerseAsync(reference, cancellationToken));
    }

    private async Task<TaskExecutionResult> RunTaughtWorkflowAsync(Intent intent, CancellationToken cancellationToken)
    {
        var workflowId = intent.Parameter("workflowId");
        if (string.IsNullOrWhiteSpace(workflowId))
        {
            return TaskExecutionResult.Fail("RUN_WORKFLOW intent is missing a 'workflowId'.");
        }

        if (workflowRepository is null)
        {
            return TaskExecutionResult.Fail("No workflow repository is configured; taught commands cannot be executed.");
        }

        var record = await workflowRepository.GetAsync(workflowId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return TaskExecutionResult.Fail($"No workflow is stored with id '{workflowId}'.");
        }

        var result = await engine.ExecuteAsync(record.Workflow, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.Status == ActionStatus.Success
            ? TaskExecutionResult.Ok()
            : TaskExecutionResult.Fail(result.FailureReason ?? $"Workflow ended with status {result.Status}.");
    }

    private Task<TaskExecutionResult> DisplaySongAsync(Intent intent, CancellationToken cancellationToken)
    {
        var title = intent.Parameter("title");
        return string.IsNullOrWhiteSpace(title)
            ? Task.FromResult(TaskExecutionResult.Fail("DISPLAY_SONG intent is missing a 'title'."))
            : ToResultAsync(videoPsalm.DisplaySongAsync(title, cancellationToken));
    }

    private async Task<TaskExecutionResult> OpenApplicationAsync(Intent intent, CancellationToken cancellationToken)
    {
        var name = intent.Parameter("application");
        if (string.IsNullOrWhiteSpace(name))
        {
            return TaskExecutionResult.Fail("OPEN_APPLICATION intent is missing an 'application' name.");
        }

        var path = _launchRegistry.ResolvePath(name);
        if (path is null)
        {
            return TaskExecutionResult.Fail($"No known launch path is registered for '{name}'.");
        }

        return await RunGenericWorkflowAsync("task-open-application", [new OpenApplicationAction { Path = path }], cancellationToken).ConfigureAwait(false);
    }

    private Task<TaskExecutionResult> CloseApplicationAsync(Intent intent, CancellationToken cancellationToken)
    {
        var processName = intent.Parameter("application");
        return string.IsNullOrWhiteSpace(processName)
            ? Task.FromResult(TaskExecutionResult.Fail("CLOSE_APPLICATION intent is missing an 'application' name."))
            : RunGenericWorkflowAsync("task-close-application", [new CloseApplicationAction { ProcessName = processName }], cancellationToken);
    }

    private async Task<TaskExecutionResult> RunGenericWorkflowAsync(string workflowId, IReadOnlyList<WorkflowAction> actions, CancellationToken cancellationToken)
    {
        var workflow = new Workflow { Id = workflowId, Name = workflowId, Actions = actions };
        var result = await engine.ExecuteAsync(workflow, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.Status == ActionStatus.Success
            ? TaskExecutionResult.Ok()
            : TaskExecutionResult.Fail(result.FailureReason ?? $"Workflow ended with status {result.Status}.");
    }

    private static async Task<TaskExecutionResult> ToResultAsync(Task<VideoPsalmActionResult> pluginCall)
    {
        var result = await pluginCall.ConfigureAwait(false);
        return result.Success ? TaskExecutionResult.Ok(result.Message) : TaskExecutionResult.Fail(result.Message ?? "The plugin action failed.");
    }
}
