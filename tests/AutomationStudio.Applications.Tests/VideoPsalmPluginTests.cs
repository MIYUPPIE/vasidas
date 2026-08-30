using AutomationStudio.Abstractions;
using AutomationStudio.Applications.VideoPsalm;
using AutomationStudio.Bible.Services;
using AutomationStudio.Core.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Applications.Tests;

/// <summary>Exercises VideoPsalmPlugin against the real Bible dataset (no fake — a fake would hide a bug like resolving a nonexistent verse) but a fake desktop/window layer, since VideoPsalm itself isn't installed here.</summary>
public class VideoPsalmPluginTests
{
    private readonly FakeDesktopController _desktop = new();
    private readonly FakeWindowManager _windowManager = new();
    private readonly IBibleService _bibleService = new KjvBibleService();
    private readonly VideoPsalmPlugin _plugin;

    public VideoPsalmPluginTests()
    {
        var engine = new WorkflowExecutionEngine(
            _desktop, new FakeScreenshotService(), _windowManager, new FakeElementFinder(), new FakeOcrEngine(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        _plugin = new VideoPsalmPlugin(engine, _bibleService);
    }

    [Fact]
    public async Task DisplayBibleVerseAsync_ForARealVerse_FocusesSearchesAndTypesTheReference()
    {
        var reference = new BibleReference("John", 3, 16, 16);

        var result = await _plugin.DisplayBibleVerseAsync(reference);

        Assert.True(result.Success, result.Message);
        Assert.Contains("FocusByProcess(VideoPsalm)", _windowManager.Calls);
        Assert.Contains($"KeyCombination({Key.LeftAlt}+{Key.B})", _desktop.Calls); // Bibles panel
        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.F})", _desktop.Calls); // focus search
        Assert.Contains("TypeText(John 3:16)", _desktop.Calls);
        Assert.Contains($"PressKey({Key.Enter})", _desktop.Calls);
    }

    [Fact]
    public async Task DisplayBibleVerseAsync_ForAVerseThatDoesNotExist_FailsWithoutTouchingTheDesktop()
    {
        // John only has 21 chapters — this validates against real scripture *before* automating,
        // per DAS.md section 16, rather than sending a bogus reference to VideoPsalm's search box.
        var reference = new BibleReference("John", 99, 1, 1);

        var result = await _plugin.DisplayBibleVerseAsync(reference);

        Assert.False(result.Success);
        Assert.Contains("does not exist", result.Message);
        Assert.Empty(_desktop.Calls);
        Assert.Empty(_windowManager.Calls);
    }

    [Fact]
    public async Task DisplayBibleVerseAsync_ForAWholeChapterReference_ValidatesAgainstItsFirstVerse()
    {
        var reference = new BibleReference("Psalms", 23, null, null);

        var result = await _plugin.DisplayBibleVerseAsync(reference);

        Assert.True(result.Success, result.Message);
        Assert.Contains("TypeText(Psalms 23)", _desktop.Calls);
    }

    [Fact]
    public async Task NextVerseAsync_FocusesThenPressesTheConfiguredKey()
    {
        var result = await _plugin.NextVerseAsync();

        Assert.True(result.Success);
        Assert.Contains("FocusByProcess(VideoPsalm)", _windowManager.Calls);
        Assert.Contains($"PressKey({Key.Down})", _desktop.Calls);
    }

    [Fact]
    public async Task PreviousVerseAsync_PressesTheConfiguredKey()
    {
        await _plugin.PreviousVerseAsync();

        Assert.Contains($"PressKey({Key.Up})", _desktop.Calls);
    }

    [Fact]
    public async Task DisplaySongAsync_SwitchesToSongbooksPanelAndTypesTheTitle()
    {
        var result = await _plugin.DisplaySongAsync("Amazing Grace");

        Assert.True(result.Success, result.Message);
        Assert.Contains($"KeyCombination({Key.LeftAlt}+{Key.S})", _desktop.Calls); // Songbooks panel
        Assert.Contains("TypeText(Amazing Grace)", _desktop.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DisplaySongAsync_WithNoTitle_FailsWithoutTouchingTheDesktop(string title)
    {
        var result = await _plugin.DisplaySongAsync(title);

        Assert.False(result.Success);
        Assert.Empty(_desktop.Calls);
    }

    [Fact]
    public async Task NextSongAsync_SendsTheAgendaNextShortcut()
    {
        await _plugin.NextSongAsync();

        Assert.Contains($"KeyCombination({Key.LeftShift}+{Key.Equals})", _desktop.Calls);
    }

    [Fact]
    public async Task PreviousSongAsync_SendsTheAgendaPreviousShortcut()
    {
        await _plugin.PreviousSongAsync();

        Assert.Contains($"PressKey({Key.Minus})", _desktop.Calls);
    }

    [Fact]
    public async Task ClearScreenAsync_SendsTheBlackScreenShortcut()
    {
        await _plugin.ClearScreenAsync();

        Assert.Contains($"PressKey({Key.B})", _desktop.Calls);
    }

    [Fact]
    public async Task HideOutputAsync_SendsTheExitPresentationShortcut()
    {
        await _plugin.HideOutputAsync();

        Assert.Contains($"PressKey({Key.Escape})", _desktop.Calls);
    }

    [Fact]
    public async Task WhenVideoPsalmCannotBeFocused_ReturnsFailure()
    {
        _windowManager.FocusByProcessResult = false;

        var result = await _plugin.NextVerseAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
    }
}
