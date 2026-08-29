using AutomationStudio.Core.Actions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutomationStudio.UI.ViewModels.ActionEditors;

/// <summary>
/// Edits one <see cref="WorkflowAction"/>. Core's action types are immutable (init-only — correct
/// for an execution/serialization model, wrong for two-way XAML binding), so this is the mutable
/// UI-side counterpart: it holds editable copies of an action's fields and <see cref="Build"/>s a
/// fresh immutable action from them on demand, rather than mutating the domain object in place.
/// </summary>
public abstract partial class ActionEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private int _retryCount;

    [ObservableProperty]
    private int _timeoutMilliseconds = 30_000;

    /// <summary>Matches <see cref="WorkflowAction.ActionType"/> — used for the palette label and action-list display.</summary>
    public abstract string ActionTypeName { get; }

    /// <summary>One-line summary shown in the action list (e.g. "Click (742, 510)").</summary>
    public abstract string Summary { get; }

    public abstract WorkflowAction Build(string id);

    protected void LoadCommonFrom(WorkflowAction action)
    {
        Enabled = action.Enabled;
        RetryCount = action.RetryCount;
        TimeoutMilliseconds = action.TimeoutMilliseconds;
    }

    partial void OnEnabledChanged(bool value) => OnPropertyChanged(nameof(Summary));
}
