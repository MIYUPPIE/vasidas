using AutomationStudio.Abstractions;
using Microsoft.Extensions.Logging;

namespace AutomationStudio.Core.Execution;

/// <summary>
/// Everything an action needs to run, handed down by the engine. Actions only ever see the
/// platform through <see cref="IDesktopController"/> / <see cref="IScreenshotService"/> /
/// <see cref="IWindowManager"/> — never a concrete Windows/Linux type (section 42).
/// </summary>
public sealed class ActionExecutionContext
{
    public required IDesktopController DesktopController { get; init; }
    public required IScreenshotService ScreenshotService { get; init; }
    public required IWindowManager WindowManager { get; init; }
    public required ILogger Logger { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}
