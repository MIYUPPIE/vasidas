using AutomationStudio.Abstractions;
using AutomationStudio.Core.Vision;
using Microsoft.Extensions.Logging;

namespace AutomationStudio.Core.Execution;

/// <summary>
/// Everything an action needs to run, handed down by the engine. Actions only ever see the
/// platform through <see cref="IDesktopController"/> / <see cref="IScreenshotService"/> /
/// <see cref="IWindowManager"/> / <see cref="IElementFinder"/> / <see cref="IOcrEngine"/> /
/// <see cref="TargetResolver"/> — never a concrete Windows/Linux type (section 42).
/// </summary>
public sealed class ActionExecutionContext
{
    public required IDesktopController DesktopController { get; init; }
    public required IScreenshotService ScreenshotService { get; init; }
    public required IWindowManager WindowManager { get; init; }
    public required IElementFinder ElementFinder { get; init; }
    public required IOcrEngine OcrEngine { get; init; }
    public required TargetResolver TargetResolver { get; init; }
    public required ILogger Logger { get; init; }
    public required CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Set by <see cref="Actions.TargetResolutionHelper"/> when target resolution actually triggers
    /// design doc section 26's self-healing signal — a side channel back to the engine (which builds
    /// the final <see cref="ActionExecutionResult"/> after <c>ExecuteAsync</c> returns) rather than a
    /// new return value, since every action's <c>ExecuteAsync</c> already has a fixed <see cref="Task"/>
    /// signature. Safe as a mutable property specifically because the engine constructs a fresh
    /// context per action attempt (never reused across actions or retries).
    /// </summary>
    public HealingProposal? HealingProposal { get; set; }
}
