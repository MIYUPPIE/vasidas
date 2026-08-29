using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Workflows;
using Microsoft.Extensions.Logging;

namespace AutomationStudio.Core.Execution;

/// <summary>
/// Runs a <see cref="Workflow"/> action by action, sequentially (section 7). Owns every
/// cross-cutting concern so individual actions can stay dumb: retry/timeout (section 15),
/// stop-on-failure (section 16), pause/cancel (section 28), and screenshot-on-failure (section 26).
/// </summary>
public sealed class WorkflowExecutionEngine
{
    private readonly IDesktopController _desktopController;
    private readonly IScreenshotService _screenshotService;
    private readonly IWindowManager _windowManager;
    private readonly IElementFinder _elementFinder;
    private readonly IOcrEngine _ocrEngine;
    private readonly ILogger<WorkflowExecutionEngine> _logger;
    private readonly string? _failureScreenshotDirectory;

    public WorkflowExecutionEngine(
        IDesktopController desktopController,
        IScreenshotService screenshotService,
        IWindowManager windowManager,
        IElementFinder elementFinder,
        IOcrEngine ocrEngine,
        ILogger<WorkflowExecutionEngine> logger,
        string? failureScreenshotDirectory = null)
    {
        _desktopController = desktopController;
        _screenshotService = screenshotService;
        _windowManager = windowManager;
        _elementFinder = elementFinder;
        _ocrEngine = ocrEngine;
        _logger = logger;
        _failureScreenshotDirectory = failureScreenshotDirectory;
    }

    public async Task<WorkflowExecutionResult> ExecuteAsync(
        Workflow workflow,
        WorkflowRunControl? runControl = null,
        IProgress<ActionExecutionResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var linkedCts = runControl is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runControl.Token);
        var overallToken = linkedCts.Token;

        var startedAtUtc = DateTimeOffset.UtcNow;
        var results = new List<ActionExecutionResult>(workflow.Actions.Count);

        void Record(ActionExecutionResult result)
        {
            results.Add(result);
            progress?.Report(result);
        }

        _logger.LogInformation("Workflow {WorkflowId} ({WorkflowName}) started", workflow.Id, workflow.Name);

        var overallStatus = ActionStatus.Success;
        string? failureReason = null;

        foreach (var action in workflow.Actions)
        {
            try
            {
                if (runControl is not null)
                {
                    await runControl.WaitIfPausedAsync(overallToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                Record(ActionExecutionResult.Cancelled(action.Id, action.ActionType, DateTimeOffset.UtcNow, TimeSpan.Zero, 0));
                overallStatus = ActionStatus.Cancelled;
                failureReason = "Workflow was stopped while paused.";
                break;
            }

            if (!action.Enabled)
            {
                Record(ActionExecutionResult.Skipped(action.Id, action.ActionType, DateTimeOffset.UtcNow));
                _logger.LogInformation("Action {ActionType} ({ActionId}) skipped (disabled)", action.ActionType, action.Id);
                continue;
            }

            var result = await ExecuteWithRetryAsync(action, overallToken).ConfigureAwait(false);
            Record(result);

            switch (result.Status)
            {
                case ActionStatus.Success:
                    continue;

                case ActionStatus.Cancelled:
                    overallStatus = ActionStatus.Cancelled;
                    failureReason = "Workflow was stopped.";
                    goto done;

                case ActionStatus.Failed or ActionStatus.Timeout:
                    _logger.LogError(
                        "Action {ActionType} ({ActionId}) ended with {Status}: {Message}",
                        action.ActionType, action.Id, result.Status, result.Message);

                    if (workflow.StopOnFailure)
                    {
                        overallStatus = ActionStatus.Failed;
                        failureReason = $"Action '{action.ActionType}' ({action.Id}) {result.Status.ToString().ToLowerInvariant()}: {result.Message}";
                        goto done;
                    }

                    break;
            }
        }

        done:
        var completedAtUtc = DateTimeOffset.UtcNow;
        _logger.LogInformation(
            "Workflow {WorkflowId} completed with status {Status} in {DurationMs}ms",
            workflow.Id, overallStatus, (completedAtUtc - startedAtUtc).TotalMilliseconds);

        return new WorkflowExecutionResult
        {
            WorkflowId = workflow.Id,
            Status = overallStatus,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            ActionResults = results,
            FailureReason = failureReason
        };
    }

    private async Task<ActionExecutionResult> ExecuteWithRetryAsync(WorkflowAction action, CancellationToken overallToken)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var attempt = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (true)
        {
            attempt++;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(overallToken);
            timeoutCts.CancelAfter(action.TimeoutMilliseconds);

            var context = new ActionExecutionContext
            {
                DesktopController = _desktopController,
                ScreenshotService = _screenshotService,
                WindowManager = _windowManager,
                ElementFinder = _elementFinder,
                OcrEngine = _ocrEngine,
                Logger = _logger,
                CancellationToken = timeoutCts.Token
            };

            try
            {
                // Checked inside the try (rather than before it) so a Stop() that landed between
                // actions is caught by the handler below and turned into a Cancelled result,
                // instead of throwing out of ExecuteAsync uncaught.
                overallToken.ThrowIfCancellationRequested();

                await action.ExecuteAsync(context).ConfigureAwait(false);
                return ActionExecutionResult.Success(action.Id, action.ActionType, startedAtUtc, stopwatch.Elapsed, attempt);
            }
            catch (OperationCanceledException) when (overallToken.IsCancellationRequested)
            {
                return ActionExecutionResult.Cancelled(action.Id, action.ActionType, startedAtUtc, stopwatch.Elapsed, attempt);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                if (attempt <= action.RetryCount)
                {
                    _logger.LogWarning(
                        "Action {ActionType} ({ActionId}) timed out after {TimeoutMs}ms, retrying (attempt {Attempt}/{MaxAttempts})",
                        action.ActionType, action.Id, action.TimeoutMilliseconds, attempt, action.RetryCount + 1);
                    continue;
                }

                var screenshotPath = await TryCaptureFailureScreenshotAsync(action, overallToken).ConfigureAwait(false);
                return ActionExecutionResult.TimedOut(action.Id, action.ActionType, startedAtUtc, stopwatch.Elapsed, attempt, screenshotPath);
            }
            catch (Exception ex)
            {
                if (attempt <= action.RetryCount)
                {
                    _logger.LogWarning(
                        ex,
                        "Action {ActionType} ({ActionId}) failed, retrying (attempt {Attempt}/{MaxAttempts})",
                        action.ActionType, action.Id, attempt, action.RetryCount + 1);
                    continue;
                }

                var screenshotPath = await TryCaptureFailureScreenshotAsync(action, overallToken).ConfigureAwait(false);
                return ActionExecutionResult.Failed(action.Id, action.ActionType, startedAtUtc, stopwatch.Elapsed, attempt, ex.Message, screenshotPath);
            }
        }
    }

    private async Task<string?> TryCaptureFailureScreenshotAsync(WorkflowAction action, CancellationToken cancellationToken)
    {
        if (_failureScreenshotDirectory is null || cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        try
        {
            var screenshot = await _screenshotService.CaptureScreenAsync(cancellationToken).ConfigureAwait(false);
            var path = Path.Combine(
                _failureScreenshotDirectory,
                $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}_{action.ActionType}_{action.Id}.png");
            await screenshot.SaveAsync(path, cancellationToken).ConfigureAwait(false);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture failure screenshot for action {ActionId}", action.Id);
            return null;
        }
    }
}
