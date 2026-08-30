using AutomationStudio.Abstractions;
using AutomationStudio.Applications.PowerPoint;
using AutomationStudio.Core.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Applications.Tests;

public class PowerPointPluginTests
{
    private readonly FakeDesktopController _desktop = new();
    private readonly FakeWindowManager _windowManager = new();
    private readonly PowerPointPlugin _plugin;

    public PowerPointPluginTests()
    {
        var engine = new WorkflowExecutionEngine(
            _desktop, new FakeScreenshotService(), _windowManager, new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        _plugin = new PowerPointPlugin(engine);
    }

    [Fact]
    public async Task NextSlideAsync_FocusesThenPressesRight()
    {
        var result = await _plugin.NextSlideAsync();

        Assert.True(result.Success, result.Message);
        Assert.Contains("FocusByProcess(POWERPNT)", _windowManager.Calls);
        Assert.Contains($"PressKey({Key.Right})", _desktop.Calls);
    }

    [Fact]
    public async Task PreviousSlideAsync_PressesLeft()
    {
        await _plugin.PreviousSlideAsync();

        Assert.Contains($"PressKey({Key.Left})", _desktop.Calls);
    }

    [Fact]
    public async Task GoToSlideAsync_TypesTheNumberThenPressesEnter()
    {
        var result = await _plugin.GoToSlideAsync(5);

        Assert.True(result.Success, result.Message);
        Assert.Contains("TypeText(5)", _desktop.Calls);
        Assert.Contains($"PressKey({Key.Enter})", _desktop.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GoToSlideAsync_WithANonPositiveNumber_FailsWithoutTouchingTheDesktop(int slideNumber)
    {
        var result = await _plugin.GoToSlideAsync(slideNumber);

        Assert.False(result.Success);
        Assert.Empty(_desktop.Calls);
    }

    [Fact]
    public async Task StartPresentationAsync_PressesF5()
    {
        await _plugin.StartPresentationAsync();

        Assert.Contains($"PressKey({Key.F5})", _desktop.Calls);
    }

    [Fact]
    public async Task EndPresentationAsync_PressesEscape()
    {
        await _plugin.EndPresentationAsync();

        Assert.Contains($"PressKey({Key.Escape})", _desktop.Calls);
    }

    [Fact]
    public async Task WhenPowerPointCannotBeFocused_ReturnsFailure()
    {
        _windowManager.FocusByProcessResult = false;

        var result = await _plugin.NextSlideAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
    }
}
