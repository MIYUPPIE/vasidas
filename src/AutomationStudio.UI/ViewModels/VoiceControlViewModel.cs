using System.Collections.ObjectModel;
using AutomationStudio.Abstractions;
using AutomationStudio.Ai.Engines;
using AutomationStudio.Ai.Intents;
using AutomationStudio.Ai.Planning;
using AutomationStudio.Applications.Chrome;
using AutomationStudio.Applications.Excel;
using AutomationStudio.Applications.FileManager;
using AutomationStudio.Applications.Obs;
using AutomationStudio.Applications.Planning;
using AutomationStudio.Applications.PowerPoint;
using AutomationStudio.Applications.VideoPsalm;
using AutomationStudio.Applications.Vlc;
using AutomationStudio.Applications.Word;
using AutomationStudio.Bible.Services;
using AutomationStudio.Core.Execution;
using AutomationStudio.Recorder;
using AutomationStudio.Storage.Repositories;
using AutomationStudio.Voice;
using AutomationStudio.Voice.Commands;
using AutomationStudio.Voice.Context;
using AutomationStudio.Voice.Intents;
using AutomationStudio.Voice.Microphone;
using AutomationStudio.Voice.SpeechToText;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Whisper.net.Ggml;

namespace AutomationStudio.UI.ViewModels;

/// <summary>
/// The Avalonia UI's first control surface for the whole voice→action pipeline every phase since 3
/// has built and tested but never actually wired up (DAS.md sections 5-10, 23, 25). Composes every
/// piece those phases shipped (mic → VAD → STT → deterministic/AI intent recognition → context
/// resolution → task planning → optional AI dynamic-workflow fallback, plus the "teach new command"
/// dialogue) behind two buttons: download the model(s) if they aren't cached yet, then
/// start/stop listening. Every application plugin <see cref="TaskPlanner"/> supports is constructed
/// here (VideoPsalm, OBS, PowerPoint, VLC, Chrome, Word, Excel, File Manager) — whichever of them
/// are actually installed on the end user's machine is what voice control ends up able to drive;
/// <see cref="AutomationStudio.Applications.ApplicationLaunchRegistry"/> stays empty by default, same
/// established reasoning as Phase 6 (no verified real path for any app on any given machine).
/// </summary>
public sealed partial class VoiceControlViewModel : ViewModelBase, IDisposable
{
    private readonly WorkflowExecutionEngine _engine;
    private readonly string _voiceModelPath;
    private readonly string _aiModelPath;

    /// <summary>Where the offline speech-to-text model is cached once downloaded — exposed for diagnostics/tests, never committed to the repo (a model weight, per CLAUDE.md).</summary>
    public string VoiceModelPath => _voiceModelPath;

    /// <summary>Where the local AI model is cached once downloaded — see <see cref="VoiceModelPath"/>.</summary>
    public string AiModelPath => _aiModelPath;

    private readonly IBibleService _bibleService = new KjvBibleService();
    private readonly IContextManager _contextManager = new ContextManager();
    private readonly IntentContextResolver _contextResolver;
    private readonly DeterministicIntentRecognizer _deterministicRecognizer;
    private readonly TaskPlanner _taskPlanner;
    private readonly TeachingCoordinator? _teaching;
    private readonly EnergyVoiceActivityDetector _voiceActivityDetector = new();

    private IMicrophoneCapture? _microphone;
    private WhisperSpeechToText? _speechToText;
    private LlamaSharpEngine? _aiEngine;
    private VoiceAssistant? _voiceAssistant;
    private CancellationTokenSource? _listenLoopCts;

    [ObservableProperty]
    private bool _isMicrophoneAvailable;

    [ObservableProperty]
    private bool _isVoiceModelReady;

    [ObservableProperty]
    private bool _isAiModelReady;

    [ObservableProperty]
    private bool _useLocalAi;

    [ObservableProperty]
    private bool _isDownloadingVoiceModel;

    [ObservableProperty]
    private bool _isDownloadingAiModel;

    [ObservableProperty]
    private bool _isListening;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _lastHeard;

    public ObservableCollection<string> Log { get; } = [];

    public VoiceControlViewModel(
        WorkflowExecutionEngine engine,
        IWorkflowRepository workflowRepository,
        IWindowManager windowManager,
        IInputRecorder? inputRecorder,
        string appDataDirectory)
    {
        _engine = engine;
        _voiceModelPath = Path.Combine(appDataDirectory, "models", "whisper-base.bin");
        _aiModelPath = Path.Combine(appDataDirectory, "models", "qwen2.5-1.5b-instruct-q4_k_m.gguf");

        var commandRegistry = new WorkflowCommandRegistry(workflowRepository);
        _contextResolver = new IntentContextResolver(_contextManager, _bibleService);
        _deterministicRecognizer = new DeterministicIntentRecognizer(_bibleService, commandRegistry);

        _taskPlanner = new TaskPlanner(
            new VideoPsalmPlugin(engine, _bibleService),
            engine,
            workflowRepository: workflowRepository,
            obs: new ObsPlugin(engine),
            powerPoint: new PowerPointPlugin(engine),
            vlc: new VlcPlugin(engine),
            chrome: new ChromePlugin(engine),
            word: new WordPlugin(engine),
            excel: new ExcelPlugin(engine),
            fileManager: new FileManagerPlugin(engine));

        // Teaching needs real global input capture — null on a pure-Wayland session with no XWayland
        // (mirrors every other optional-capability check in this codebase; "teach new command" is
        // simply unavailable there rather than crashing app startup).
        _teaching = inputRecorder is null ? null : new TeachingCoordinator(new TeachingSession(inputRecorder, windowManager), workflowRepository);

        try
        {
            _microphone = new PvMicrophoneCapture();
            IsMicrophoneAvailable = true;
        }
        catch (Exception ex)
        {
            IsMicrophoneAvailable = false;
            StatusMessage = $"No microphone available: {ex.Message}";
        }

        IsVoiceModelReady = File.Exists(_voiceModelPath);
        IsAiModelReady = LocalAiCapabilityCheck.IsModelPresent(_aiModelPath);
        UseLocalAi = IsAiModelReady;

        RebuildVoiceAssistant();
    }

