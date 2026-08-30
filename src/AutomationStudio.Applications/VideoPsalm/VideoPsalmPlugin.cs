using AutomationStudio.Abstractions;
using AutomationStudio.Applications.Plugins;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;

namespace AutomationStudio.Applications.VideoPsalm;

/// <summary>
/// Profile-driven VideoPsalm automation (DAS.md section 14/15): every capability builds a short
/// Workflow from <see cref="VideoPsalmProfile"/>'s documented shortcuts and runs it through the
/// same generic <see cref="WorkflowExecutionEngine"/> everything else uses — no VideoPsalm-specific
/// code inside the engine itself (section 42).
/// </summary>
public sealed class VideoPsalmPlugin : ApplicationPluginBase, IVideoPsalmPlugin
{
    private readonly IBibleService _bibleService;
    private readonly VideoPsalmProfile _profile;

    public VideoPsalmPlugin(WorkflowExecutionEngine engine, IBibleService bibleService, VideoPsalmProfile? profile = null)
        : base(engine, (profile ?? VideoPsalmProfile.Default).ProcessName)
    {
        _bibleService = bibleService;
        _profile = profile ?? VideoPsalmProfile.Default;
    }

    public async Task<PluginActionResult> DisplayBibleVerseAsync(BibleReference reference, CancellationToken cancellationToken = default)
    {
        // Validate against real scripture before touching VideoPsalm at all (DAS.md section 16: "the
        // system can therefore validate the user's request before operating VideoPsalm").
        var verse = await _bibleService.GetVerseAsync(reference.Book, reference.Chapter, reference.StartVerse ?? 1, cancellationToken).ConfigureAwait(false);
        if (verse is null)
        {
            return PluginActionResult.Fail($"{reference} does not exist in this Bible.");
        }

        List<WorkflowAction> actions =
        [
            FocusApplication(),
            KeyCombo(_profile.BiblesPanelModifier, _profile.BiblesPanelKey),
            KeyCombo(_profile.FocusSearchModifier, _profile.FocusSearchKey),
            TypeText(reference.ToString()),
            PressKey(_profile.ConfirmSelectionKey),
        ];

        return await RunAsync("videopsalm-display-verse", actions, cancellationToken).ConfigureAwait(false);
    }

    public Task<PluginActionResult> NextVerseAsync(CancellationToken cancellationToken = default) =>
        RunAsync("videopsalm-next-verse", [FocusApplication(), PressKey(_profile.NextSlideKey)], cancellationToken);

    public Task<PluginActionResult> PreviousVerseAsync(CancellationToken cancellationToken = default) =>
        RunAsync("videopsalm-previous-verse", [FocusApplication(), PressKey(_profile.PreviousSlideKey)], cancellationToken);

    public Task<PluginActionResult> DisplaySongAsync(string title, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Task.FromResult(PluginActionResult.Fail("No song title was given."));
        }

        List<WorkflowAction> actions =
        [
            FocusApplication(),
            KeyCombo(_profile.SongbooksPanelModifier, _profile.SongbooksPanelKey),
            KeyCombo(_profile.FocusSearchModifier, _profile.FocusSearchKey),
            TypeText(title),
            PressKey(_profile.ConfirmSelectionKey),
        ];

        return RunAsync("videopsalm-display-song", actions, cancellationToken);
    }

    public Task<PluginActionResult> NextSongAsync(CancellationToken cancellationToken = default) =>
        RunAsync("videopsalm-next-song", [FocusApplication(), KeyCombo(_profile.NextAgendaItemModifier, _profile.NextAgendaItemKey)], cancellationToken);

    public Task<PluginActionResult> PreviousSongAsync(CancellationToken cancellationToken = default) =>
        RunAsync("videopsalm-previous-song", [FocusApplication(), PressKey(_profile.PreviousAgendaItemKey)], cancellationToken);

    public Task<PluginActionResult> ClearScreenAsync(CancellationToken cancellationToken = default) =>
        RunAsync("videopsalm-clear-screen", [FocusApplication(), PressKey(_profile.BlackScreenKey)], cancellationToken);

    public Task<PluginActionResult> HideOutputAsync(CancellationToken cancellationToken = default) =>
        RunAsync("videopsalm-hide-output", [FocusApplication(), PressKey(_profile.ExitPresentationKey)], cancellationToken);
}
