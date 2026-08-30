using AutomationStudio.Applications.Planning;
using AutomationStudio.Bible.Services;
using AutomationStudio.Core.Execution;
using AutomationStudio.Recorder;
using AutomationStudio.Storage.Repositories;
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
    private readonly FakeWorkflowRepository _workflowRepository = new();
    private readonly FakeInputRecorder _inputRecorder = new();
    private readonly FakeWindowManager _teachingWindowManager = new();
    private readonly TeachingCoordinator _teaching;
    private readonly VoiceAssistant _assistant;

    public VoiceAssistantTests()
    {
        var bibleService = new KjvBibleService();
        var contextManager = new ContextManager();
        var commandRegistry = new WorkflowCommandRegistry(_workflowRepository);
        var recognizer = new DeterministicIntentRecognizer(bibleService, commandRegistry);
        var resolver = new IntentContextResolver(contextManager, bibleService);
        var listener = new VoiceCommandListener(new FakeMicrophoneCapture(), new FakeVoiceActivityDetector(), _speechToText, recognizer, resolver);

        var engine = new WorkflowExecutionEngine(
            new FakeDesktopController(), new FakeScreenshotService(), new FakeWindowManager(), new FakeElementFinder(), new FakeOcrEngine(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        var planner = new TaskPlanner(_plugin, engine, workflowRepository: _workflowRepository);

        _teaching = new TeachingCoordinator(new TeachingSession(_inputRecorder, _teachingWindowManager), _workflowRepository);
        _assistant = new VoiceAssistant(listener, planner, _teaching);
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

    [Fact]
    public async Task ListenAndExecuteAsync_TeachNewCommandWithInlineName_StartsRecordingImmediately()
    {
        _speechToText.NextTranscription = "Teach new command start church service";

        var result = await _assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        Assert.True(result?.Success);
        Assert.True(_inputRecorder.Started);
        Assert.Equal(TeachingState.Recording, _teaching.State);
    }

    [Fact]
    public async Task ListenAndExecuteAsync_TeachNewCommandBare_AsksForACommandName_ThenTheNextUtteranceStartsRecording()
    {
        _speechToText.NextTranscription = "Teach new command";
        var prompted = await _assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        Assert.True(prompted?.Success);
        Assert.Equal(TeachingState.AwaitingCommandName, _teaching.State);
        Assert.False(_inputRecorder.Started); // not yet — the name hasn't been given.

        // "Start church service" would normally match OPEN_APPLICATION; while awaiting a command
        // name it must be taken as the raw name instead, never run through intent recognition.
        _speechToText.NextTranscription = "Start church service";
        var started = await _assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        Assert.True(started?.Success);
        Assert.True(_inputRecorder.Started);
        Assert.Equal(TeachingState.Recording, _teaching.State);
        Assert.Equal("Start church service", _teaching.PendingCommandName);
    }

    [Fact]
    public async Task ListenAndExecuteAsync_StopTeachingWhileRecording_FinishesAndPersistsTheWorkflow()
    {
        _speechToText.NextTranscription = "Teach new command start church service";
        await _assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        _speechToText.NextTranscription = "Stop teaching";
        var result = await _assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        Assert.True(result?.Success);
        Assert.Equal(TeachingState.Idle, _teaching.State);
        Assert.Contains("StartChurchService", _workflowRepository.SavedIds);
    }

    [Fact]
    public async Task ListenAndExecuteAsync_ATaughtCommandsPhrase_RunsTheSavedWorkflow()
    {
        _speechToText.NextTranscription = "Teach new command start church service";
        await _assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));
        _speechToText.NextTranscription = "Stop teaching";
        await _assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        _speechToText.NextTranscription = "Start church service";
        var result = await _assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        Assert.True(result?.Success, result?.Message);
    }
}
