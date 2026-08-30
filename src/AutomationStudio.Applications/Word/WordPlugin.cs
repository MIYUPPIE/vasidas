using AutomationStudio.Applications.Plugins;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;

namespace AutomationStudio.Applications.Word;

/// <summary>Profile-driven Word automation (DAS.md section 14) — every capability sends <see cref="WordProfile"/>'s documented shortcut and runs it through the same generic <see cref="WorkflowExecutionEngine"/> everything else uses.</summary>
public sealed class WordPlugin(WorkflowExecutionEngine engine, WordProfile? profile = null)
    : ApplicationPluginBase(engine, (profile ?? WordProfile.Default).ProcessName), IWordPlugin
{
    private readonly WordProfile _profile = profile ?? WordProfile.Default;

    public Task<PluginActionResult> NewDocumentAsync(CancellationToken cancellationToken = default) =>
        RunAsync("word-new-document", [FocusApplication(), KeyCombo(_profile.NewDocumentModifier, _profile.NewDocumentKey)], cancellationToken);

    public Task<PluginActionResult> SaveAsync(CancellationToken cancellationToken = default) =>
        RunAsync("word-save", [FocusApplication(), KeyCombo(_profile.SaveModifier, _profile.SaveKey)], cancellationToken);

    public Task<PluginActionResult> PrintAsync(CancellationToken cancellationToken = default) =>
        RunAsync("word-print", [FocusApplication(), KeyCombo(_profile.PrintModifier, _profile.PrintKey)], cancellationToken);

    public Task<PluginActionResult> ToggleBoldAsync(CancellationToken cancellationToken = default) =>
        RunAsync("word-toggle-bold", [FocusApplication(), KeyCombo(_profile.BoldModifier, _profile.BoldKey)], cancellationToken);

    public Task<PluginActionResult> ToggleItalicAsync(CancellationToken cancellationToken = default) =>
        RunAsync("word-toggle-italic", [FocusApplication(), KeyCombo(_profile.ItalicModifier, _profile.ItalicKey)], cancellationToken);

    public Task<PluginActionResult> ToggleUnderlineAsync(CancellationToken cancellationToken = default) =>
        RunAsync("word-toggle-underline", [FocusApplication(), KeyCombo(_profile.UnderlineModifier, _profile.UnderlineKey)], cancellationToken);

    public Task<PluginActionResult> FindAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(PluginActionResult.Fail("No search text was given."));
        }

        List<WorkflowAction> actions = [FocusApplication(), KeyCombo(_profile.FindModifier, _profile.FindKey), TypeText(text)];
        return RunAsync("word-find", actions, cancellationToken);
    }
}
