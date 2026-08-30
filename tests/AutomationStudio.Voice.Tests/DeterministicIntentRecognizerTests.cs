using AutomationStudio.Abstractions;
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

    [Theory]
    [InlineData("Start streaming", "START_STREAM")]
    [InlineData("Start stream", "START_STREAM")]
    [InlineData("Stop streaming", "STOP_STREAM")]
    [InlineData("Start presentation", "START_PRESENTATION")]
    [InlineData("Start the presentation", "START_PRESENTATION")]
    [InlineData("End presentation", "END_PRESENTATION")]
    [InlineData("Stop", "STOP_MEDIA")]
    [InlineData("Stop media", "STOP_MEDIA")]
    [InlineData("Next track", "NEXT_TRACK")]
    [InlineData("Previous track", "PREVIOUS_TRACK")]
    [InlineData("Back track", "PREVIOUS_TRACK")]
    [InlineData("Mute", "TOGGLE_MUTE")]
    [InlineData("Unmute", "TOGGLE_MUTE")]
    [InlineData("Fullscreen", "TOGGLE_FULLSCREEN")]
    [InlineData("Full screen", "TOGGLE_FULLSCREEN")]
    [InlineData("Toggle fullscreen", "TOGGLE_FULLSCREEN")]
    [InlineData("New tab", "NEW_TAB")]
    [InlineData("Close tab", "CLOSE_TAB")]
    [InlineData("Reload", "RELOAD_PAGE")]
    [InlineData("Reload the page", "RELOAD_PAGE")]
    [InlineData("Previous page", "GO_BACK")]
    [InlineData("Back page", "GO_BACK")]
    [InlineData("Go forward", "GO_FORWARD")]
    [InlineData("Next page", "GO_FORWARD")]
    [InlineData("New folder", "NEW_FOLDER")]
    [InlineData("Create a new folder", "NEW_FOLDER")]
    [InlineData("Copy", "COPY_FILE")]
    [InlineData("Cut", "CUT_FILE")]
    [InlineData("Paste", "PASTE_FILE")]
    [InlineData("Delete", "DELETE_FILE")]
    public async Task Recognize_PhaseNineCommands_MatchTheExpectedIntentName(string spoken, string expectedIntent)
    {
        var intent = await _recognizer.RecognizeAsync(spoken);

        Assert.Equal(expectedIntent, intent?.Name);
    }

    [Fact]
    public async Task Recognize_SwitchScene_CapturesTheSceneName()
    {
        var intent = await _recognizer.RecognizeAsync("Switch to scene Main Camera");

        Assert.Equal("SWITCH_SCENE", intent?.Name);
        Assert.Equal("main camera", intent?.Parameter("scene"));
    }

    [Fact]
    public async Task Recognize_GoToSlide_CapturesTheSlideNumber()
    {
        var intent = await _recognizer.RecognizeAsync("Go to slide 5");

        Assert.Equal("GO_TO_SLIDE", intent?.Name);
        Assert.Equal("5", intent?.Parameter("slideNumber"));
    }

    [Fact]
    public async Task Recognize_OpenUrl_RequiresADomainLikeTargetToDistinguishFromOpenApplication()
    {
        var url = await _recognizer.RecognizeAsync("Open example.com");
        Assert.Equal("OPEN_URL", url?.Name);
        Assert.Equal("example.com", url?.Parameter("url"));

        var app = await _recognizer.RecognizeAsync("Open obs");
        Assert.Equal("OPEN_APPLICATION", app?.Name);
        Assert.Equal("obs", app?.Parameter("application"));
    }

    [Fact]
    public async Task Recognize_FindOnPage_CapturesTheSearchText()
    {
        var intent = await _recognizer.RecognizeAsync("Find recording settings");

        Assert.Equal("FIND_ON_PAGE", intent?.Name);
        Assert.Equal("recording settings", intent?.Parameter("text"));
    }

    [Fact]
    public async Task Recognize_RenameFile_CapturesTheNewName()
    {
        var intent = await _recognizer.RecognizeAsync("Rename to vacation photos");

        Assert.Equal("RENAME_FILE", intent?.Name);
        Assert.Equal("vacation photos", intent?.Parameter("name"));
    }

    [Fact]
    public async Task Recognize_SearchFiles_CapturesTheQuery()
    {
        var intent = await _recognizer.RecognizeAsync("Search files for budget");

        Assert.Equal("SEARCH_FILES", intent?.Name);
        Assert.Equal("budget", intent?.Parameter("query"));
    }

    [Theory]
    [InlineData("Save the word document", "word")]
    [InlineData("Save word", "word")]
    [InlineData("Save the excel file", "excel")]
    [InlineData("Save excel", "excel")]
    public async Task Recognize_SaveDocument_CapturesWhichApplication(string spoken, string expectedApplication)
    {
        var intent = await _recognizer.RecognizeAsync(spoken);

        Assert.Equal("SAVE_DOCUMENT", intent?.Name);
        Assert.Equal(expectedApplication, intent?.Parameter("application"));
    }

    [Fact]
    public async Task Recognize_GoBackAloneIsStillPreviousSlide_NotGoBackPage()
    {
        // "go back" bare is already claimed by PreviousSlidePattern (an established Phase 3 command)
        // — Chrome's back-navigation intent deliberately only matches "previous page"/"back page" so
        // it never shadows that existing behavior.
        Assert.Equal("PREVIOUS_SLIDE", (await _recognizer.RecognizeAsync("go back"))?.Name);
    }

    [Fact]
    public async Task Recognize_PlayAndPauseAlreadyCoverVlcsToggleVerbs_NoSeparateVlcPatternNeeded()
    {
        // PLAY_MEDIA/PAUSE_MEDIA already existed (Phase 3) and are what VLC's plugin binds to (VLC's
        // own Space key toggles both) — confirms Phase 9 didn't need to add new patterns for these.
        Assert.Equal("PLAY_MEDIA", (await _recognizer.RecognizeAsync("play"))?.Name);
        Assert.Equal("PAUSE_MEDIA", (await _recognizer.RecognizeAsync("pause"))?.Name);
    }

    [Fact]
    public async Task Recognize_TeachNewCommandBare_ProducesTeachCommandWithNoName()
    {
        var intent = await _recognizer.RecognizeAsync("Teach new command");

        Assert.Equal("TEACH_COMMAND", intent?.Name);
        Assert.Null(intent?.Parameter("name"));
    }

    [Fact]
    public async Task Recognize_TeachNewCommandWithInlineName_CapturesTheName()
    {
        var intent = await _recognizer.RecognizeAsync("Teach new command start church service");

        Assert.Equal("TEACH_COMMAND", intent?.Name);
        Assert.Equal("start church service", intent?.Parameter("name"));
    }

    [Theory]
    [InlineData("Stop teaching")]
    [InlineData("Done teaching")]
    [InlineData("Finished teaching")]
    public async Task Recognize_StopTeachingPhrasings_ProduceStopTeaching(string spoken)
    {
        Assert.Equal("STOP_TEACHING", (await _recognizer.RecognizeAsync(spoken))?.Name);
    }

    [Fact]
    public async Task Recognize_StopTeachingIsDistinctFromStopRecording()
    {
        Assert.Equal("STOP_TEACHING", (await _recognizer.RecognizeAsync("stop teaching"))?.Name);
        Assert.Equal("STOP_RECORDING", (await _recognizer.RecognizeAsync("stop recording"))?.Name);
    }
}

