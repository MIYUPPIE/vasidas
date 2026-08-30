using AutomationStudio.Applications.Planning;
using AutomationStudio.Bible.Services;
using AutomationStudio.Core.Execution;
using AutomationStudio.Voice;
using AutomationStudio.Voice.Context;
using AutomationStudio.Voice.Intents;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Applications.Tests;

/// <summary>
/// The culmination of DAS.md sections 5-10 exercised end to end: a spoken command produces an
/// executed result. This is the first point in the whole project where that's true — every earlier
/// phase deliberately stopped at "here is the recognized/resolved Intent."
/// </summary>
public class VoiceAssistantTests
{
    private readonly FakeSpeechToText _speechToText = new();
    private readonly FakeVideoPsalmPlugin _plugin = new();
    private readonly VoiceAssistant _assistant;

    public VoiceAssistantTests()
    {
        var bibleService = new KjvBibleService();
        var contextManager = new ContextManager();
        var recognizer = new DeterministicIntentRecognizer(bibleService);
        var resolver = new IntentContextResolver(contextManager, bibleService);
        var listener = new VoiceCommandListener(new FakeMicrophoneCapture(), new FakeVoiceActivityDetector(), _speechToText, recognizer, resolver);

        var engine = new WorkflowExecutionEngine(
            new FakeDesktopController(), new FakeScreenshotService(), new FakeWindowManager(), new FakeElementFinder(), new FakeOcrEngine(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        var planner = new TaskPlanner(_plugin, engine);

        _assistant = new VoiceAssistant(listener, planner);
    }

    [Fact]
    public async Task ListenAndExecuteAsync_ForARealBibleVerseCommand_ActuallyCallsThePlugin()
    {
        _speechToText.NextTranscription = "Show John 3:16";

        var result = await _assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        Assert.NotNull(result);
        Assert.True(result!.Success, result.Message);
        Assert.Contains("DisplayBibleVerse(John 3:16)", _plugin.Calls);
    }

    [Fact]
    public async Task ListenAndExecuteAsync_NextVerseAfterAnExplicitVerse_ExecutesTheResolvedReference()
    {
        // DAS.md section 8's own example, carried all the way through to actual execution.
        _speechToText.NextTranscription = "Show John 3:16";
        await _assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        _speechToText.NextTranscription = "Next verse";
        var result = await _assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        Assert.True(result?.Success);
        Assert.Contains("DisplayBibleVerse(John 3:17)", _plugin.Calls);
    }

    [Fact]
    public async Task ListenAndExecuteAsync_ForUnrecognizedSpeech_ReturnsNull_AndNeverCallsThePlugin()
    {
        _speechToText.NextTranscription = "the weather is nice today";

        var result = await _assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        Assert.Null(result);
        Assert.Empty(_plugin.Calls);
    }

    [Fact]
    public async Task ListenAndExecuteAsync_ClearScreenCommand_ExecutesIt()
    {
        _speechToText.NextTranscription = "Clear the screen";

        var result = await _assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        Assert.True(result?.Success);
        Assert.Contains("ClearScreen", _plugin.Calls);
    }
}
