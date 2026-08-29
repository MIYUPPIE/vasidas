using System.Collections.ObjectModel;
using AutomationStudio.Abstractions;
using AutomationStudio.Core.Execution;
using AutomationStudio.Core.Workflows;
using AutomationStudio.Storage;
using AutomationStudio.Storage.Repositories;
using AutomationStudio.UI.ViewModels.ActionEditors;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutomationStudio.UI.ViewModels;

/// <summary>
/// One open workflow: its editable metadata and action list (section 19/20), plus the
/// RUN/PAUSE/STOP/EMERGENCY STOP surface section 28 requires. Building and running a workflow both
/// go through the same real <see cref="WorkflowExecutionEngine"/> and platform backend Phase 1
/// shipped — this view model is wiring, not a second execution path.
/// </summary>
public partial class WorkflowEditorViewModel : ViewModelBase
{
    private readonly WorkflowExecutionEngine _engine;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IExecutionRepository _executionRepository;
    private readonly Action _onSaved;

    private WorkflowRunControl? _runControl;

    public string Id { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private ActionEditorViewModel? _selectedAction;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isPaused;

    public ObservableCollection<ActionEditorViewModel> Actions { get; } = [];

    public ObservableCollection<string> LogLines { get; } = [];

    public IReadOnlyList<ActionPaletteEntry> Palette { get; } = ActionPaletteEntry.All;

    public WorkflowEditorViewModel(
        WorkflowExecutionEngine engine,
        IWorkflowRepository workflowRepository,
        IExecutionRepository executionRepository,
        Action onSaved,
        WorkflowRecord? existing = null)
    {
        _engine = engine;
        _workflowRepository = workflowRepository;
        _executionRepository = executionRepository;
        _onSaved = onSaved;

        if (existing is null)
        {
            Id = Guid.NewGuid().ToString("n");
            _name = "New Workflow";
        }
        else
        {
            Id = existing.Workflow.Id;
            _name = existing.Workflow.Name;
            _description = existing.Workflow.Description;
            foreach (var action in existing.Workflow.Actions)
            {
                Actions.Add(ActionEditorFactory.FromAction(action));
            }
        }
    }

    [RelayCommand]
    private void AddAction(ActionPaletteEntry entry)
    {
        var editor = entry.CreateEditor();
        Actions.Add(editor);
        SelectedAction = editor;
    }

    private bool HasSelection() => SelectedAction is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void RemoveSelectedAction()
    {
        if (SelectedAction is null)
        {
            return;
        }

        Actions.Remove(SelectedAction);
        SelectedAction = null;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveSelectedActionUp()
    {
        if (SelectedAction is null)
        {
            return;
        }

        var index = Actions.IndexOf(SelectedAction);
        if (index > 0)
        {
            Actions.Move(index, index - 1);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveSelectedActionDown()
    {
        if (SelectedAction is null)
        {
            return;
        }

        var index = Actions.IndexOf(SelectedAction);
        if (index >= 0 && index < Actions.Count - 1)
        {
            Actions.Move(index, index + 1);
        }
    }

    public bool TryBuildWorkflow(out Workflow workflow, out string? error)
    {
        try
        {
            var builtActions = Actions.Select((editor, index) => editor.Build($"action-{index:D3}")).ToList();
            workflow = new Workflow { Id = Id, Name = Name, Description = Description, Actions = builtActions };
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            workflow = null!;
            error = ex.Message;
            return false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!TryBuildWorkflow(out var workflow, out var error))
        {
            StatusMessage = $"Cannot save: {error}";
            return;
        }

        var now = DateTimeOffset.UtcNow;
        await _workflowRepository.SaveAsync(new WorkflowRecord
        {
            Workflow = workflow,
            Platform = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : null,
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }).ConfigureAwait(true);

        StatusMessage = $"Saved at {now.ToLocalTime():T}.";
        _onSaved();
    }

    private bool CanRun() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if (!TryBuildWorkflow(out var workflow, out var error))
        {
            StatusMessage = $"Cannot run: {error}";
            return;
        }

        LogLines.Clear();
        StatusMessage = null;
        IsRunning = true;
        IsPaused = false;
        RunCommand.NotifyCanExecuteChanged();
        PauseResumeCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();

        using var runControl = new WorkflowRunControl();
        _runControl = runControl;

        var executionId = await _executionRepository.StartExecutionAsync(Id, DateTimeOffset.UtcNow).ConfigureAwait(true);
        var progress = new Progress<ActionExecutionResult>(result => OnActionResult(executionId, result));

        try
        {
            var result = await _engine.ExecuteAsync(workflow, runControl, progress).ConfigureAwait(true);
            await _executionRepository.CompleteExecutionAsync(
                executionId, DateTimeOffset.UtcNow, result.Status.ToString(), result.FailureReason).ConfigureAwait(true);
            StatusMessage = $"Workflow finished: {result.Status}" + (result.FailureReason is null ? "" : $" — {result.FailureReason}");
        }
        catch (Exception ex)
        {
            await _executionRepository.CompleteExecutionAsync(
                executionId, DateTimeOffset.UtcNow, ActionStatus.Failed.ToString(), ex.Message).ConfigureAwait(true);
            StatusMessage = $"Workflow crashed: {ex.Message}";
        }
        finally
        {
            _runControl = null;
            IsRunning = false;
            IsPaused = false;
            RunCommand.NotifyCanExecuteChanged();
            PauseResumeCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanPauseOrStop() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanPauseOrStop))]
    private void PauseResume()
    {
        if (_runControl is null)
        {
            return;
        }

        if (IsPaused)
        {
            _runControl.Resume();
            IsPaused = false;
        }
        else
        {
            _runControl.Pause();
            IsPaused = true;
        }
    }

    /// <summary>The "Emergency Stop" section 28 requires — cannot be undone once pressed.</summary>
    [RelayCommand(CanExecute = nameof(CanPauseOrStop))]
    private void Stop() => _runControl?.Stop();

    private void OnActionResult(string executionId, ActionExecutionResult result)
    {
        var line = $"{DateTimeOffset.Now:HH:mm:ss} [{result.Status}] {result.ActionType}" +
                    $" ({result.Duration.TotalMilliseconds:F0}ms)" +
                    (string.IsNullOrEmpty(result.Message) ? "" : $" — {result.Message}");
        LogLines.Add(line);

        _ = TryLogToDatabaseAsync(executionId, result, line);
    }

    private async Task TryLogToDatabaseAsync(string executionId, ActionExecutionResult result, string line)
    {
        try
        {
            await _executionRepository.AddLogEntryAsync(new ExecutionLogEntry(
                executionId, DateTimeOffset.UtcNow, result.Status.ToString(), line, result.ActionId, result.ScreenshotPath)).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort: a DB logging failure must not interrupt (or crash) the running workflow.
        }
    }
}