/// <summary>Separate fixture: these need a recognizer constructed with an <see cref="ICommandRegistry"/>, unlike every test above.</summary>
public class DeterministicIntentRecognizerWithCommandRegistryTests
{
    private sealed class FakeCommandRegistry : ICommandRegistry
    {
        public Dictionary<string, string> WorkflowIdsByNormalizedText { get; } = [];

        public Task<string?> ResolveWorkflowIdAsync(string normalizedText, CancellationToken cancellationToken = default) =>
            Task.FromResult(WorkflowIdsByNormalizedText.TryGetValue(normalizedText, out var id) ? id : null);
    }

    [Fact]
    public async Task Recognize_ATaughtCommandsPhrase_ProducesRunWorkflowWithItsId()
    {
        var registry = new FakeCommandRegistry();
        registry.WorkflowIdsByNormalizedText["start church service"] = "StartChurchService";
        var recognizer = new DeterministicIntentRecognizer(new KjvBibleService(), registry);

        var intent = await recognizer.RecognizeAsync("Start church service");

        Assert.Equal("RUN_WORKFLOW", intent?.Name);
        Assert.Equal("StartChurchService", intent?.Parameter("workflowId"));
    }

    [Fact]
    public async Task Recognize_ATaughtCommandThatShadowsABuiltInPhrase_TakesPriorityOverTheBuiltIn()
    {
        // If the user re-taught "clear the screen" to mean something custom, saying it should run
        // what they taught, not silently fall through to the built-in CLEAR_SCREEN pattern.
        var registry = new FakeCommandRegistry();
        registry.WorkflowIdsByNormalizedText["clear the screen"] = "MyCustomClearScreen";
        var recognizer = new DeterministicIntentRecognizer(new KjvBibleService(), registry);

        var intent = await recognizer.RecognizeAsync("Clear the screen");

        Assert.Equal("RUN_WORKFLOW", intent?.Name);
        Assert.Equal("MyCustomClearScreen", intent?.Parameter("workflowId"));
    }

