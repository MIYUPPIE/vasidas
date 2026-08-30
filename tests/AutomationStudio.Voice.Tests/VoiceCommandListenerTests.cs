using AutomationStudio.Bible.Services;
using AutomationStudio.Voice.Context;
using AutomationStudio.Voice.Intents;

namespace AutomationStudio.Voice.Tests;

/// <summary>End-to-end orchestration: transcribed text → recognized intent → context-resolved intent, with fakes standing in only for the microphone/VAD/STT layers (audio hardware and model inference are already covered live elsewhere).</summary>
public class VoiceCommandListenerTests
{
    private readonly FakeSpeechToText _speechToText = new();
    private readonly ContextManager _contextManager = new();
    private readonly VoiceCommandListener _listener;

    public VoiceCommandListenerTests()
    {
        var bibleService = new KjvBibleService();
        var recognizer = new DeterministicIntentRecognizer(bibleService);
        var resolver = new IntentContextResolver(_contextManager, bibleService);
        _listener = new VoiceCommandListener(new FakeMicrophoneCapture(), new FakeVoiceActivityDetector(), _speechToText, recognizer, resolver);
    }

    [Fact]
    public async Task ListenAsync_TranscribedRecognizedCommand_ReturnsTheResolvedIntent()
    {
        _speechToText.NextTranscription = "Show John 3:16";

        var intent = await _listener.ListenAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("DISPLAY_BIBLE_VERSE", intent?.Name);
        Assert.Equal("16", intent?.Parameter("startVerse"));
    }

    [Fact]
    public async Task ListenAsync_TwoCallsInSequence_TheSecondResolvesNextVerseAgainstTheFirst()
    {
        _speechToText.NextTranscription = "Show John 3:16";
        await _listener.ListenAsync(TimeSpan.FromSeconds(1));

        _speechToText.NextTranscription = "Next verse";
        var second = await _listener.ListenAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("DISPLAY_BIBLE_VERSE", second?.Name);
        Assert.Equal("17", second?.Parameter("startVerse"));
    }

    [Fact]
    public async Task ListenAsync_WhenNothingWasSaid_ReturnsNull()
    {
        var vad = new FakeVoiceActivityDetector { NextUtterance = null };
        var listener = new VoiceCommandListener(new FakeMicrophoneCapture(), vad, _speechToText, new DeterministicIntentRecognizer(new KjvBibleService()), new IntentContextResolver(_contextManager, new KjvBibleService()));

        var intent = await listener.ListenAsync(TimeSpan.FromSeconds(1));

        Assert.Null(intent);
    }

    [Fact]
    public async Task ListenAsync_ForUnrecognizedSpeech_ReturnsNull()
    {
        _speechToText.NextTranscription = "the weather is nice today";

        var intent = await _listener.ListenAsync(TimeSpan.FromSeconds(1));

        Assert.Null(intent);
    }
}
