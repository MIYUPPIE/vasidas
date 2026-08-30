using AutomationStudio.Abstractions;
using AutomationStudio.Applications.Plugins;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;

namespace AutomationStudio.Applications.FileManager;

/// <summary>Profile-driven file manager automation (DAS.md section 14) — every capability sends <see cref="FileManagerProfile"/>'s documented shortcut and runs it through the same generic <see cref="WorkflowExecutionEngine"/> everything else uses.</summary>
public sealed class FileManagerPlugin(WorkflowExecutionEngine engine, FileManagerProfile? profile = null)
    : ApplicationPluginBase(engine, (profile ?? FileManagerProfile.Default).ProcessName), IFileManagerPlugin
{
    private readonly FileManagerProfile _profile = profile ?? FileManagerProfile.Default;

    public Task<PluginActionResult> NewFolderAsync(CancellationToken cancellationToken = default) =>
        RunAsync(
            "filemanager-new-folder",
            [FocusApplication(), KeyCombo(_profile.NewFolderModifier1, _profile.NewFolderModifier2, _profile.NewFolderKey)],
            cancellationToken);

    public Task<PluginActionResult> CopyAsync(CancellationToken cancellationToken = default) =>
        RunAsync("filemanager-copy", [FocusApplication(), KeyCombo(_profile.CopyModifier, _profile.CopyKey)], cancellationToken);

    public Task<PluginActionResult> CutAsync(CancellationToken cancellationToken = default) =>
        RunAsync("filemanager-cut", [FocusApplication(), KeyCombo(_profile.CutModifier, _profile.CutKey)], cancellationToken);

    public Task<PluginActionResult> PasteAsync(CancellationToken cancellationToken = default) =>
        RunAsync("filemanager-paste", [FocusApplication(), KeyCombo(_profile.PasteModifier, _profile.PasteKey)], cancellationToken);

    public Task<PluginActionResult> RenameAsync(string newName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return Task.FromResult(PluginActionResult.Fail("No new name was given."));
        }

        List<WorkflowAction> actions =
        [
            FocusApplication(),
            PressKey(_profile.RenameKey),
            TypeText(newName),
            PressKey(Key.Enter),
        ];
        return RunAsync("filemanager-rename", actions, cancellationToken);
    }

    public Task<PluginActionResult> DeleteAsync(CancellationToken cancellationToken = default) =>
        RunAsync("filemanager-delete", [FocusApplication(), PressKey(_profile.DeleteKey)], cancellationToken);

    public Task<PluginActionResult> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(PluginActionResult.Fail("No search query was given."));
        }

        List<WorkflowAction> actions = [FocusApplication(), KeyCombo(_profile.SearchModifier, _profile.SearchKey), TypeText(query)];
        return RunAsync("filemanager-search", actions, cancellationToken);
    }
}
