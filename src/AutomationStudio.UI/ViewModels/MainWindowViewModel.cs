using System.Collections.ObjectModel;
using AutomationStudio.Abstractions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Core.Workflows;
using AutomationStudio.Storage;
using AutomationStudio.Storage.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutomationStudio.UI.ViewModels;

/// <summary>Top-level shell: the saved-workflow list on one side, the currently open <see cref="WorkflowEditorViewModel"/> on the other (section 19).</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly WorkflowExecutionEngine _engine;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IExecutionRepository _executionRepository;

    public ObservableCollection<WorkflowSummary> Workflows { get; } = [];

    [ObservableProperty]
    private WorkflowEditorViewModel? _currentEditor;

    [ObservableProperty]
    private WorkflowSummary? _selectedWorkflow;

    [ObservableProperty]
    private string? _statusMessage;

    public string CapabilitySummary { get; }
    public IReadOnlyList<string> CapabilityNotes { get; }
    public VoiceControlViewModel VoiceControl { get; }

    public MainWindowViewModel(
        WorkflowExecutionEngine engine,
        IWorkflowRepository workflowRepository,
        IExecutionRepository executionRepository,
        ICapabilityProvider capabilityProvider,
        VoiceControlViewModel voiceControl)
    {
        _engine = engine;
        _workflowRepository = workflowRepository;
        _executionRepository = executionRepository;
        VoiceControl = voiceControl;

        var capabilities = capabilityProvider.DetectCapabilities();
        var desktop = capabilities.DesktopEnvironment is null ? "" : $" / {capabilities.DesktopEnvironment}";
        CapabilitySummary =
            $"{capabilities.OperatingSystem}{desktop} / {capabilities.DisplayServer}  —  " +
            $"Mouse/Keyboard: {capabilities.MouseInjection}  •  Screen Capture: {capabilities.ScreenCapture}  •  Windows: {capabilities.WindowManagement}";
        CapabilityNotes = capabilities.Notes;

        NewWorkflow();
        _ = RefreshWorkflowsAsync();
    }

    [RelayCommand]
    private void NewWorkflow()
    {
        CurrentEditor = new WorkflowEditorViewModel(
            _engine, _workflowRepository, _executionRepository, onSaved: () => _ = RefreshWorkflowsAsync());
        StatusMessage = null;
    }

    private bool HasSelectedWorkflow() => SelectedWorkflow is not null;

    [RelayCommand(CanExecute = nameof(HasSelectedWorkflow))]
    private async Task OpenSelectedWorkflowAsync()
    {
        if (SelectedWorkflow is null)
        {
            return;
        }

        var record = await _workflowRepository.GetAsync(SelectedWorkflow.Id).ConfigureAwait(true);
        if (record is null)
        {
            StatusMessage = "That workflow no longer exists.";
            await RefreshWorkflowsAsync().ConfigureAwait(true);
            return;
        }

        CurrentEditor = new WorkflowEditorViewModel(
            _engine, _workflowRepository, _executionRepository, onSaved: () => _ = RefreshWorkflowsAsync(), existing: record);
        StatusMessage = null;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedWorkflow))]
    private async Task DeleteSelectedWorkflowAsync()
    {
        if (SelectedWorkflow is null)
        {
            return;
        }

        var deletedId = SelectedWorkflow.Id;
        await _workflowRepository.DeleteAsync(deletedId).ConfigureAwait(true);

        if (CurrentEditor?.Id == deletedId)
        {
            NewWorkflow();
        }

        await RefreshWorkflowsAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshWorkflowsAsync()
    {
        var summaries = await _workflowRepository.ListAsync().ConfigureAwait(true);
        Workflows.Clear();
        foreach (var summary in summaries)
        {
            Workflows.Add(summary);
        }
    }

    public async Task ExportCurrentWorkflowAsync(string filePath)
    {
        if (CurrentEditor is null)
        {
            return;
        }

        if (!CurrentEditor.TryBuildWorkflow(out var workflow, out var error))
        {
            StatusMessage = $"Cannot export: {error}";
            return;
        }

        await WorkflowSerializer.SaveToFileAsync(workflow, filePath).ConfigureAwait(true);
        StatusMessage = $"Exported to {filePath}.";
    }

    public async Task ImportWorkflowAsync(string filePath)
    {
        Workflow workflow;
        try
        {
            workflow = await WorkflowSerializer.LoadFromFileAsync(filePath).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cannot import '{filePath}': {ex.Message}";
            return;
        }

        var now = DateTimeOffset.UtcNow;
        CurrentEditor = new WorkflowEditorViewModel(
            _engine, _workflowRepository, _executionRepository, onSaved: () => _ = RefreshWorkflowsAsync(),
            existing: new WorkflowRecord { Workflow = workflow, CreatedAtUtc = now, UpdatedAtUtc = now });
        StatusMessage = $"Imported {Path.GetFileName(filePath)}.";
    }
}