    [Fact]
    public async Task Recognize_UnregisteredPhraseWithARegistryPresent_FallsThroughToNormalRecognition()
    {
        var registry = new FakeCommandRegistry();
        var recognizer = new DeterministicIntentRecognizer(new KjvBibleService(), registry);

        var intent = await recognizer.RecognizeAsync("Stop recording");

        Assert.Equal("STOP_RECORDING", intent?.Name);
    }
}

/// <summary>
/// Separate fixture: these need a recognizer constructed with an <see cref="IApplicationNameResolver"/>
/// — regression tests for a real, live-found bug (Phase 10/11's own testing): without a resolver,
/// OpenApplicationPattern's catch-all (`^(open|launch|start)\s+(?<app>.+)$`) claims ANY "start X"
/// phrase, including DAS.md section 4.3's own Tier-3 worked example ("Start Sunday service"), which
/// then fails at task-planning time instead of ever reaching AI classification/workflow generation.
/// </summary>
public class DeterministicIntentRecognizerWithApplicationResolverTests
{
    private sealed class FakeApplicationNameResolver : IApplicationNameResolver
    {
        public HashSet<string> KnownNames { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsKnown(string name) => KnownNames.Contains(name);
    }

    [Fact]
    public async Task Recognize_OpenApplicationWithAKnownName_StillProducesOpenApplication()
    {
        var resolver = new FakeApplicationNameResolver();
        resolver.KnownNames.Add("obs");
        var recognizer = new DeterministicIntentRecognizer(new KjvBibleService(), applicationNameResolver: resolver);

        var intent = await recognizer.RecognizeAsync("Open OBS");

        Assert.Equal("OPEN_APPLICATION", intent?.Name);
        Assert.Equal("obs", intent?.Parameter("application"));
    }

    [Fact]
    public async Task Recognize_OpenApplicationWithAnUnknownName_FallsThroughToNull_InsteadOfAGuessedOpenApplication()
    {
        var resolver = new FakeApplicationNameResolver(); // knows nothing
        var recognizer = new DeterministicIntentRecognizer(new KjvBibleService(), applicationNameResolver: resolver);

        var intent = await recognizer.RecognizeAsync("Start Sunday service");

        // Not OPEN_APPLICATION (that would fail at task-planning time with "no known launch path")
        // and not any other built-in intent either — null here is what lets VoiceAssistant's Tier 2/3
        // (AI classification, then dynamic workflow generation) actually get a turn at this phrase.
        Assert.Null(intent);
    }

    [Fact]
    public async Task Recognize_OpenApplicationWithNoResolverConfigured_PreservesTheOldAlwaysMatchBehavior()
    {
        // Backward compatibility: a caller that doesn't wire up a resolver (the default, and every
        // existing test in this file) must see the exact old behavior — OPEN_APPLICATION claims the
        // phrase regardless of whether the name means anything.
        var recognizer = new DeterministicIntentRecognizer(new KjvBibleService());

        var intent = await recognizer.RecognizeAsync("Start Sunday service");

        Assert.Equal("OPEN_APPLICATION", intent?.Name);
        Assert.Equal("sunday service", intent?.Parameter("application"));
    }

    [Fact]
    public async Task Recognize_CloseApplicationAndDisplaySong_StillWorkAfterOpenApplicationDeclines()
    {
        // OpenApplicationPattern declining to match must not stop the Rules loop from trying the
        // rules after it (CloseApplicationPattern, DisplaySongPattern) — confirms the loop's
        // "keep trying on a null build result" change didn't break sibling rules.
        var resolver = new FakeApplicationNameResolver();
        var recognizer = new DeterministicIntentRecognizer(new KjvBibleService(), applicationNameResolver: resolver);

        Assert.Equal("CLOSE_APPLICATION", (await recognizer.RecognizeAsync("Close OBS"))?.Name);
        Assert.Equal("DISPLAY_SONG", (await recognizer.RecognizeAsync("Show Amazing Grace"))?.Name);
    }
}
