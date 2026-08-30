using AutomationStudio.Abstractions;
using AutomationStudio.Applications.Plugins;
using AutomationStudio.Core.Execution;

namespace AutomationStudio.Applications.PowerPoint;

/// <summary>Profile-driven PowerPoint automation (DAS.md section 14) — every capability sends <see cref="PowerPointProfile"/>'s documented shortcut and runs it through the same generic <see cref="WorkflowExecutionEngine"/> everything else uses.</summary>
public sealed class PowerPointPlugin(WorkflowExecutionEngine engine, PowerPointProfile? profile = null)
    : ApplicationPluginBase(engine, (profile ?? PowerPointProfile.Default).ProcessName), IPowerPointPlugin
{
    private readonly PowerPointProfile _profile = profile ?? PowerPointProfile.Default;

    public Task<PluginActionResult> NextSlideAsync(CancellationToken cancellationToken = default) =>
        RunAsync("powerpoint-next-slide", [FocusApplication(), PressKey(_profile.NextSlideKey)], cancellationToken);

    public Task<PluginActionResult> PreviousSlideAsync(CancellationToken cancellationToken = default) =>
        RunAsync("powerpoint-previous-slide", [FocusApplication(), PressKey(_profile.PreviousSlideKey)], cancellationToken);

    /// <summary>Only meaningful during a slide show — see <see cref="PowerPointProfile"/>'s doc comment for the documented behavior this relies on.</summary>
    public Task<PluginActionResult> GoToSlideAsync(int slideNumber, CancellationToken cancellationToken = default)
    {
        if (slideNumber <= 0)
        {
            return Task.FromResult(PluginActionResult.Fail("Slide number must be positive."));
        }

        return RunAsync(
            "powerpoint-go-to-slide",
            [FocusApplication(), TypeText(slideNumber.ToString()), PressKey(Key.Enter)],
            cancellationToken);
    }

    public Task<PluginActionResult> StartPresentationAsync(CancellationToken cancellationToken = default) =>
        RunAsync("powerpoint-start-presentation", [FocusApplication(), PressKey(_profile.StartPresentationKey)], cancellationToken);

    public Task<PluginActionResult> EndPresentationAsync(CancellationToken cancellationToken = default) =>
        RunAsync("powerpoint-end-presentation", [FocusApplication(), PressKey(_profile.EndPresentationKey)], cancellationToken);
}
