using System.Diagnostics;
using AutomationStudio.Ai.Engines;
using AutomationStudio.Ai.Intents;
using AutomationStudio.Ai.Planning;
using AutomationStudio.Core.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Ai.Tests;

/// <summary>
/// Loads the real Qwen2.5-1.5B-Instruct GGUF model once for the whole test class (unlike
/// WhisperSpeechToTextTests' per-test <c>new WhisperSpeechToText(...)</c> — Whisper's model load is
/// cheap, a ~1GB GGUF load on the CPU backend is not) and reuses the same <see cref="LlamaSharpEngine"/>
/// across every test, which is also exactly the production usage pattern: one engine instance, many
/// independent one-shot completions.
/// </summary>
public sealed class LlamaSharpEngineFixture : IAsyncLifetime
{
    public static string ModelPath => Path.Combine(Path.GetTempPath(), "automationstudio-tests", "qwen2.5-1.5b-instruct-q4_k_m.gguf");

    public LlamaSharpEngine Engine { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await LocalAiModelProvisioning.EnsureModelAsync(ModelPath);
        Engine = new LlamaSharpEngine(ModelPath);
    }

    public Task DisposeAsync()
    {
        Engine.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Exercises the real LLamaSharp/llama.cpp pipeline end to end — confirmed live before writing
/// <see cref="LlamaSharpEngine"/> that it loads a real GGUF model on this dev machine's CPU backend
/// and produces well-formed, correctly classified JSON. The model is downloaded once to a shared
/// temp path and reused across the whole run, matching WhisperSpeechToTextTests' own precedent for
/// a real, uncommitted model file (never committed to the repo — CLAUDE.md bans committing model
/// weights).
/// </summary>
public class LlamaSharpEngineTests(LlamaSharpEngineFixture fixture) : IClassFixture<LlamaSharpEngineFixture>
{
    [Fact]
    public async Task EnsureModelAsync_DownloadsOnce_AndReusesTheCachedFileOnLaterCalls()
    {
        Assert.True(File.Exists(LlamaSharpEngineFixture.ModelPath));
        var sizeAfterFirstEnsure = new FileInfo(LlamaSharpEngineFixture.ModelPath).Length;

        // Should be a no-op file-existence check now, not a second ~1GB download.
        var stopwatch = Stopwatch.StartNew();
        await LocalAiModelProvisioning.EnsureModelAsync(LlamaSharpEngineFixture.ModelPath);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"expected a cached no-op, took {stopwatch.ElapsedMilliseconds}ms");
        Assert.Equal(sizeAfterFirstEnsure, new FileInfo(LlamaSharpEngineFixture.ModelPath).Length);
    }

    [Fact]
    public async Task CompleteAsync_ViaAiIntentInterpreter_ClassifiesAnUnambiguousCommand_Correctly()
    {
        var interpreter = new AiIntentInterpreter(fixture.Engine);

        var intent = await interpreter.InterpretAsync("would you mind pausing that for me");

        Assert.NotNull(intent);
        Assert.Equal("PAUSE_MEDIA", intent!.Name);
    }

    [Fact]
    public async Task CompleteAsync_CalledTwiceInARowOnTheSameEngine_BothCallsProduceNonEmptyOutput()
    {
        // Regression test for the finding documented on LlamaSharpEngine itself: InteractiveExecutor
        // retains conversation state across calls on a shared context, which silently produced EMPTY
        // output on the second of two independent one-shot completions when this was first tried
        // live. Every call this project makes really is independent (classify one command, then a
        // different one, never a back-and-forth), so this pins the StatelessExecutor fix down for
        // real rather than leaving it as an unverified doc comment.
        var first = await fixture.Engine.CompleteAsync("Reply with exactly one word: FIRST", "go");
        var second = await fixture.Engine.CompleteAsync("Reply with exactly one word: SECOND", "go");

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.False(string.IsNullOrWhiteSpace(second));
    }

    [Fact]
    public async Task TryGenerateAsync_ViaAiWorkflowGenerator_ForAMultiStepDescription_ProducesAUsableWorkflow()
    {
        var executionEngine = new WorkflowExecutionEngine(
            new FakeDesktopController(), new FakeScreenshotService(), new FakeWindowManager(), new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        var generator = new AiWorkflowGenerator(fixture.Engine, executionEngine);

        var workflow = await generator.TryGenerateAsync("open OBS and start streaming");

        Assert.NotNull(workflow);
        Assert.NotEmpty(workflow!.Actions);
    }
}
