using AutomationStudio.Bible.Services;
using AutomationStudio.Voice.Intents;

namespace AutomationStudio.Voice.Tests;

public class DeterministicIntentRecognizerTests
{
    private readonly DeterministicIntentRecognizer _recognizer = new(new KjvBibleService());

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
    public async Task Recognize_MatchesTheExpectedIntentName_ForPhaseThreeCommands(string spoken, string expectedIntent)
    {
        var intent = await _recognizer.RecognizeAsync(spoken);

        Assert.NotNull(intent);
        Assert.Equal(expectedIntent, intent!.Name);
    }

    [Fact]
    public async Task Recognize_OpenApplication_CapturesTheApplicationName()
    {
        var intent = await _recognizer.RecognizeAsync("Open OBS");

        Assert.NotNull(intent);
        Assert.Equal("OPEN_APPLICATION", intent!.Name);
        Assert.Equal("obs", intent.Parameter("application"));
    }

    [Fact]
    public async Task Recognize_LaunchIsASynonymForOpen()
    {
        var intent = await _recognizer.RecognizeAsync("Launch OBS");

        Assert.Equal("OPEN_APPLICATION", intent?.Name);
        Assert.Equal("obs", intent?.Parameter("application"));
    }

    [Fact]
    public async Task Recognize_CloseApplication_CapturesTheApplicationName()
    {
        var intent = await _recognizer.RecognizeAsync("Close OBS");

        Assert.Equal("CLOSE_APPLICATION", intent?.Name);
        Assert.Equal("obs", intent?.Parameter("application"));
    }

    [Fact]
    public async Task Recognize_StartRecording_IsNotMisclassifiedAsOpenApplication()
    {
        // "start" alone would match the OpenApplication pattern's alternation; ordering must ensure
        // the more specific START_RECORDING rule wins for this exact phrase.
        var intent = await _recognizer.RecognizeAsync("start recording");

        Assert.Equal("START_RECORDING", intent?.Name);
    }

    [Fact]
    public async Task Recognize_StartFollowedByAnApplicationName_OpensThatApplication()
    {
        var intent = await _recognizer.RecognizeAsync("start obs");

        Assert.Equal("OPEN_APPLICATION", intent?.Name);
        Assert.Equal("obs", intent?.Parameter("application"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("what time is it")]
    [InlineData("banana")]
    public async Task Recognize_ForUnrecognizedText_ReturnsNull(string spoken)
    {
        Assert.Null(await _recognizer.RecognizeAsync(spoken));
    }

    [Fact]
    public async Task Recognize_IsCaseAndPunctuationInsensitive()
    {
        var intent = await _recognizer.RecognizeAsync("STOP RECORDING!!");

        Assert.Equal("STOP_RECORDING", intent?.Name);
    }

    [Theory]
    [InlineData("John 3:16")]
    [InlineData("Show John 3:16")]
    [InlineData("Display John chapter 3 verse 16")]
    [InlineData("John three sixteen")]
    public async Task Recognize_BibleVersePhrasings_ProduceDisplayBibleVerseWithParsedFields(string spoken)
    {
        var intent = await _recognizer.RecognizeAsync(spoken);

        Assert.NotNull(intent);
        Assert.Equal("DISPLAY_BIBLE_VERSE", intent!.Name);
        Assert.Equal("John", intent.Parameter("book"));
        Assert.Equal("3", intent.Parameter("chapter"));
        Assert.Equal("16", intent.Parameter("startVerse"));
    }

    [Fact]
    public async Task Recognize_BibleVerseRange_CapturesBothEndpoints()
    {
        var intent = await _recognizer.RecognizeAsync("Show Psalm 91 verses 1 to 5");

        Assert.Equal("DISPLAY_BIBLE_VERSE", intent?.Name);
        Assert.Equal("Psalms", intent!.Parameter("book"));
        Assert.Equal("1", intent.Parameter("startVerse"));
        Assert.Equal("5", intent.Parameter("endVerse"));
    }

    [Fact]
    public async Task Recognize_WholeChapterReference_ValidatesAgainstTheRealChapter()
    {
        var intent = await _recognizer.RecognizeAsync("Show Psalm 23");

        Assert.Equal("DISPLAY_BIBLE_VERSE", intent?.Name);
        Assert.Equal("Psalms", intent!.Parameter("book"));
        Assert.Equal("23", intent.Parameter("chapter"));
        Assert.Null(intent.Parameter("startVerse"));
    }

    [Fact]
    public async Task Recognize_ForANonexistentVerse_FallsBackToDisplaySong_RatherThanABogusBibleIntent()
    {
        // John only has 21 chapters — this must not emit DISPLAY_BIBLE_VERSE for scripture that
        // doesn't exist (validated against the real dataset, not just parsed grammatically).
        // Falling back to "treat it as a song title" is a deliberate, reasonable choice here (the
        // system can't be certain which the speaker meant), not a null result.
        var intent = await _recognizer.RecognizeAsync("Show John chapter 99");

        Assert.Equal("DISPLAY_SONG", intent?.Name);
    }

    [Fact]
    public async Task Recognize_ForANonexistentVerseNumber_FallsBackRatherThanABogusBibleIntent()
    {
        // John 3 only has 36 verses.
        var intent = await _recognizer.RecognizeAsync("Show John 3:999");

        Assert.NotEqual("DISPLAY_BIBLE_VERSE", intent?.Name);
    }

    [Fact]
    public async Task Recognize_ShowWithANonBibleReference_ProducesDisplaySong()
    {
        var intent = await _recognizer.RecognizeAsync("Show Amazing Grace");

        Assert.Equal("DISPLAY_SONG", intent?.Name);
        Assert.Equal("amazing grace", intent?.Parameter("title"));
    }

    [Theory]
    [InlineData("Next verse", "NEXT_VERSE")]
    [InlineData("Previous verse", "PREVIOUS_VERSE")]
    [InlineData("Next song", "NEXT_SONG")]
    [InlineData("Previous song", "PREVIOUS_SONG")]
    [InlineData("Clear the screen", "CLEAR_SCREEN")]
    [InlineData("Clear screen", "CLEAR_SCREEN")]
    [InlineData("Hide the presentation", "HIDE_OUTPUT")]
    [InlineData("Hide output", "HIDE_OUTPUT")]
    public async Task Recognize_VideoPsalmCommands_MatchTheExpectedIntentName(string spoken, string expectedIntent)
    {
        var intent = await _recognizer.RecognizeAsync(spoken);

        Assert.Equal(expectedIntent, intent?.Name);
    }

    [Fact]
    public async Task Recognize_NextVerseIsDistinctFromNextSlide()
    {
        Assert.Equal("NEXT_VERSE", (await _recognizer.RecognizeAsync("next verse"))?.Name);
        Assert.Equal("NEXT_SLIDE", (await _recognizer.RecognizeAsync("next slide"))?.Name);
        Assert.Equal("NEXT_SLIDE", (await _recognizer.RecognizeAsync("next"))?.Name);
    }
}
