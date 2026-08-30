using AutomationStudio.Core.Actions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutomationStudio.UI.ViewModels.ActionEditors;

public partial class OpenApplicationEditorViewModel : ActionEditorViewModel
{
    [ObservableProperty]
    private string _path = "";

    [ObservableProperty]
    private string? _arguments;

    public override string ActionTypeName => "OpenApplication";
    public override string Summary => string.IsNullOrWhiteSpace(Path) ? "(no path set)" : Path;

    public override WorkflowAction Build(string id) => new OpenApplicationAction
    {
        Id = id,
        Enabled = Enabled,
        RetryCount = RetryCount,
        TimeoutMilliseconds = TimeoutMilliseconds,
        Path = Path,
        Arguments = string.IsNullOrWhiteSpace(Arguments) ? null : Arguments
    };

    public static OpenApplicationEditorViewModel FromAction(OpenApplicationAction action)
    {
        var vm = new OpenApplicationEditorViewModel { Path = action.Path, Arguments = action.Arguments };
        vm.LoadCommonFrom(action);
        return vm;
    }

    partial void OnPathChanged(string value) => OnPropertyChanged(nameof(Summary));
}

public partial class CloseApplicationEditorViewModel : ActionEditorViewModel
{
    [ObservableProperty]
    private string _processName = "";

    public override string ActionTypeName => "CloseApplication";
    public override string Summary => string.IsNullOrWhiteSpace(ProcessName) ? "(no process set)" : ProcessName;

    public override WorkflowAction Build(string id) => new CloseApplicationAction
    {
        Id = id,
        Enabled = Enabled,
        RetryCount = RetryCount,
        TimeoutMilliseconds = TimeoutMilliseconds,
        ProcessName = ProcessName
    };

    public static CloseApplicationEditorViewModel FromAction(CloseApplicationAction action)
    {
        var vm = new CloseApplicationEditorViewModel { ProcessName = action.ProcessName };
        vm.LoadCommonFrom(action);
        return vm;
    }

    partial void OnProcessNameChanged(string value) => OnPropertyChanged(nameof(Summary));
}

public partial class FocusWindowEditorViewModel : ActionEditorViewModel
{
    [ObservableProperty]
    private string _titleContains = "";

    public override string ActionTypeName => "FocusWindow";
    public override string Summary => string.IsNullOrWhiteSpace(TitleContains) ? "(no title set)" : $"Focus window containing \"{TitleContains}\"";

    public override WorkflowAction Build(string id) => new FocusWindowAction
    {
        Id = id,
        Enabled = Enabled,
        RetryCount = RetryCount,
        TimeoutMilliseconds = TimeoutMilliseconds,
        TitleContains = TitleContains
    };

    public static FocusWindowEditorViewModel FromAction(FocusWindowAction action)
    {
        var vm = new FocusWindowEditorViewModel { TitleContains = action.TitleContains };
        vm.LoadCommonFrom(action);
        return vm;
    }

    partial void OnTitleContainsChanged(string value) => OnPropertyChanged(nameof(Summary));
}

public partial class FocusApplicationEditorViewModel : ActionEditorViewModel
{
    [ObservableProperty]
    private string _processName = "";

    public override string ActionTypeName => "FocusApplication";
    public override string Summary => string.IsNullOrWhiteSpace(ProcessName) ? "(no process set)" : $"Focus \"{ProcessName}\"";

    public override WorkflowAction Build(string id) => new FocusApplicationAction
    {
        Id = id,
        Enabled = Enabled,
        RetryCount = RetryCount,
        TimeoutMilliseconds = TimeoutMilliseconds,
        ProcessName = ProcessName
    };

    public static FocusApplicationEditorViewModel FromAction(FocusApplicationAction action)
    {
        var vm = new FocusApplicationEditorViewModel { ProcessName = action.ProcessName };
        vm.LoadCommonFrom(action);
        return vm;
    }

    partial void OnProcessNameChanged(string value) => OnPropertyChanged(nameof(Summary));
}
