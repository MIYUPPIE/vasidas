using AutomationStudio.Abstractions;
using AutomationStudio.Applications.Excel;
using AutomationStudio.Core.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Applications.Tests;

public class ExcelPluginTests
{
    private readonly FakeDesktopController _desktop = new();
    private readonly FakeWindowManager _windowManager = new();
    private readonly ExcelPlugin _plugin;

    public ExcelPluginTests()
    {
        var engine = new WorkflowExecutionEngine(
            _desktop, new FakeScreenshotService(), _windowManager, new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        _plugin = new ExcelPlugin(engine);
    }

    [Fact]
    public async Task NewWorkbookAsync_FocusesThenSendsCtrlN()
    {
        var result = await _plugin.NewWorkbookAsync();

        Assert.True(result.Success, result.Message);
        Assert.Contains("FocusByProcess(EXCEL)", _windowManager.Calls);
        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.N})", _desktop.Calls);
    }

    [Fact]
    public async Task SaveAsync_SendsCtrlS()
    {
        await _plugin.SaveAsync();

        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.S})", _desktop.Calls);
    }

    [Fact]
    public async Task GoToCellAsync_SendsCtrlGThenTypesTheReferenceThenEnter()
    {
        var result = await _plugin.GoToCellAsync("B14");

        Assert.True(result.Success, result.Message);
        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.G})", _desktop.Calls);
        Assert.Contains("TypeText(B14)", _desktop.Calls);
        Assert.Contains($"PressKey({Key.Enter})", _desktop.Calls);
    }

    [Fact]
    public async Task GoToCellAsync_WithNoReference_FailsWithoutTouchingTheDesktop()
    {
        var result = await _plugin.GoToCellAsync("");

        Assert.False(result.Success);
        Assert.Empty(_desktop.Calls);
    }

    [Fact]
    public async Task InsertAutoSumAsync_SendsAltEquals()
    {
        await _plugin.InsertAutoSumAsync();

        Assert.Contains($"KeyCombination({Key.LeftAlt}+{Key.Equals})", _desktop.Calls);
    }

    [Fact]
    public async Task ToggleBoldAsync_SendsCtrlB()
    {
        await _plugin.ToggleBoldAsync();

        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.B})", _desktop.Calls);
    }

    [Fact]
    public async Task FindAsync_SendsCtrlFThenTypesTheText()
    {
        var result = await _plugin.FindAsync("budget");

        Assert.True(result.Success, result.Message);
        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.F})", _desktop.Calls);
        Assert.Contains("TypeText(budget)", _desktop.Calls);
    }

    [Fact]
    public async Task WhenExcelCannotBeFocused_ReturnsFailure()
    {
        _windowManager.FocusByProcessResult = false;

        var result = await _plugin.SaveAsync();

        Assert.False(result.Success);
    }
}
