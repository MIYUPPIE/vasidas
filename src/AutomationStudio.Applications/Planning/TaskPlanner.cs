using AutomationStudio.Abstractions;
using AutomationStudio.Applications.Chrome;
using AutomationStudio.Applications.Excel;
using AutomationStudio.Applications.FileManager;
using AutomationStudio.Applications.Obs;
using AutomationStudio.Applications.PowerPoint;
using AutomationStudio.Applications.Vlc;
using AutomationStudio.Applications.VideoPsalm;
using AutomationStudio.Applications.Word;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Core.Workflows;
using AutomationStudio.Storage.Repositories;

namespace AutomationStudio.Applications.Planning;

/// <summary>
/// Dispatches a resolved <see cref="Intent"/> to whichever plugin action or generic workflow
/// actually carries it out (DAS.md section 10) — the intent never touches the mouse/keyboard
/// directly. Every DISPLAY_BIBLE_VERSE/NEXT_SONG/CLEAR_SCREEN/etc. intent DAS.md section 9 defines
/// maps 1:1 onto an <see cref="IVideoPsalmPlugin"/> method; DAS.md section 14's OBS/PowerPoint
/// method lists and the PLAY_MEDIA/PAUSE_MEDIA/STOP_MEDIA intents section 9 already lists map the
/// same way onto <see cref="IObsPlugin"/>/<see cref="IPowerPointPlugin"/>/<see cref="IVlcPlugin"/>
/// (Phase 9 — "More Applications"). Every application plugin is optional (nullable, defaulting to
/// "not configured") since none of these applications are guaranteed installed on any given
/// machine — dispatching to a missing one fails clearly rather than throwing.
///
/// Chrome/File Manager get their own new intent names (NEW_TAB, NEW_FOLDER, etc.) — DAS.md section
/// 9 has no app-specific vocabulary for them, but its own text says "the system must allow new
/// intents to be registered through plugins," and these have no ambiguity risk since no other
/// plugin defines the same verb. SAVE_DOCUMENT (which DAS.md section 9 *does* list, generically)
/// dispatches to Word or Excel based on an explicit "application" parameter, the same pattern
/// OPEN_APPLICATION already uses — deliberately not resolved from "current application" context,
/// since nothing populates that yet (see DAS Phase 5's own scoping note). Word/Excel's other,
/// app-specific verbs (bold/italic/print/autosum/go-to-cell/...) are NOT given voice intents at
/// all: unlike Chrome/File Manager's verbs, generic names like "bold" or "find" would collide
/// across Word and Excel (and arguably PowerPoint) with no context-resolution mechanism yet to
/// disambiguate them — rather than guess or invent app-prefixed names DAS.md doesn't ask for, those
/// plugin methods stay fully built and testable, just not voice-reachable yet.
/// </summary>
public sealed class TaskPlanner(
    IVideoPsalmPlugin videoPsalm,
    WorkflowExecutionEngine engine,
    ApplicationLaunchRegistry? launchRegistry = null,
    IWorkflowRepository? workflowRepository = null,
    IObsPlugin? obs = null,
    IPowerPointPlugin? powerPoint = null,
    IVlcPlugin? vlc = null,
    IChromePlugin? chrome = null,
    IWordPlugin? word = null,
    IExcelPlugin? excel = null,
    IFileManagerPlugin? fileManager = null) : ITaskPlanner
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

        // OBS (DAS.md section 14).
        "START_RECORDING" => WithPlugin(obs, "OBS", p => p.StartRecordingAsync(cancellationToken)),
        "STOP_RECORDING" => WithPlugin(obs, "OBS", p => p.StopRecordingAsync(cancellationToken)),
        "START_STREAM" => WithPlugin(obs, "OBS", p => p.StartStreamingAsync(cancellationToken)),
        "STOP_STREAM" => WithPlugin(obs, "OBS", p => p.StopStreamingAsync(cancellationToken)),
        "SWITCH_SCENE" => SwitchSceneAsync(intent, cancellationToken),

        // PowerPoint (DAS.md section 14).
        "NEXT_SLIDE" => WithPlugin(powerPoint, "PowerPoint", p => p.NextSlideAsync(cancellationToken)),
        "PREVIOUS_SLIDE" => WithPlugin(powerPoint, "PowerPoint", p => p.PreviousSlideAsync(cancellationToken)),
        "GO_TO_SLIDE" => GoToSlideAsync(intent, cancellationToken),
        "START_PRESENTATION" => WithPlugin(powerPoint, "PowerPoint", p => p.StartPresentationAsync(cancellationToken)),
        "END_PRESENTATION" => WithPlugin(powerPoint, "PowerPoint", p => p.EndPresentationAsync(cancellationToken)),

        // VLC (DAS.md section 9's PLAY_MEDIA/PAUSE_MEDIA/STOP_MEDIA).
        "PLAY_MEDIA" => WithPlugin(vlc, "VLC", p => p.PlayAsync(cancellationToken)),
        "PAUSE_MEDIA" => WithPlugin(vlc, "VLC", p => p.PauseAsync(cancellationToken)),
        "STOP_MEDIA" => WithPlugin(vlc, "VLC", p => p.StopAsync(cancellationToken)),
        "NEXT_TRACK" => WithPlugin(vlc, "VLC", p => p.NextTrackAsync(cancellationToken)),
        "PREVIOUS_TRACK" => WithPlugin(vlc, "VLC", p => p.PreviousTrackAsync(cancellationToken)),
        "TOGGLE_MUTE" => WithPlugin(vlc, "VLC", p => p.ToggleMuteAsync(cancellationToken)),
        "TOGGLE_FULLSCREEN" => WithPlugin(vlc, "VLC", p => p.ToggleFullscreenAsync(cancellationToken)),

        // Chrome.
        "NEW_TAB" => WithPlugin(chrome, "Chrome", p => p.NewTabAsync(cancellationToken)),
        "CLOSE_TAB" => WithPlugin(chrome, "Chrome", p => p.CloseTabAsync(cancellationToken)),
        "RELOAD_PAGE" => WithPlugin(chrome, "Chrome", p => p.ReloadAsync(cancellationToken)),
        "GO_BACK" => WithPlugin(chrome, "Chrome", p => p.GoBackAsync(cancellationToken)),
        "GO_FORWARD" => WithPlugin(chrome, "Chrome", p => p.GoForwardAsync(cancellationToken)),
        "OPEN_URL" => OpenUrlAsync(intent, cancellationToken),
        "FIND_ON_PAGE" => FindOnPageAsync(intent, cancellationToken),

        // File Manager.
        "NEW_FOLDER" => WithPlugin(fileManager, "the file manager", p => p.NewFolderAsync(cancellationToken)),
        "COPY_FILE" => WithPlugin(fileManager, "the file manager", p => p.CopyAsync(cancellationToken)),
        "CUT_FILE" => WithPlugin(fileManager, "the file manager", p => p.CutAsync(cancellationToken)),
        "PASTE_FILE" => WithPlugin(fileManager, "the file manager", p => p.PasteAsync(cancellationToken)),
        "RENAME_FILE" => RenameFileAsync(intent, cancellationToken),
        "DELETE_FILE" => WithPlugin(fileManager, "the file manager", p => p.DeleteAsync(cancellationToken)),
        "SEARCH_FILES" => SearchFilesAsync(intent, cancellationToken),

        // Generic across Word/Excel (DAS.md section 9's SAVE_DOCUMENT).
        "SAVE_DOCUMENT" => SaveDocumentAsync(intent, cancellationToken),

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

    private Task<TaskExecutionResult> SwitchSceneAsync(Intent intent, CancellationToken cancellationToken)
    {
        var sceneName = intent.Parameter("scene");
        return string.IsNullOrWhiteSpace(sceneName)
            ? Task.FromResult(TaskExecutionResult.Fail("SWITCH_SCENE intent is missing a 'scene' name."))
            : WithPlugin(obs, "OBS", p => p.SwitchSceneAsync(sceneName, cancellationToken));
    }

    private Task<TaskExecutionResult> GoToSlideAsync(Intent intent, CancellationToken cancellationToken)
    {
        var slideText = intent.Parameter("slideNumber");
        return int.TryParse(slideText, out var slideNumber)
            ? WithPlugin(powerPoint, "PowerPoint", p => p.GoToSlideAsync(slideNumber, cancellationToken))
            : Task.FromResult(TaskExecutionResult.Fail("GO_TO_SLIDE intent is missing a valid 'slideNumber'."));
    }

    private Task<TaskExecutionResult> OpenUrlAsync(Intent intent, CancellationToken cancellationToken)
    {
        var url = intent.Parameter("url");
        return string.IsNullOrWhiteSpace(url)
            ? Task.FromResult(TaskExecutionResult.Fail("OPEN_URL intent is missing a 'url'."))
            : WithPlugin(chrome, "Chrome", p => p.OpenUrlAsync(url, cancellationToken));
    }

    private Task<TaskExecutionResult> FindOnPageAsync(Intent intent, CancellationToken cancellationToken)
    {
        var text = intent.Parameter("text");
        return string.IsNullOrWhiteSpace(text)
            ? Task.FromResult(TaskExecutionResult.Fail("FIND_ON_PAGE intent is missing 'text'."))
            : WithPlugin(chrome, "Chrome", p => p.FindOnPageAsync(text, cancellationToken));
    }

    private Task<TaskExecutionResult> RenameFileAsync(Intent intent, CancellationToken cancellationToken)
    {
        var newName = intent.Parameter("name");
        return string.IsNullOrWhiteSpace(newName)
            ? Task.FromResult(TaskExecutionResult.Fail("RENAME_FILE intent is missing a 'name'."))
            : WithPlugin(fileManager, "the file manager", p => p.RenameAsync(newName, cancellationToken));
    }

    private Task<TaskExecutionResult> SearchFilesAsync(Intent intent, CancellationToken cancellationToken)
    {
        var query = intent.Parameter("query");
        return string.IsNullOrWhiteSpace(query)
            ? Task.FromResult(TaskExecutionResult.Fail("SEARCH_FILES intent is missing a 'query'."))
            : WithPlugin(fileManager, "the file manager", p => p.SearchAsync(query, cancellationToken));
    }

    private Task<TaskExecutionResult> SaveDocumentAsync(Intent intent, CancellationToken cancellationToken)
    {
        var application = intent.Parameter("application")?.Trim().ToLowerInvariant();
        return application switch
        {
            "word" => WithPlugin(word, "Word", p => p.SaveAsync(cancellationToken)),
            "excel" => WithPlugin(excel, "Excel", p => p.SaveAsync(cancellationToken)),
            _ => Task.FromResult(TaskExecutionResult.Fail(
                "SAVE_DOCUMENT needs to know which application — pass 'application' as 'word' or 'excel'."))
        };
    }

    private async Task<TaskExecutionResult> RunGenericWorkflowAsync(string workflowId, IReadOnlyList<WorkflowAction> actions, CancellationToken cancellationToken)
    {
        var workflow = new Workflow { Id = workflowId, Name = workflowId, Actions = actions };
        var result = await engine.ExecuteAsync(workflow, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.Status == ActionStatus.Success
            ? TaskExecutionResult.Ok()
            : TaskExecutionResult.Fail(result.FailureReason ?? $"Workflow ended with status {result.Status}.");
    }

    private static Task<TaskExecutionResult> WithPlugin<TPlugin>(TPlugin? plugin, string applicationLabel, Func<TPlugin, Task<PluginActionResult>> call) =>
        plugin is null
            ? Task.FromResult(TaskExecutionResult.Fail($"No {applicationLabel} plugin is configured."))
            : ToResultAsync(call(plugin));

    private static async Task<TaskExecutionResult> ToResultAsync(Task<PluginActionResult> pluginCall)
    {
        var result = await pluginCall.ConfigureAwait(false);
        return result.Success ? TaskExecutionResult.Ok(result.Message) : TaskExecutionResult.Fail(result.Message ?? "The plugin action failed.");
    }
}
