using AutomationStudio.Core.Actions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutomationStudio.UI.ViewModels.ActionEditors;

public partial class WaitEditorViewModel : ActionEditorViewModel
{
    [ObservableProperty]
    private int _milliseconds = 1000;

    public override string ActionTypeName => "Wait";
    public override string Summary => $"Wait {Milliseconds}ms";

    public override WorkflowAction Build(string id) => new WaitAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds, Milliseconds = Milliseconds
    };

    public static WaitEditorViewModel FromAction(WaitAction action)
    {
        var vm = new WaitEditorViewModel { Milliseconds = action.Milliseconds };
        vm.LoadCommonFrom(action);
        return vm;
    }

    partial void OnMillisecondsChanged(int value) => OnPropertyChanged(nameof(Summary));
}

public partial class TakeScreenshotEditorViewModel : ActionEditorViewModel
{
    [ObservableProperty]
    private string? _savePath;

    public override string ActionTypeName => "TakeScreenshot";
    public override string Summary => string.IsNullOrWhiteSpace(SavePath) ? "Screenshot (not saved to disk)" : $"Screenshot → {SavePath}";

    public override WorkflowAction Build(string id) => new TakeScreenshotAction
    {
        Id = id,
        Enabled = Enabled,
        RetryCount = RetryCount,
        TimeoutMilliseconds = TimeoutMilliseconds,
        SavePath = string.IsNullOrWhiteSpace(SavePath) ? null : SavePath
    };

    public static TakeScreenshotEditorViewModel FromAction(TakeScreenshotAction action)
    {
        var vm = new TakeScreenshotEditorViewModel { SavePath = action.SavePath };
        vm.LoadCommonFrom(action);
        return vm;
    }

    partial void OnSavePathChanged(string? value) => OnPropertyChanged(nameof(Summary));
}
