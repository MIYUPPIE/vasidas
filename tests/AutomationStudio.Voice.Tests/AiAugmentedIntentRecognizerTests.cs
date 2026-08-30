using AutomationStudio.Abstractions;
using AutomationStudio.Ai.Intents;
using AutomationStudio.Voice.Intents;

namespace AutomationStudio.Voice.Tests;

/// <summary>
/// Design doc section 23's Tier-1/Tier-2 composition: deterministic recognition always runs first
/// (fast, free, exact), falling back to AI classification only when it comes up empty — and never
/// falling back at all when no AI interpreter is configured, which is exactly section 23's "the
/// application must remain functional without a local LLM."
/// </summary>
public class AiAugmentedIntentRecognizerTests
{
    [Fact]
    public async Task RecognizeAsync_DeterministicHit_ShortCircuits_NeverCallsTheAiEngine()
    {
        var deterministicIntent = new Intent("NEXT_SLIDE", new Dictionary<string, string>());
        var deterministic = new FakeIntentRecognizer { NextIntent = deterministicIntent };
        var engine = new FakeLocalAiEngine();
        var recognizer = new AiAugmentedIntentRecognizer(deterministic, new AiIntentInterpreter(engine));

        var result = await recognizer.RecognizeAsync("next slide please");

        Assert.Same(deterministicIntent, result);
        Assert.Equal(0, engine.CallCount);
    }

    [Fact]
    public async Task RecognizeAsync_DeterministicMiss_FallsThroughToAi()
    {
        var deterministic = new FakeIntentRecognizer { NextIntent = null };
        var engine = new FakeLocalAiEngine { NextCompletion = """{"intent": "PAUSE_MEDIA", "parameters": {}}""" };
        var recognizer = new AiAugmentedIntentRecognizer(deterministic, new AiIntentInterpreter(engine));

        var result = await recognizer.RecognizeAsync("would you mind pausing that for me");

        Assert.NotNull(result);
        Assert.Equal("PAUSE_MEDIA", result!.Name);
        Assert.Equal(1, deterministic.CallCount);
        Assert.Equal(1, engine.CallCount);
    }

    [Fact]
    public async Task RecognizeAsync_DeterministicMiss_NoAiInterpreterConfigured_ReturnsNull_StaysFunctionalWithoutAnLlm()
    {
        var deterministic = new FakeIntentRecognizer { NextIntent = null };
        var recognizer = new AiAugmentedIntentRecognizer(deterministic);

        var result = await recognizer.RecognizeAsync("would you mind pausing that for me");

        Assert.Null(result);
    }

    [Fact]
    public async Task RecognizeAsync_DeterministicMiss_AiAlsoMisses_ReturnsNull()
    {
        var deterministic = new FakeIntentRecognizer { NextIntent = null };
        var engine = new FakeLocalAiEngine { NextCompletion = """{"intent": "UNKNOWN"}""" };
        var recognizer = new AiAugmentedIntentRecognizer(deterministic, new AiIntentInterpreter(engine));

        var result = await recognizer.RecognizeAsync("the weather is nice today");

        Assert.Null(result);
    }
}
