using AutomationStudio.Abstractions;
using AutomationStudio.Core.Execution;
using AutomationStudio.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutomationStudio.UI.Tests;

/// <summary>
/// The Avalonia UI's first control surface for the whole voice pipeline (Phase 11 continuation —
/// wiring DAS.md sections 5-10/23/25 into the app). Exercises the ViewModel's own logic (readiness
/// gating, command enabling) with a real <see cref="WorkflowExecutionEngine"/> over fakes, matching
/// <see cref="WorkflowEditorViewModelTests"/>'s own pattern — the underlying model downloads and
/// real inference are already covered by <c>WhisperSpeechToTextTests</c>/<c>LlamaSharpEngineTests</c>,
/// so these tests deliberately never trigger a real download.
/// </summary>
public class VoiceControlViewModelTests
{
    private static (VoiceControlViewModel ViewModel, string AppDataDirectory) Build(string? appDataDirectory = null)
    {
        var directory = appDataDirectory ?? Path.Combine(Path.GetTempPath(), "automationstudio-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(directory);

        var engine = new WorkflowExecutionEngine(
            new FakeDesktopController(), new FakeScreenshotService(), new FakeWindowManager(), new FakeElementFinder(), new FakeOcrEngine(), new FakeTemplateMatcher(),
            NullLogger<WorkflowExecutionEngine>.Instance);
        var workflowRepository = new FakeWorkflowRepository();

        var viewModel = new VoiceControlViewModel(engine, workflowRepository, new FakeWindowManager(), inputRecorder: null, directory);
        return (viewModel, directory);
    }

    [Fact]
    public void Construction_WithAFreshAppDataDirectory_ReportsNeitherModelReady()
    {
        var (viewModel, _) = Build();

        Assert.False(viewModel.IsVoiceModelReady);
        Assert.False(viewModel.IsAiModelReady);
        Assert.False(viewModel.UseLocalAi);
        viewModel.Dispose();
    }

    [Fact]
    public void Construction_WithNoInputRecorder_DoesNotThrow_TeachingIsSimplyUnavailable()
    {
        // Mirrors LinuxAutomationEnvironment's own "no XWayland, no InputRecorder" degrade-gracefully
        // contract (section 31) — a pure-Wayland session must not crash the whole app on startup.
        var (viewModel, _) = Build();

        Assert.NotNull(viewModel);
        viewModel.Dispose();
    }

    [Fact]
    public void ToggleListeningCommand_CanExecute_FalseWhenNoVoiceModelIsDownloaded()
    {
        var (viewModel, _) = Build();

        Assert.False(viewModel.ToggleListeningCommand.CanExecute(null));
        viewModel.Dispose();
    }

    [Fact]
    public void ToggleListeningCommand_CanExecute_TrueOnceTheVoiceModelFileExistsAndAMicrophoneIsAvailable()
    {
        // The model path is a private implementation detail computed from appDataDirectory — learn
        // it from a throwaway instance first rather than hardcoding the filename convention here.
        var directory = Path.Combine(Path.GetTempPath(), "automationstudio-tests", Guid.NewGuid().ToString("n"));
        var (probe, _) = Build(directory);
        var voiceModelPath = probe.VoiceModelPath;
        probe.Dispose();

        Directory.CreateDirectory(Path.GetDirectoryName(voiceModelPath)!);
        File.WriteAllBytes(voiceModelPath, [0x01]); // content is irrelevant — only File.Exists is checked at construction.

        var (viewModel, _) = Build(directory);
        try
        {
            if (!viewModel.IsMicrophoneAvailable)
            {
                return; // no real microphone on this machine/CI runner — nothing to assert.
            }

            Assert.True(viewModel.IsVoiceModelReady);
            Assert.True(viewModel.ToggleListeningCommand.CanExecute(null));
        }
        finally
        {
            viewModel.Dispose();
        }
    }

    [Fact]
    public void DownloadVoiceModelCommand_CanExecute_FalseOnceTheModelIsAlreadyPresent()
    {
        var directory = Path.Combine(Path.GetTempPath(), "automationstudio-tests", Guid.NewGuid().ToString("n"));
        var (probe, _) = Build(directory);
        var voiceModelPath = probe.VoiceModelPath;
        probe.Dispose();

        Directory.CreateDirectory(Path.GetDirectoryName(voiceModelPath)!);
        File.WriteAllBytes(voiceModelPath, [0x01]);

        var (viewModel, _) = Build(directory);

        Assert.True(viewModel.IsVoiceModelReady);
        Assert.False(viewModel.DownloadVoiceModelCommand.CanExecute(null));
        viewModel.Dispose();
    }

    [Fact]
    public void DownloadAiModelCommand_CanExecute_TrueByDefault_NothingDownloadedYet()
    {
        var (viewModel, _) = Build();

        Assert.True(viewModel.DownloadAiModelCommand.CanExecute(null));
        viewModel.Dispose();
    }
}
