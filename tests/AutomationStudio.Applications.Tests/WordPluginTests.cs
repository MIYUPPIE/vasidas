using AutomationStudio.Abstractions;
using AutomationStudio.Applications.Word;
using AutomationStudio.Core.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Applications.Tests;

public class WordPluginTests
{
    private readonly FakeDesktopController _desktop = new();
    private readonly FakeWindowManager _windowManager = new();
    private readonly WordPlugin _plugin;

    public WordPluginTests()
    {
        var engine = new WorkflowExecutionEngine(
            _desktop, new FakeScreenshotService(), _windowManager, new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        _plugin = new WordPlugin(engine);
    }

    [Fact]
    public async Task NewDocumentAsync_FocusesThenSendsCtrlN()
    {
        var result = await _plugin.NewDocumentAsync();

        Assert.True(result.Success, result.Message);
        Assert.Contains("FocusByProcess(WINWORD)", _windowManager.Calls);
        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.N})", _desktop.Calls);
    }

    [Fact]
    public async Task SaveAsync_SendsCtrlS()
    {
        await _plugin.SaveAsync();

        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.S})", _desktop.Calls);
    }

    [Fact]
    public async Task PrintAsync_SendsCtrlP()
    {
        await _plugin.PrintAsync();

        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.P})", _desktop.Calls);
    }

    [Fact]
    public async Task ToggleBoldAsync_SendsCtrlB()
    {
        await _plugin.ToggleBoldAsync();

        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.B})", _desktop.Calls);
    }

    [Fact]
    public async Task ToggleItalicAsync_SendsCtrlI()
    {
        await _plugin.ToggleItalicAsync();

        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.I})", _desktop.Calls);
    }

    [Fact]
    public async Task ToggleUnderlineAsync_SendsCtrlU()
    {
        await _plugin.ToggleUnderlineAsync();

        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.U})", _desktop.Calls);
    }

    [Fact]
    public async Task FindAsync_SendsCtrlFThenTypesTheText()
    {
        var result = await _plugin.FindAsync("quarterly report");

        Assert.True(result.Success, result.Message);
        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.F})", _desktop.Calls);
        Assert.Contains("TypeText(quarterly report)", _desktop.Calls);
    }

    [Fact]
    public async Task FindAsync_WithNoText_FailsWithoutTouchingTheDesktop()
    {
        var result = await _plugin.FindAsync("");

        Assert.False(result.Success);
        Assert.Empty(_desktop.Calls);
    }

    [Fact]
    public async Task WhenWordCannotBeFocused_ReturnsFailure()
    {
        _windowManager.FocusByProcessResult = false;

        var result = await _plugin.SaveAsync();

        Assert.False(result.Success);
    }
}
