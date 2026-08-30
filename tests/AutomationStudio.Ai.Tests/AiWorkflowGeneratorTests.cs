using AutomationStudio.Ai.Planning;
using AutomationStudio.Core.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Ai.Tests;

/// <summary>
/// Design doc section 23's "Advanced task planning" / dynamic workflow generation tier, and the
/// full end-to-end path <see cref="ITaskPlanner"/>'s own doc comment forward-references. Every
/// generated workflow is round-tripped through the SAME <c>WorkflowSerializer</c>/
/// <c>WorkflowExecutionEngine</c> every taught or hand-authored workflow uses — nothing here is a
/// separate, less-validated execution path.
/// </summary>
public class AiWorkflowGeneratorTests
{
    private const string ValidWorkflowJson = """
        {"id": "start-service", "name": "Start Sunday service", "version": 1, "actions": [
            {"type": "FocusApplication", "processName": "obs"},
            {"type": "Wait", "milliseconds": 500},
            {"type": "ClickTarget", "target": {"text": "Start Streaming", "ocrText": "Start Streaming"}}
        ]}
        """;

    private static (FakeLocalAiEngine Engine, WorkflowExecutionEngine ExecutionEngine, FakeWindowManager WindowManager, FakeElementFinder ElementFinder, AiWorkflowGenerator Generator) Build()
    {
        var engine = new FakeLocalAiEngine();
        var windowManager = new FakeWindowManager();
        var elementFinder = new FakeElementFinder();
        var executionEngine = new WorkflowExecutionEngine(
            new FakeDesktopController(), new FakeScreenshotService(), windowManager, elementFinder, new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        var generator = new AiWorkflowGenerator(engine, executionEngine);
        return (engine, executionEngine, windowManager, elementFinder, generator);
    }

    [Fact]
    public async Task TryGenerateAsync_EmptyDescription_ReturnsNullWithoutCallingTheEngine()
    {
        var (engine, _, _, _, generator) = Build();

        var workflow = await generator.TryGenerateAsync("   ");

        Assert.Null(workflow);
        Assert.Empty(engine.Calls);
    }

    [Fact]
    public async Task TryGenerateAsync_ValidWorkflowJson_ParsesTheWorkflow()
    {
        var (engine, _, _, _, generator) = Build();
        engine.NextCompletion = ValidWorkflowJson;

        var workflow = await generator.TryGenerateAsync("start Sunday service");

        Assert.NotNull(workflow);
        Assert.Equal(3, workflow!.Actions.Count);
    }

    [Fact]
    public async Task TryGenerateAsync_MarkdownFencedWorkflowJson_StillExtractsAndParses()
    {
        var (engine, _, _, _, generator) = Build();
        engine.NextCompletion = $"Here you go:\n```json\n{ValidWorkflowJson}\n```";

        var workflow = await generator.TryGenerateAsync("start Sunday service");

        Assert.NotNull(workflow);
    }

    [Fact]
    public async Task TryGenerateAsync_MalformedJson_FailsClosed()
    {
        var (engine, _, _, _, generator) = Build();
        engine.NextCompletion = "not json at all, sorry";

        Assert.Null(await generator.TryGenerateAsync("do something"));
    }

    [Fact]
    public async Task TryGenerateAsync_UnknownActionType_FailsClosed()
    {
        var (engine, _, _, _, generator) = Build();
        // "LaunchNukes" isn't a real action type — WorkflowSerializer.FromJson's
        // JsonUnknownDerivedTypeHandling.FailSerialization throws, and the generator must never
        // treat a hallucinated action type as something to try executing anyway.
        engine.NextCompletion = """{"id": "x", "name": "x", "version": 1, "actions": [{"type": "LaunchNukes"}]}""";

        Assert.Null(await generator.TryGenerateAsync("do something"));
    }

    [Fact]
    public async Task TryGenerateAsync_EmptyActionsArray_ReturnsNull()
    {
        var (engine, _, _, _, generator) = Build();
        engine.NextCompletion = """{"id": "x", "name": "x", "version": 1, "actions": []}""";

        Assert.Null(await generator.TryGenerateAsync("do something"));
    }

    [Fact]
    public async Task TryGenerateAsync_EngineThrows_DegradesToNull_MatchingSection23sOfflineRequirement()
    {
        var (engine, _, _, _, generator) = Build();
        engine.ThrowOnComplete = new InvalidOperationException("model not loaded");

        Assert.Null(await generator.TryGenerateAsync("do something"));
    }

    [Fact]
    public async Task TryGenerateAndRunAsync_NothingGenerated_ReturnsNull_NotAFailedResult()
    {
        // The caller (VoiceAssistant) treats null as "nothing understood," the same as an
        // unrecognized command — not as an attempted-and-failed action.
        var (engine, _, _, _, generator) = Build();
        engine.NextCompletion = "garbage";

        Assert.Null(await generator.TryGenerateAndRunAsync("do something"));
    }

    [Fact]
    public async Task TryGenerateAndRunAsync_ValidWorkflow_ActuallyExecutesEveryAction()
    {
        var (engine, _, windowManager, elementFinder, generator) = Build();
        engine.NextCompletion = ValidWorkflowJson;
        elementFinder.ElementsByText["Start Streaming"] = new AutomationStudio.Abstractions.DetectedElement
        {
            Bounds = new AutomationStudio.Abstractions.ScreenRect(10, 10, 100, 30),
        };

        var result = await generator.TryGenerateAndRunAsync("start Sunday service");

        Assert.NotNull(result);
        Assert.True(result!.Success, result.Message);
        Assert.Contains("FocusByProcess(obs)", windowManager.Calls);
        Assert.Contains("Invoke", elementFinder.Calls);
    }

    [Fact]
    public async Task TryGenerateAndRunAsync_ExecutionFails_ReturnsAFailedResult_WithTheEnginesReason()
    {
        var (engine, _, windowManager, _, generator) = Build();
        windowManager.FocusByProcessResult = false; // FocusApplication will fail to find the process.
        engine.NextCompletion = ValidWorkflowJson;

        var result = await generator.TryGenerateAndRunAsync("start Sunday service");

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }
}
