using AutomationStudio.Core.Actions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutomationStudio.UI.ViewModels.ActionEditors;

/// <summary>Shared Text/AutomationId/WindowTitleContains target shape for FindElement, ClickElement
/// and TypeIntoElement (section 10/15) — a query matches whichever of these fields are non-blank.</summary>
public abstract partial class ElementTargetEditorViewModelBase : ActionEditorViewModel
{
    [ObservableProperty]
    private string _targetText = "";

    [ObservableProperty]
    private string _automationId = "";

    [ObservableProperty]
    private string _windowTitleContains = "";

    protected ElementTarget BuildTarget() => new()
    {
        Text = string.IsNullOrWhiteSpace(TargetText) ? null : TargetText,
        AutomationId = string.IsNullOrWhiteSpace(AutomationId) ? null : AutomationId,
        WindowTitleContains = string.IsNullOrWhiteSpace(WindowTitleContains) ? null : WindowTitleContains
    };

    protected string DescribeTarget() => BuildTarget().ToQuery().ToString();

    protected void LoadTargetFrom(ElementTarget target, WorkflowAction action)
    {
        TargetText = target.Text ?? "";
        AutomationId = target.AutomationId ?? "";
        WindowTitleContains = target.WindowTitleContains ?? "";
        LoadCommonFrom(action);
    }

    partial void OnTargetTextChanged(string value) => OnPropertyChanged(nameof(Summary));
    partial void OnAutomationIdChanged(string value) => OnPropertyChanged(nameof(Summary));
    partial void OnWindowTitleContainsChanged(string value) => OnPropertyChanged(nameof(Summary));
}

public sealed class FindElementEditorViewModel : ElementTargetEditorViewModelBase
{
    public override string ActionTypeName => "FindElement";
    public override string Summary => $"Find {DescribeTarget()}";

    public override WorkflowAction Build(string id) => new FindElementAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds,
        Target = BuildTarget()
    };

    public static FindElementEditorViewModel FromAction(FindElementAction action)
    {
        var vm = new FindElementEditorViewModel();
        vm.LoadTargetFrom(action.Target, action);
        return vm;
    }
}

public sealed class ClickElementEditorViewModel : ElementTargetEditorViewModelBase
{
    public override string ActionTypeName => "ClickElement";
    public override string Summary => $"Click {DescribeTarget()}";

    public override WorkflowAction Build(string id) => new ClickElementAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds,
        Target = BuildTarget()
    };

    public static ClickElementEditorViewModel FromAction(ClickElementAction action)
    {
        var vm = new ClickElementEditorViewModel();
        vm.LoadTargetFrom(action.Target, action);
        return vm;
    }
}

public partial class TypeIntoElementEditorViewModel : ElementTargetEditorViewModelBase
{
    [ObservableProperty]
    private string _text = "";

    public override string ActionTypeName => "TypeIntoElement";
    public override string Summary => string.IsNullOrEmpty(Text) ? $"Type into {DescribeTarget()}" : $"Type \"{Text}\" into {DescribeTarget()}";

    public override WorkflowAction Build(string id) => new TypeIntoElementAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds,
        Target = BuildTarget(), Text = Text
    };

    public static TypeIntoElementEditorViewModel FromAction(TypeIntoElementAction action)
    {
        var vm = new TypeIntoElementEditorViewModel { Text = action.Text };
        vm.LoadTargetFrom(action.Target, action);
        return vm;
    }

    partial void OnTextChanged(string value) => OnPropertyChanged(nameof(Summary));
}
