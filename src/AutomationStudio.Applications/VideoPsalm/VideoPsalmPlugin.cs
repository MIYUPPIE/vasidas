using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Core.Workflows;

namespace AutomationStudio.Applications.VideoPsalm;

/// <summary>
/// Profile-driven VideoPsalm automation (DAS.md section 14/15): every capability builds a short
/// <see cref="Workflow"/> from <see cref="VideoPsalmProfile"/>'s documented shortcuts and runs it
/// through the same generic <see cref="WorkflowExecutionEngine"/> everything else uses — no
/// VideoPsalm-specific code inside the engine itself (section 42).
/// </summary>
public sealed class VideoPsalmPlugin(WorkflowExecutionEngine engine, IBibleService bibleService, VideoPsalmProfile? profile = null) : IVideoPsalmPlugin
{
    private readonly VideoPsalmProfile _profile = profile ?? VideoPsalmProfile.Default;

    public async Task<VideoPsalmActionResult> DisplayBibleVerseAsync(BibleReference reference, CancellationToken cancellationToken = default)
    {
        // Validate against real scripture before touching VideoPsalm at all (DAS.md section 16: "the
        // system can therefore validate the user's request before operating VideoPsalm").
        var verse = await bibleService.GetVerseAsync(reference.Book, reference.Chapter, reference.StartVerse ?? 1, cancellationToken).ConfigureAwait(false);
        if (verse is null)
        {
            return VideoPsalmActionResult.Fail($"{reference} does not exist in this Bible.");
        }

        var actions = new List<WorkflowAction>
        {
            FocusApplication(),
            KeyCombo(_profile.BiblesPanelModifier, _profile.BiblesPanelKey),
            KeyCombo(_profile.FocusSearchModifier, _profile.FocusSearchKey),
            new TypeTextAction { Text = reference.ToString() },
            new PressKeyAction { Key = _profile.ConfirmSelectionKey },
        };

        return await RunAsync("videopsalm-display-verse", actions, cancellationToken).ConfigureAwait(false);
    }

    public Task<VideoPsalmActionResult> NextVerseAsync(CancellationToken cancellationToken = default) =>
        RunAsync("videopsalm-next-verse", [FocusApplication(), new PressKeyAction { Key = _profile.NextSlideKey }], cancellationToken);

    public Task<VideoPsalmActionResult> PreviousVerseAsync(CancellationToken cancellationToken = default) =>
        RunAsync("videopsalm-previous-verse", [FocusApplication(), new PressKeyAction { Key = _profile.PreviousSlideKey }], cancellationToken);

    public Task<VideoPsalmActionResult> DisplaySongAsync(string title, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Task.FromResult(VideoPsalmActionResult.Fail("No song title was given."));
        }

        List<WorkflowAction> actions =
        [
            FocusApplication(),
            KeyCombo(_profile.SongbooksPanelModifier, _profile.SongbooksPanelKey),
            KeyCombo(_profile.FocusSearchModifier, _profile.FocusSearchKey),
            new TypeTextAction { Text = title },
            new PressKeyAction { Key = _profile.ConfirmSelectionKey },
        ];

        return RunAsync("videopsalm-display-song", actions, cancellationToken);
    }

    public Task<VideoPsalmActionResult> NextSongAsync(CancellationToken cancellationToken = default) =>
        RunAsync("videopsalm-next-song", [FocusApplication(), KeyCombo(_profile.NextAgendaItemModifier, _profile.NextAgendaItemKey)], cancellationToken);

    public Task<VideoPsalmActionResult> PreviousSongAsync(CancellationToken cancellationToken = default) =>
        RunAsync("videopsalm-previous-song", [FocusApplication(), new PressKeyAction { Key = _profile.PreviousAgendaItemKey }], cancellationToken);

    public Task<VideoPsalmActionResult> ClearScreenAsync(CancellationToken cancellationToken = default) =>
        RunAsync("videopsalm-clear-screen", [FocusApplication(), new PressKeyAction { Key = _profile.BlackScreenKey }], cancellationToken);

    public Task<VideoPsalmActionResult> HideOutputAsync(CancellationToken cancellationToken = default) =>
        RunAsync("videopsalm-hide-output", [FocusApplication(), new PressKeyAction { Key = _profile.ExitPresentationKey }], cancellationToken);

    private FocusApplicationAction FocusApplication() => new() { ProcessName = _profile.ProcessName };

    private static KeyCombinationAction KeyCombo(Key modifier, Key key) => new() { Keys = [modifier, key] };

    private async Task<VideoPsalmActionResult> RunAsync(string workflowId, IReadOnlyList<WorkflowAction> actions, CancellationToken cancellationToken)
    {
        var workflow = new Workflow { Id = workflowId, Name = workflowId, Actions = actions };
        var result = await engine.ExecuteAsync(workflow, cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.Status == ActionStatus.Success
            ? VideoPsalmActionResult.Ok()
            : VideoPsalmActionResult.Fail(result.FailureReason ?? $"Workflow ended with status {result.Status}.");
    }
}
