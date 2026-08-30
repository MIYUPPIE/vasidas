using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Core.Tests.Fakes;
using AutomationStudio.Core.Workflows;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Core.Tests.Execution;

public class WorkflowExecutionEngineTests
{
    private static WorkflowExecutionEngine BuildEngine(
        FakeDesktopController desktop, FakeScreenshotService? screenshots = null, string? failureScreenshotDirectory = null,
        FakeElementFinder? elementFinder = null, FakeOcrEngine? ocrEngine = null) =>
        new(desktop, screenshots ?? new FakeScreenshotService(), new FakeWindowManager(), elementFinder ?? new FakeElementFinder(),
            ocrEngine ?? new FakeOcrEngine(), new FakeTemplateMatcher(), NullLogger<WorkflowExecutionEngine>.Instance, failureScreenshotDirectory);

    [Fact]
    public async Task ExecuteAsync_RunsActionsInOrder()
    {
        var desktop = new FakeDesktopController();
        var engine = BuildEngine(desktop);
        var workflow = new Workflow
        {
            Id = "wf1",
            Name = "test",
            Actions =
            [
                new OpenApplicationAction { Path = "obs" },
                new WaitAction { Milliseconds = 1 },
                new ClickAction { X = 1, Y = 2 },
                new TypeTextAction { Text = "hi" },
            ]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Equal(4, result.ActionResults.Count);
        Assert.All(result.ActionResults, r => Assert.Equal(ActionStatus.Success, r.Status));
        Assert.Equal(
            ["OpenApplication(obs,)", "Click(Left,1,2)", "TypeText(hi)"],
            desktop.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_DisabledAction_IsSkippedNotExecuted()
    {
        var desktop = new FakeDesktopController();
        var engine = BuildEngine(desktop);
        var workflow = new Workflow
        {
            Id = "wf1",
            Name = "test",
            Actions = [new ClickAction { X = 1, Y = 1, Enabled = false }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Equal(ActionStatus.Skipped, result.ActionResults[0].Status);
        Assert.Empty(desktop.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_FailureWithStopOnFailure_StopsWorkflowAndDoesNotRunLaterActions()
    {
        var desktop = new FakeDesktopController
        {
            OnClick = _ => throw new InvalidOperationException("simulated failure")
        };
        var engine = BuildEngine(desktop);
        var workflow = new Workflow
        {
            Id = "wf1",
            Name = "test",
            StopOnFailure = true,
            Actions =
            [
                new ClickAction { X = 1, Y = 1 },
                new TypeTextAction { Text = "should not run" },
            ]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Failed, result.Status);
        Assert.Single(result.ActionResults);
        Assert.Equal(ActionStatus.Failed, result.ActionResults[0].Status);
        Assert.Contains("simulated failure", result.ActionResults[0].Message);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public async Task ExecuteAsync_FailureWithoutStopOnFailure_ContinuesToLaterActions()
    {
        var desktop = new FakeDesktopController
        {
            OnClick = _ => throw new InvalidOperationException("simulated failure")
        };
        var engine = BuildEngine(desktop);
        var workflow = new Workflow
        {
            Id = "wf1",
            Name = "test",
            StopOnFailure = false,
            Actions =
            [
                new ClickAction { X = 1, Y = 1 },
                new TypeTextAction { Text = "runs anyway" },
            ]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(2, result.ActionResults.Count);
        Assert.Equal(ActionStatus.Failed, result.ActionResults[0].Status);
        Assert.Equal(ActionStatus.Success, result.ActionResults[1].Status);
        Assert.Contains("TypeText(runs anyway)", desktop.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesUpToRetryCountThenSucceeds()
    {
        var attempts = 0;
        var desktop = new FakeDesktopController
        {
            OnClick = _ =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new InvalidOperationException($"attempt {attempts} fails");
                }

                return Task.CompletedTask;
            }
        };
        var engine = BuildEngine(desktop);
        var workflow = new Workflow
        {
            Id = "wf1",
            Name = "test",
            Actions = [new ClickAction { X = 1, Y = 1, RetryCount = 5 }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Equal(3, result.ActionResults[0].AttemptCount);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_ExhaustsRetriesThenFails()
    {
        var attempts = 0;
        var desktop = new FakeDesktopController
        {
            OnClick = _ =>
            {
                attempts++;
                throw new InvalidOperationException("always fails");
            }
        };
        var engine = BuildEngine(desktop);
        var workflow = new Workflow
        {
            Id = "wf1",
            Name = "test",
            Actions = [new ClickAction { X = 1, Y = 1, RetryCount = 2 }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Failed, result.Status);
        Assert.Equal(3, attempts); // initial attempt + 2 retries
        Assert.Equal(3, result.ActionResults[0].AttemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_ActionExceedingTimeout_IsRecordedAsTimeout()
    {
        var desktop = new FakeDesktopController
        {
            OnClick = async ct => await Task.Delay(TimeSpan.FromSeconds(5), ct)
        };
        var engine = BuildEngine(desktop);
        var workflow = new Workflow
        {
            Id = "wf1",
            Name = "test",
            Actions = [new ClickAction { X = 1, Y = 1, TimeoutMilliseconds = 50 }]
        };

        var result = await engine.ExecuteAsync(workflow);

        Assert.Equal(ActionStatus.Failed, result.Status); // StopOnFailure defaults true; Timeout also stops
        Assert.Equal(ActionStatus.Timeout, result.ActionResults[0].Status);
    }

    [Fact]
    public async Task ExecuteAsync_FailureCapturesScreenshotWhenDirectoryConfigured()
    {
        var desktop = new FakeDesktopController
        {
            OnClick = _ => throw new InvalidOperationException("boom")
        };
        var screenshots = new FakeScreenshotService();
        var tempDir = Path.Combine(Path.GetTempPath(), $"automationstudio-failures-{Guid.NewGuid():n}");
        var engine = BuildEngine(desktop, screenshots, tempDir);
        var workflow = new Workflow
        {
            Id = "wf1",
            Name = "test",
            Actions = [new ClickAction { X = 1, Y = 1 }]
        };

        try
        {
            var result = await engine.ExecuteAsync(workflow);

            Assert.Equal(ActionStatus.Failed, result.ActionResults[0].Status);
            Assert.NotNull(result.ActionResults[0].ScreenshotPath);
            Assert.True(File.Exists(result.ActionResults[0].ScreenshotPath));
            Assert.Equal(1, screenshots.CaptureCount);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_Stop_CancelsRemainingActions()
    {
        using var runControl = new WorkflowRunControl();
        var desktop = new FakeDesktopController
        {
            OnClick = _ =>
            {
                runControl.Stop();
                return Task.CompletedTask;
            }
        };
        var engine = BuildEngine(desktop);
        var workflow = new Workflow
        {
            Id = "wf1",
            Name = "test",
            Actions =
            [
                new ClickAction { X = 1, Y = 1 },
                new TypeTextAction { Text = "should never run" },
            ]
        };

        var result = await engine.ExecuteAsync(workflow, runControl);

        Assert.Equal(ActionStatus.Cancelled, result.Status);
        Assert.DoesNotContain("TypeText(should never run)", desktop.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_Pause_BlocksUntilResumed()
    {
        using var runControl = new WorkflowRunControl();
        var desktop = new FakeDesktopController();
        var engine = BuildEngine(desktop);
        var workflow = new Workflow
        {
            Id = "wf1",
            Name = "test",
            Actions = [new ClickAction { X = 1, Y = 1 }, new ClickAction { X = 2, Y = 2 }]
        };

        runControl.Pause();
        var runTask = engine.ExecuteAsync(workflow, runControl);

        await Task.Delay(50);
        Assert.Empty(desktop.Calls); // still paused before the first action

        runControl.Resume();
        var result = await runTask;

        Assert.Equal(ActionStatus.Success, result.Status);
        Assert.Equal(2, desktop.Calls.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsProgress_OncePerAction_InOrder()
    {
        var desktop = new FakeDesktopController();
        var engine = BuildEngine(desktop);
        var workflow = new Workflow
        {
            Id = "wf1",
            Name = "test",
            Actions =
            [
                new ClickAction { X = 1, Y = 1 },
                new TypeTextAction { Text = "hi", Enabled = false },
                new WaitAction { Milliseconds = 1 },
            ]
        };

        var reported = new List<ActionExecutionResult>();
        var progress = new Progress<ActionExecutionResult>(reported.Add);

        var result = await engine.ExecuteAsync(workflow, progress: progress);

        // Progress<T> marshals callbacks via the captured SynchronizationContext (post/async), so
        // give any pending callback a moment to land before asserting on `reported`.
        await Task.Delay(50);

        Assert.Equal(result.ActionResults.Count, reported.Count);
        Assert.Equal(result.ActionResults.Select(r => r.ActionId), reported.Select(r => r.ActionId));
        Assert.Contains(reported, r => r.Status == ActionStatus.Skipped);
    }
}
