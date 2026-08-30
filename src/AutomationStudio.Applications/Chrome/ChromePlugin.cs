using AutomationStudio.Abstractions;
using AutomationStudio.Applications.Plugins;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;

namespace AutomationStudio.Applications.Chrome;

/// <summary>Profile-driven Chrome automation (DAS.md section 14) — every capability sends <see cref="ChromeProfile"/>'s documented shortcut and runs it through the same generic <see cref="WorkflowExecutionEngine"/> everything else uses.</summary>
public sealed class ChromePlugin(WorkflowExecutionEngine engine, ChromeProfile? profile = null)
    : ApplicationPluginBase(engine, (profile ?? ChromeProfile.Default).ProcessName), IChromePlugin
{
    private readonly ChromeProfile _profile = profile ?? ChromeProfile.Default;

    public Task<PluginActionResult> NewTabAsync(CancellationToken cancellationToken = default) =>
        RunAsync("chrome-new-tab", [FocusApplication(), KeyCombo(_profile.NewTabModifier, _profile.NewTabKey)], cancellationToken);

    public Task<PluginActionResult> CloseTabAsync(CancellationToken cancellationToken = default) =>
        RunAsync("chrome-close-tab", [FocusApplication(), KeyCombo(_profile.CloseTabModifier, _profile.CloseTabKey)], cancellationToken);

    public Task<PluginActionResult> ReloadAsync(CancellationToken cancellationToken = default) =>
        RunAsync("chrome-reload", [FocusApplication(), KeyCombo(_profile.ReloadModifier, _profile.ReloadKey)], cancellationToken);

    public Task<PluginActionResult> GoBackAsync(CancellationToken cancellationToken = default) =>
        RunAsync("chrome-go-back", [FocusApplication(), KeyCombo(_profile.BackModifier, _profile.BackKey)], cancellationToken);

    public Task<PluginActionResult> GoForwardAsync(CancellationToken cancellationToken = default) =>
        RunAsync("chrome-go-forward", [FocusApplication(), KeyCombo(_profile.ForwardModifier, _profile.ForwardKey)], cancellationToken);

    public Task<PluginActionResult> OpenUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult(PluginActionResult.Fail("No URL was given."));
        }

        List<WorkflowAction> actions =
        [
            FocusApplication(),
            KeyCombo(_profile.AddressBarModifier, _profile.AddressBarKey),
            TypeText(url),
            PressKey(Key.Enter),
        ];
        return RunAsync("chrome-open-url", actions, cancellationToken);
    }

    public Task<PluginActionResult> FindOnPageAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(PluginActionResult.Fail("No search text was given."));
        }

        List<WorkflowAction> actions =
        [
            FocusApplication(),
            KeyCombo(_profile.FindModifier, _profile.FindKey),
            TypeText(text),
        ];
        return RunAsync("chrome-find-on-page", actions, cancellationToken);
    }
}
