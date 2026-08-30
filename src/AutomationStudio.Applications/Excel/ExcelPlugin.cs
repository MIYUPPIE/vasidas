using AutomationStudio.Abstractions;
using AutomationStudio.Applications.Plugins;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;

namespace AutomationStudio.Applications.Excel;

/// <summary>Profile-driven Excel automation (DAS.md section 14) — every capability sends <see cref="ExcelProfile"/>'s documented shortcut and runs it through the same generic <see cref="WorkflowExecutionEngine"/> everything else uses.</summary>
public sealed class ExcelPlugin(WorkflowExecutionEngine engine, ExcelProfile? profile = null)
    : ApplicationPluginBase(engine, (profile ?? ExcelProfile.Default).ProcessName), IExcelPlugin
{
    private readonly ExcelProfile _profile = profile ?? ExcelProfile.Default;

    public Task<PluginActionResult> NewWorkbookAsync(CancellationToken cancellationToken = default) =>
        RunAsync("excel-new-workbook", [FocusApplication(), KeyCombo(_profile.NewWorkbookModifier, _profile.NewWorkbookKey)], cancellationToken);

    public Task<PluginActionResult> SaveAsync(CancellationToken cancellationToken = default) =>
        RunAsync("excel-save", [FocusApplication(), KeyCombo(_profile.SaveModifier, _profile.SaveKey)], cancellationToken);

    public Task<PluginActionResult> GoToCellAsync(string cellReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return Task.FromResult(PluginActionResult.Fail("No cell reference was given."));
        }

        List<WorkflowAction> actions =
        [
            FocusApplication(),
            KeyCombo(_profile.GoToModifier, _profile.GoToKey),
            TypeText(cellReference),
            PressKey(Key.Enter),
        ];
        return RunAsync("excel-go-to-cell", actions, cancellationToken);
    }

    public Task<PluginActionResult> InsertAutoSumAsync(CancellationToken cancellationToken = default) =>
        RunAsync("excel-insert-autosum", [FocusApplication(), KeyCombo(_profile.AutoSumModifier, _profile.AutoSumKey)], cancellationToken);

    public Task<PluginActionResult> ToggleBoldAsync(CancellationToken cancellationToken = default) =>
        RunAsync("excel-toggle-bold", [FocusApplication(), KeyCombo(_profile.BoldModifier, _profile.BoldKey)], cancellationToken);

    public Task<PluginActionResult> FindAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(PluginActionResult.Fail("No search text was given."));
        }

        List<WorkflowAction> actions = [FocusApplication(), KeyCombo(_profile.FindModifier, _profile.FindKey), TypeText(text)];
        return RunAsync("excel-find", actions, cancellationToken);
    }
}
