using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutomationStudio.UI.ViewModels.ActionEditors;

/// <summary>Shared text/confidence/region shape for FindText, ClickText and VerifyText (section 40 Phase 4).</summary>
public abstract partial class TextSearchEditorViewModelBase : ActionEditorViewModel
{
    [ObservableProperty]
    private string _text = "";

    [ObservableProperty]
    private double _minConfidence = 0.5;

    [ObservableProperty]
    private bool _useRegion;

    [ObservableProperty]
    private int _regionX;

    [ObservableProperty]
    private int _regionY;

    [ObservableProperty]
    private int _regionWidth = 200;

    [ObservableProperty]
    private int _regionHeight = 100;

    protected ScreenRect? BuildRegion() => UseRegion ? new ScreenRect(RegionX, RegionY, RegionWidth, RegionHeight) : null;

    protected void LoadTextSearchFrom(string text, double minConfidence, ScreenRect? region, WorkflowAction action)
    {
        Text = text;
        MinConfidence = minConfidence;
        UseRegion = region.HasValue;
        RegionX = region?.X ?? 0;
        RegionY = region?.Y ?? 0;
        RegionWidth = region?.Width ?? 200;
        RegionHeight = region?.Height ?? 100;
        LoadCommonFrom(action);
    }

    partial void OnTextChanged(string value) => OnPropertyChanged(nameof(Summary));
}

public sealed class FindTextEditorViewModel : TextSearchEditorViewModelBase
{
    public override string ActionTypeName => "FindText";
    public override string Summary => string.IsNullOrEmpty(Text) ? "(no text set)" : $"Find \"{Text}\"";

    public override WorkflowAction Build(string id) => new FindTextAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds,
        Text = Text, MinConfidence = MinConfidence, Region = BuildRegion()
    };

    public static FindTextEditorViewModel FromAction(FindTextAction action)
    {
        var vm = new FindTextEditorViewModel();
        vm.LoadTextSearchFrom(action.Text, action.MinConfidence, action.Region, action);
        return vm;
    }
}

public partial class ClickTextEditorViewModel : TextSearchEditorViewModelBase
{
    [ObservableProperty]
    private MouseButton _button = MouseButton.Left;

    public static IReadOnlyList<MouseButton> AllButtons { get; } = Enum.GetValues<MouseButton>();

    public override string ActionTypeName => "ClickText";
    public override string Summary => string.IsNullOrEmpty(Text) ? "(no text set)" : $"Click \"{Text}\"";

    public override WorkflowAction Build(string id) => new ClickTextAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds,
        Text = Text, MinConfidence = MinConfidence, Region = BuildRegion(), Button = Button
    };

    public static ClickTextEditorViewModel FromAction(ClickTextAction action)
    {
        var vm = new ClickTextEditorViewModel { Button = action.Button };
        vm.LoadTextSearchFrom(action.Text, action.MinConfidence, action.Region, action);
        return vm;
    }

    partial void OnButtonChanged(MouseButton value) => OnPropertyChanged(nameof(Summary));
}

public sealed class VerifyTextEditorViewModel : TextSearchEditorViewModelBase
{
    public override string ActionTypeName => "VerifyText";
    public override string Summary => string.IsNullOrEmpty(Text) ? "(no text set)" : $"Verify \"{Text}\" is on screen";

    public override WorkflowAction Build(string id) => new VerifyTextAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds,
        Text = Text, MinConfidence = MinConfidence, Region = BuildRegion()
    };

    public static VerifyTextEditorViewModel FromAction(VerifyTextAction action)
    {
        var vm = new VerifyTextEditorViewModel();
        vm.LoadTextSearchFrom(action.Text, action.MinConfidence, action.Region, action);
        return vm;
    }
}
