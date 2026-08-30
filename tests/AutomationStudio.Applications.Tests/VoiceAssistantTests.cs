using AutomationStudio.Ai.Planning;
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
            new FakeDesktopController(), new FakeScreenshotService(), new FakeWindowManager(), new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
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

    /// <summary>
    /// Design doc section 23's Tier-3 fallback: when nothing else understood a multi-step request,
    /// a configured <see cref="AiWorkflowGenerator"/> gets a last shot at it before the assistant
    /// gives up. A fresh <see cref="VoiceAssistant"/> (not <see cref="_assistant"/>) is built per
    /// test here so the generator can be configured differently per case.
    /// </summary>
    private (VoiceAssistant Assistant, FakeSpeechToText SpeechToText, FakeLocalAiEngine AiEngine, FakeWindowManager WindowManager) BuildAssistantWithAiFallback()
    {
        var speechToText = new FakeSpeechToText();
        var bibleService = new KjvBibleService();
        var contextManager = new ContextManager();
        var workflowRepository = new FakeWorkflowRepository();
        var commandRegistry = new WorkflowCommandRegistry(workflowRepository);
        var recognizer = new DeterministicIntentRecognizer(bibleService, commandRegistry);
        var resolver = new IntentContextResolver(contextManager, bibleService);
        var listener = new VoiceCommandListener(new FakeMicrophoneCapture(), new FakeVoiceActivityDetector(), speechToText, recognizer, resolver);

        var windowManager = new FakeWindowManager();
        var engine = new WorkflowExecutionEngine(
            new FakeDesktopController(), new FakeScreenshotService(), windowManager, new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        var planner = new TaskPlanner(_plugin, engine, workflowRepository: workflowRepository);

        var aiEngine = new FakeLocalAiEngine();
        var generator = new AiWorkflowGenerator(aiEngine, engine);
        var assistant = new VoiceAssistant(listener, planner, workflowGenerator: generator);

        return (assistant, speechToText, aiEngine, windowManager);
    }

    [Fact]
    public async Task ListenAndExecuteAsync_NothingMatchesButAiGeneratesAWorkflow_RunsIt()
    {
        var (assistant, speechToText, aiEngine, windowManager) = BuildAssistantWithAiFallback();
        // Deliberately NOT "start Sunday service" — DeterministicIntentRecognizer's OpenApplication
        // catch-all (`^(open|launch|start)\s+(?<app>.+)$`) would claim that phrase at Tier 1 before
        // AI ever gets a turn, exactly the ambiguity DAS.md section 4.3's own worked example glosses
        // over. This phrase avoids every Tier-1 trigger verb so it genuinely falls through to Tier 3.
        speechToText.NextTranscription = "please get everything ready for the Sunday service";
        aiEngine.NextCompletion = """
            {"id": "start-service", "name": "Start Sunday service", "version": 1, "actions": [
                {"type": "FocusApplication", "processName": "obs"}
            ]}
            """;

        var result = await assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        Assert.NotNull(result);
        Assert.True(result!.Success, result.Message);
        Assert.Contains("FocusByProcess(obs)", windowManager.Calls);
    }

    [Fact]
    public async Task ListenAndExecuteAsync_NothingMatchesAndAiAlsoProducesNothingUsable_ReturnsNull()
    {
        var (assistant, speechToText, aiEngine, _) = BuildAssistantWithAiFallback();
        speechToText.NextTranscription = "the weather is nice today";
        aiEngine.NextCompletion = "I'm not sure what you mean.";

        var result = await assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        Assert.Null(result);
    }

    [Fact]
    public async Task ListenAndExecuteAsync_ForUnrecognizedSpeech_NoAiConfigured_StillReturnsNull()
    {
        // _assistant was built with no workflowGenerator — confirms the existing "must remain
        // functional without a local LLM" contract (design doc section 23) is unchanged.
        _speechToText.NextTranscription = "please get everything ready for the Sunday service";

        var result = await _assistant.ListenAndExecuteAsync(TimeSpan.FromSeconds(1));

        Assert.Null(result);
        Assert.Empty(_plugin.Calls);
    }

    [Fact]
    public async Task ListenAndExecuteWithTranscriptAsync_ForARecognizedCommand_ReturnsBothTheTranscriptAndTheResult()
    {
        _speechToText.NextTranscription = "Clear the screen";

        var outcome = await _assistant.ListenAndExecuteWithTranscriptAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("Clear the screen", outcome.Text);
        Assert.True(outcome.Result?.Success);
        Assert.Contains("ClearScreen", _plugin.Calls);
    }

    [Fact]
    public async Task ListenAndExecuteWithTranscriptAsync_ForUnrecognizedSpeech_StillReturnsTheTranscript_WithANullResult()
    {
        _speechToText.NextTranscription = "the weather is nice today";

        var outcome = await _assistant.ListenAndExecuteWithTranscriptAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("the weather is nice today", outcome.Text);
        Assert.Null(outcome.Result);
    }

    [Fact]
    public async Task ListenAndExecuteWithTranscriptAsync_NothingSaidInTime_ReturnsNullTextAndNullResult()
    {
        _speechToText.NextTranscription = "";
        // FakeVoiceActivityDetector's default NextUtterance is non-null, so give it nothing to transcribe by
        // clearing what WhisperSpeechToText would have returned — an empty transcription still reaches
        // ListenWithTextAsync as a non-null "" string via TranscribeAsync, which is a distinct case from
        // "nothing said within the timeout" (VAD timed out, no utterance at all). Exercise the latter here.
        var listener = new VoiceCommandListener(
            new FakeMicrophoneCapture(),
            new FakeVoiceActivityDetector { NextUtterance = null },
            _speechToText,
            new DeterministicIntentRecognizer(new KjvBibleService()),
            new IntentContextResolver(new ContextManager(), new KjvBibleService()));
        var engine = new WorkflowExecutionEngine(
            new FakeDesktopController(), new FakeScreenshotService(), new FakeWindowManager(), new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        var assistant = new VoiceAssistant(listener, new TaskPlanner(_plugin, engine));

        var outcome = await assistant.ListenAndExecuteWithTranscriptAsync(TimeSpan.FromSeconds(1));

        Assert.Null(outcome.Text);
        Assert.Null(outcome.Result);
    }
}
