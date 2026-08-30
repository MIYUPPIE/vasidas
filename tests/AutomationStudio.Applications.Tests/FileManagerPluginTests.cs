using AutomationStudio.Abstractions;
using AutomationStudio.Applications.FileManager;
using AutomationStudio.Core.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.Applications.Tests;

public class FileManagerPluginTests
{
    private readonly FakeDesktopController _desktop = new();
    private readonly FakeWindowManager _windowManager = new();
    private readonly FileManagerPlugin _plugin;

    public FileManagerPluginTests()
    {
        var engine = new WorkflowExecutionEngine(
            _desktop, new FakeScreenshotService(), _windowManager, new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        _plugin = new FileManagerPlugin(engine);
    }

    [Fact]
    public async Task NewFolderAsync_FocusesThenSendsCtrlShiftN()
    {
        var result = await _plugin.NewFolderAsync();

        Assert.True(result.Success, result.Message);
        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.LeftShift}+{Key.N})", _desktop.Calls);
    }

    [Fact]
    public async Task CopyAsync_SendsCtrlC()
    {
        await _plugin.CopyAsync();

        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.C})", _desktop.Calls);
    }

    [Fact]
    public async Task CutAsync_SendsCtrlX()
    {
        await _plugin.CutAsync();

        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.X})", _desktop.Calls);
    }

    [Fact]
    public async Task PasteAsync_SendsCtrlV()
    {
        await _plugin.PasteAsync();

        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.V})", _desktop.Calls);
    }

    [Fact]
    public async Task RenameAsync_PressesF2ThenTypesTheNewNameThenEnter()
    {
        var result = await _plugin.RenameAsync("vacation photos");

        Assert.True(result.Success, result.Message);
        Assert.Contains($"PressKey({Key.F2})", _desktop.Calls);
        Assert.Contains("TypeText(vacation photos)", _desktop.Calls);
        Assert.Contains($"PressKey({Key.Enter})", _desktop.Calls);
    }

    [Fact]
    public async Task RenameAsync_WithNoNewName_FailsWithoutTouchingTheDesktop()
    {
        var result = await _plugin.RenameAsync("");

        Assert.False(result.Success);
        Assert.Empty(_desktop.Calls);
    }

    [Fact]
    public async Task DeleteAsync_PressesDelete()
    {
        await _plugin.DeleteAsync();

        Assert.Contains($"PressKey({Key.Delete})", _desktop.Calls);
    }

    [Fact]
    public async Task SearchAsync_SendsCtrlFThenTypesTheQuery()
    {
        var result = await _plugin.SearchAsync("budget");

        Assert.True(result.Success, result.Message);
        Assert.Contains($"KeyCombination({Key.LeftControl}+{Key.F})", _desktop.Calls);
        Assert.Contains("TypeText(budget)", _desktop.Calls);
    }

    [Fact]
    public async Task WhenFileManagerCannotBeFocused_ReturnsFailure()
    {
        _windowManager.FocusByProcessResult = false;

        var result = await _plugin.CopyAsync();

        Assert.False(result.Success);
    }
}

public class FileManagerProfileTests
{
    [Fact]
    public void WindowsAndLinuxProfiles_DifferOnlyInProcessName()
    {
        Assert.Equal("explorer", FileManagerProfile.Windows.ProcessName);
        Assert.Equal("nautilus", FileManagerProfile.Linux.ProcessName);

        Assert.Equal(FileManagerProfile.Windows.NewFolderKey, FileManagerProfile.Linux.NewFolderKey);
        Assert.Equal(FileManagerProfile.Windows.CopyKey, FileManagerProfile.Linux.CopyKey);
        Assert.Equal(FileManagerProfile.Windows.RenameKey, FileManagerProfile.Linux.RenameKey);
        Assert.Equal(FileManagerProfile.Windows.DeleteKey, FileManagerProfile.Linux.DeleteKey);
        Assert.Equal(FileManagerProfile.Windows.SearchKey, FileManagerProfile.Linux.SearchKey);
    }

    [Fact]
    public void Default_MatchesTheCurrentOperatingSystem()
    {
        var expected = OperatingSystem.IsWindows() ? FileManagerProfile.Windows : FileManagerProfile.Linux;

        Assert.Equal(expected.ProcessName, FileManagerProfile.Default.ProcessName);
    }
}
