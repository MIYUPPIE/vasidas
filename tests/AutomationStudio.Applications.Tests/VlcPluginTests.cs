using AutomationStudio.Abstractions;
using AutomationStudio.Applications.Vlc;
using AutomationStudio.Core.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Applications.Tests;

public class VlcPluginTests
{
    private readonly FakeDesktopController _desktop = new();
    private readonly FakeWindowManager _windowManager = new();
    private readonly VlcPlugin _plugin;

    public VlcPluginTests()
    {
        var engine = new WorkflowExecutionEngine(
            _desktop, new FakeScreenshotService(), _windowManager, new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        _plugin = new VlcPlugin(engine);
    }

    [Fact]
    public async Task PlayAsync_FocusesThenPressesSpace()
    {
        var result = await _plugin.PlayAsync();

        Assert.True(result.Success, result.Message);
        Assert.Contains("FocusByProcess(vlc)", _windowManager.Calls);
        Assert.Contains($"PressKey({Key.Space})", _desktop.Calls);
    }

    [Fact]
    public async Task PauseAsync_AlsoPressesSpace_MatchingVlcsRealToggleBehavior()
    {
        // VLC has no separate default "pause" key — Space toggles both, confirmed via research —
        // so Play and Pause deliberately send the exact same key rather than inventing two.
        await _plugin.PauseAsync();

        Assert.Contains($"PressKey({Key.Space})", _desktop.Calls);
    }

    [Fact]
    public async Task StopAsync_PressesS()
    {
        await _plugin.StopAsync();

        Assert.Contains($"PressKey({Key.S})", _desktop.Calls);
    }

    [Fact]
    public async Task NextTrackAsync_PressesN()
    {
        await _plugin.NextTrackAsync();

        Assert.Contains($"PressKey({Key.N})", _desktop.Calls);
    }

    [Fact]
    public async Task PreviousTrackAsync_PressesP()
    {
        await _plugin.PreviousTrackAsync();

        Assert.Contains($"PressKey({Key.P})", _desktop.Calls);
    }

    [Fact]
    public async Task ToggleMuteAsync_PressesM()
    {
        await _plugin.ToggleMuteAsync();

        Assert.Contains($"PressKey({Key.M})", _desktop.Calls);
    }

    [Fact]
    public async Task ToggleFullscreenAsync_PressesF()
    {
        await _plugin.ToggleFullscreenAsync();

        Assert.Contains($"PressKey({Key.F})", _desktop.Calls);
    }

    [Fact]
    public async Task WhenVlcCannotBeFocused_ReturnsFailure()
    {
        _windowManager.FocusByProcessResult = false;

        var result = await _plugin.PlayAsync();

        Assert.False(result.Success);
    }
}
