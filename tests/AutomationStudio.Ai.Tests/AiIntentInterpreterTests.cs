using AutomationStudio.Ai.Intents;

namespace AutomationStudio.Ai.Tests;

/// <summary>
/// Design doc section 23's "Local AI → Intent" classification tier. Every test drives a canned
/// completion through <see cref="AiIntentInterpreter.ParseResponse"/> directly (the parsing/
/// validation logic is what actually matters here — <see cref="FakeLocalAiEngine"/> never runs
/// real inference) plus a couple through the public <c>InterpretAsync</c> to confirm the engine
/// call and exception handling wire up correctly.
/// </summary>
public class AiIntentInterpreterTests
{
    [Fact]
    public void ParseResponse_ValidClassificationWithParameters_ReturnsTheIntent()
    {
        var intent = AiIntentInterpreter.ParseResponse("""{"intent": "SWITCH_SCENE", "parameters": {"scene": "Worship"}}""");

        Assert.NotNull(intent);
        Assert.Equal("SWITCH_SCENE", intent!.Name);
        Assert.Equal("Worship", intent.Parameter("scene"));
    }

    [Fact]
    public void ParseResponse_ValidClassificationWithNoRequiredParameters_ReturnsTheIntent()
    {
        var intent = AiIntentInterpreter.ParseResponse("""{"intent": "NEXT_SLIDE", "parameters": {}}""");

        Assert.NotNull(intent);
        Assert.Equal("NEXT_SLIDE", intent!.Name);
    }

    [Fact]
    public void ParseResponse_Unknown_ReturnsNull()
    {
        Assert.Null(AiIntentInterpreter.ParseResponse("""{"intent": "UNKNOWN"}"""));
    }

    [Fact]
    public void ParseResponse_HallucinatedIntentName_ReturnsNull()
    {
        // The model invented a name that isn't in IntentCatalog — TaskPlanner has no plan for it
        // anyway, so this must be rejected rather than dispatched.
        Assert.Null(AiIntentInterpreter.ParseResponse("""{"intent": "LAUNCH_THE_ROCKET", "parameters": {}}"""));
    }

    [Fact]
    public void ParseResponse_MissingRequiredParameter_FailsClosed()
    {
        // SWITCH_SCENE requires "scene" — a classification without it isn't usable.
        Assert.Null(AiIntentInterpreter.ParseResponse("""{"intent": "SWITCH_SCENE", "parameters": {}}"""));
    }

    [Fact]
    public void ParseResponse_MalformedJson_ReturnsNull()
    {
        Assert.Null(AiIntentInterpreter.ParseResponse("{intent: not valid json at all"));
    }

    [Fact]
    public void ParseResponse_MarkdownFencedJson_StillExtractsAndParses()
    {
        var intent = AiIntentInterpreter.ParseResponse("""
            Sure, here's the classification:
            ```json
            {"intent": "PAUSE_MEDIA", "parameters": {}}
            ```
            """);

        Assert.NotNull(intent);
        Assert.Equal("PAUSE_MEDIA", intent!.Name);
    }

    [Fact]
    public void ParseResponse_NumericParameterValue_IsCapturedAsString()
    {
        var intent = AiIntentInterpreter.ParseResponse("""{"intent": "GO_TO_SLIDE", "parameters": {"slideNumber": 7}}""");

        Assert.NotNull(intent);
        Assert.Equal("7", intent!.Parameter("slideNumber"));
    }

    [Fact]
    public void ParseResponse_UnknownParameterName_IsSilentlyDropped()
    {
        var intent = AiIntentInterpreter.ParseResponse("""{"intent": "NEXT_SLIDE", "parameters": {"extra": "stuff"}}""");

        Assert.NotNull(intent);
        Assert.Null(intent!.Parameter("extra"));
    }

    [Fact]
    public async Task InterpretAsync_EmptyText_ReturnsNullWithoutCallingTheEngine()
    {
        var engine = new FakeLocalAiEngine();
        var interpreter = new AiIntentInterpreter(engine);

        var result = await interpreter.InterpretAsync("   ");

        Assert.Null(result);
        Assert.Empty(engine.Calls);
    }

    [Fact]
    public async Task InterpretAsync_ValidCompletion_ReturnsTheParsedIntent()
    {
        var engine = new FakeLocalAiEngine { NextCompletion = """{"intent": "TOGGLE_MUTE", "parameters": {}}""" };
        var interpreter = new AiIntentInterpreter(engine);

        var result = await interpreter.InterpretAsync("would you mind muting that");

        Assert.NotNull(result);
        Assert.Equal("TOGGLE_MUTE", result!.Name);
        Assert.Single(engine.Calls);
    }

    [Fact]
    public async Task InterpretAsync_EngineThrows_DegradesToNull_MatchingSection23sOfflineRequirement()
    {
        var engine = new FakeLocalAiEngine { ThrowOnComplete = new InvalidOperationException("model not loaded") };
        var interpreter = new AiIntentInterpreter(engine);

        var result = await interpreter.InterpretAsync("anything");

        Assert.Null(result);
    }

    [Fact]
    public async Task InterpretAsync_EngineThrowsCancellation_PropagatesRatherThanSwallowing()
    {
        var engine = new FakeLocalAiEngine { ThrowOnComplete = new OperationCanceledException() };
        var interpreter = new AiIntentInterpreter(engine);

        await Assert.ThrowsAsync<OperationCanceledException>(() => interpreter.InterpretAsync("anything"));
    }
}
