using AutomationStudio.Voice.Intents;

namespace AutomationStudio.Voice.Tests;

public class DeterministicIntentRecognizerTests
{
    private readonly DeterministicIntentRecognizer _recognizer = new();

    [Theory]
    [InlineData("Start recording", "START_RECORDING")]
    [InlineData("Begin recording", "START_RECORDING")]
    [InlineData("Stop recording", "STOP_RECORDING")]
    [InlineData("Next slide", "NEXT_SLIDE")]
    [InlineData("Next", "NEXT_SLIDE")]
    [InlineData("Previous slide", "PREVIOUS_SLIDE")]
    [InlineData("Go back", "PREVIOUS_SLIDE")]
    [InlineData("Pause", "PAUSE_MEDIA")]
    [InlineData("Pause it", "PAUSE_MEDIA")]
    [InlineData("Resume", "PLAY_MEDIA")]
    [InlineData("Play", "PLAY_MEDIA")]
    [InlineData("Continue", "PLAY_MEDIA")]
    public void Recognize_MatchesTheExpectedIntentName_ForPhaseThreeCommands(string spoken, string expectedIntent)
    {
        var intent = _recognizer.Recognize(spoken);

        Assert.NotNull(intent);
        Assert.Equal(expectedIntent, intent!.Name);
    }

    [Fact]
    public void Recognize_OpenApplication_CapturesTheApplicationName()
    {
        var intent = _recognizer.Recognize("Open OBS");

        Assert.NotNull(intent);
        Assert.Equal("OPEN_APPLICATION", intent!.Name);
        Assert.Equal("obs", intent.Parameter("application"));
    }

    [Fact]
    public void Recognize_LaunchIsASynonymForOpen()
    {
        var intent = _recognizer.Recognize("Launch OBS");

        Assert.Equal("OPEN_APPLICATION", intent?.Name);
        Assert.Equal("obs", intent?.Parameter("application"));
    }

    [Fact]
    public void Recognize_CloseApplication_CapturesTheApplicationName()
    {
        var intent = _recognizer.Recognize("Close OBS");

        Assert.Equal("CLOSE_APPLICATION", intent?.Name);
        Assert.Equal("obs", intent?.Parameter("application"));
    }

    [Fact]
    public void Recognize_StartRecording_IsNotMisclassifiedAsOpenApplication()
    {
        // "start" alone would match the OpenApplication pattern's alternation; ordering must ensure
        // the more specific START_RECORDING rule wins for this exact phrase.
        var intent = _recognizer.Recognize("start recording");

        Assert.Equal("START_RECORDING", intent?.Name);
    }

    [Fact]
    public void Recognize_StartFollowedByAnApplicationName_OpensThatApplication()
    {
        var intent = _recognizer.Recognize("start obs");

        Assert.Equal("OPEN_APPLICATION", intent?.Name);
        Assert.Equal("obs", intent?.Parameter("application"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("what time is it")]
    [InlineData("banana")]
    public void Recognize_ForUnrecognizedText_ReturnsNull(string spoken)
    {
        Assert.Null(_recognizer.Recognize(spoken));
    }

    [Fact]
    public void Recognize_IsCaseAndPunctuationInsensitive()
    {
        var intent = _recognizer.Recognize("STOP RECORDING!!");

        Assert.Equal("STOP_RECORDING", intent?.Name);
    }
}