    private bool CanDownloadVoiceModel() => !IsDownloadingVoiceModel && !IsVoiceModelReady;

    [RelayCommand(CanExecute = nameof(CanDownloadVoiceModel))]
    private async Task DownloadVoiceModelAsync()
    {
        IsDownloadingVoiceModel = true;
        StatusMessage = "Downloading the offline speech-to-text model…";
        DownloadVoiceModelCommand.NotifyCanExecuteChanged();

        try
        {
            await WhisperModelProvisioning.EnsureModelAsync(_voiceModelPath, GgmlType.Base).ConfigureAwait(true);
            IsVoiceModelReady = true;
            StatusMessage = "Speech-to-text model ready.";
            RebuildVoiceAssistant();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not download the speech-to-text model: {ex.Message}";
        }
        finally
        {
            IsDownloadingVoiceModel = false;
            DownloadVoiceModelCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanDownloadAiModel() => !IsDownloadingAiModel && !IsAiModelReady;

    [RelayCommand(CanExecute = nameof(CanDownloadAiModel))]
    private async Task DownloadAiModelAsync()
    {
        IsDownloadingAiModel = true;
        StatusMessage = "Downloading the local AI model (~1.1GB, one time only)…";
        DownloadAiModelCommand.NotifyCanExecuteChanged();

        try
        {
            await LocalAiModelProvisioning.EnsureModelAsync(_aiModelPath).ConfigureAwait(true);
            IsAiModelReady = true;
            UseLocalAi = true;
            StatusMessage = "Local AI model ready.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not download the local AI model: {ex.Message}";
        }
        finally
        {
            IsDownloadingAiModel = false;
            DownloadAiModelCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnUseLocalAiChanged(bool value) => RebuildVoiceAssistant();

    private bool CanToggleListening() => IsListening || (_voiceAssistant is not null && IsMicrophoneAvailable);

    [RelayCommand(CanExecute = nameof(CanToggleListening))]
    private void ToggleListening()
    {
        if (IsListening)
        {
            _listenLoopCts?.Cancel();
            return;
        }

        _listenLoopCts = new CancellationTokenSource();
        IsListening = true;
        StatusMessage = "Listening…";
        ToggleListeningCommand.NotifyCanExecuteChanged();
        _ = RunListenLoopAsync(_listenLoopCts.Token);
    }

    private async Task RunListenLoopAsync(CancellationToken cancellationToken)
    {
        string? errorMessage = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var outcome = await _voiceAssistant!.ListenAndExecuteWithTranscriptAsync(TimeSpan.FromSeconds(8), cancellationToken).ConfigureAwait(true);
                if (outcome.Text is null)
                {
                    continue;
                }

                LastHeard = outcome.Text;
                var line = $"{DateTimeOffset.Now:HH:mm:ss}  \"{outcome.Text}\"  —  " + outcome.Result switch
                {
                    null => "not understood",
                    { Success: true, Message: null } => "OK",
                    { Success: true } => outcome.Result.Message,
                    _ => $"failed: {outcome.Result.Message}"
                };
                Log.Insert(0, line);
                while (Log.Count > 50)
                {
                    Log.RemoveAt(Log.Count - 1);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected — "Stop Listening" was clicked.
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            IsListening = false;
            StatusMessage = errorMessage is null ? "Stopped." : $"Voice control stopped unexpectedly: {errorMessage}";
            ToggleListeningCommand.NotifyCanExecuteChanged();
        }
    }

    private void RebuildVoiceAssistant()
    {
        if (!IsVoiceModelReady || _microphone is null)
        {
            _voiceAssistant = null;
            ToggleListeningCommand.NotifyCanExecuteChanged();
            return;
        }

        _speechToText ??= new WhisperSpeechToText(_voiceModelPath);

        IIntentRecognizer recognizer = _deterministicRecognizer;
        AiWorkflowGenerator? workflowGenerator = null;
        if (UseLocalAi && IsAiModelReady)
        {
            _aiEngine ??= new LlamaSharpEngine(_aiModelPath);
            recognizer = new AiAugmentedIntentRecognizer(_deterministicRecognizer, new AiIntentInterpreter(_aiEngine));
            workflowGenerator = new AiWorkflowGenerator(_aiEngine, _engine);
        }

        var listener = new VoiceCommandListener(_microphone, _voiceActivityDetector, _speechToText, recognizer, _contextResolver);
        _voiceAssistant = new VoiceAssistant(listener, _taskPlanner, _teaching, workflowGenerator);
        ToggleListeningCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _listenLoopCts?.Cancel();
        _listenLoopCts?.Dispose();
        _microphone?.Dispose();
        _speechToText?.Dispose();
        _aiEngine?.Dispose();
    }
}
