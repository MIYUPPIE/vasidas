using AutomationStudio.Abstractions;
using AutomationStudio.Applications.Chrome;
using AutomationStudio.Core.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Applications.Tests;

public class ChromePluginTests
{
    private readonly FakeDesktopController _desktop = new();
    private readonly FakeWindowManager _windowManager = new();
    private readonly ChromePlugin _plugin;

    public ChromePluginTests()
    {
        var engine = new WorkflowExecutionEngine(
            _desktop, new FakeScreenshotService(), _windowManager, new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        _plugin = new ChromePlugin(engine);
    }

    [Fact]
    public async Task NewTabAsync_FocusesThenSendsCtrlT()
    {
        var result = await _plugin.NewTabAsync();

        Assert.True(result.Success, result.Message);
        Assert.Contains("FocusByProcess(chrome)", _windowManager.Calls);
        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.T})", _desktop.Calls);
    }

    [Fact]
    public async Task CloseTabAsync_SendsCtrlW()
    {
        await _plugin.CloseTabAsync();

        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.W})", _desktop.Calls);
    }

    [Fact]
    public async Task ReloadAsync_SendsCtrlR()
    {
        await _plugin.ReloadAsync();

        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.R})", _desktop.Calls);
    }

    [Fact]
    public async Task GoBackAsync_SendsAltLeft()
    {
        await _plugin.GoBackAsync();

        Assert.Contains($"KeyCombination({Key.LeftAlt}+{Key.Left})", _desktop.Calls);
    }

    [Fact]
    public async Task GoForwardAsync_SendsAltRight()
    {
        await _plugin.GoForwardAsync();

        Assert.Contains($"KeyCombination({Key.LeftAlt}+{Key.Right})", _desktop.Calls);
    }

    [Fact]
    public async Task OpenUrlAsync_FocusesAddressBarThenTypesTheUrlThenEnter()
    {
        var result = await _plugin.OpenUrlAsync("example.com");

        Assert.True(result.Success, result.Message);
        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.L})", _desktop.Calls);
        Assert.Contains("TypeText(example.com)", _desktop.Calls);
        Assert.Contains($"PressKey({Key.Enter})", _desktop.Calls);
    }

    [Fact]
    public async Task OpenUrlAsync_WithNoUrl_FailsWithoutTouchingTheDesktop()
    {
        var result = await _plugin.OpenUrlAsync("");

        Assert.False(result.Success);
        Assert.Empty(_desktop.Calls);
    }

    [Fact]
    public async Task FindOnPageAsync_SendsCtrlFThenTypesTheText()
    {
        var result = await _plugin.FindOnPageAsync("recording settings");

        Assert.True(result.Success, result.Message);
        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.F})", _desktop.Calls);
        Assert.Contains("TypeText(recording settings)", _desktop.Calls);
    }

    [Fact]
    public async Task FindOnPageAsync_WithNoText_FailsWithoutTouchingTheDesktop()
    {
        var result = await _plugin.FindOnPageAsync("   ");

        Assert.False(result.Success);
        Assert.Empty(_desktop.Calls);
    }

    [Fact]
    public async Task WhenChromeCannotBeFocused_ReturnsFailure()
    {
        _windowManager.FocusByProcessResult = false;

        var result = await _plugin.NewTabAsync();

        Assert.False(result.Success);
    }
}
