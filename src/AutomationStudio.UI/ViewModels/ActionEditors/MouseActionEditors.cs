using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutomationStudio.UI.ViewModels.ActionEditors;

public partial class MoveMouseEditorViewModel : ActionEditorViewModel
{
    [ObservableProperty]
    private int _x;

    [ObservableProperty]
    private int _y;

    public override string ActionTypeName => "MoveMouse";
    public override string Summary => $"Move to ({X}, {Y})";

    public override WorkflowAction Build(string id) => new MoveMouseAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds, X = X, Y = Y
    };

    public static MoveMouseEditorViewModel FromAction(MoveMouseAction action)
    {
        var vm = new MoveMouseEditorViewModel { X = action.X, Y = action.Y };
        vm.LoadCommonFrom(action);
        return vm;
    }

    partial void OnXChanged(int value) => OnPropertyChanged(nameof(Summary));
    partial void OnYChanged(int value) => OnPropertyChanged(nameof(Summary));
}

/// <summary>Shared X/Y/Button shape for Click, MouseDown and MouseUp — the UI always sets a concrete
/// position (unlike the JSON format, which allows a null position meaning "wherever the cursor already is";
/// that's a scripting-only case simple enough to hand-edit in JSON without needing a GUI toggle for it).</summary>
public abstract partial class PointButtonEditorViewModelBase : ActionEditorViewModel
{
    [ObservableProperty]
    private int _x;

    [ObservableProperty]
    private int _y;

    [ObservableProperty]
    private MouseButton _button = MouseButton.Left;

    public static IReadOnlyList<MouseButton> AllButtons { get; } = Enum.GetValues<MouseButton>();

    public override string Summary => $"{ActionTypeName} ({Button}) at ({X}, {Y})";

    protected void LoadPointButtonFrom(int x, int y, MouseButton button, WorkflowAction action)
    {
        X = x;
        Y = y;
        Button = button;
        LoadCommonFrom(action);
    }

    partial void OnXChanged(int value) => OnPropertyChanged(nameof(Summary));
    partial void OnYChanged(int value) => OnPropertyChanged(nameof(Summary));
    partial void OnButtonChanged(MouseButton value) => OnPropertyChanged(nameof(Summary));
}

public sealed class ClickEditorViewModel : PointButtonEditorViewModelBase
{
    public override string ActionTypeName => "Click";

    public override WorkflowAction Build(string id) => new ClickAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds,
        X = X, Y = Y, Button = Button
    };

    public static ClickEditorViewModel FromAction(ClickAction action)
    {
        var vm = new ClickEditorViewModel();
        vm.LoadPointButtonFrom(action.X ?? 0, action.Y ?? 0, action.Button, action);
        return vm;
    }
}

public sealed class MouseDownEditorViewModel : PointButtonEditorViewModelBase
{
    public override string ActionTypeName => "MouseDown";

    public override WorkflowAction Build(string id) => new MouseDownAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds,
        X = X, Y = Y, Button = Button
    };

    public static MouseDownEditorViewModel FromAction(MouseDownAction action)
    {
        var vm = new MouseDownEditorViewModel();
        vm.LoadPointButtonFrom(action.X ?? 0, action.Y ?? 0, action.Button, action);
        return vm;
    }
}

public sealed class MouseUpEditorViewModel : PointButtonEditorViewModelBase
{
    public override string ActionTypeName => "MouseUp";

    public override WorkflowAction Build(string id) => new MouseUpAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds,
        X = X, Y = Y, Button = Button
    };

    public static MouseUpEditorViewModel FromAction(MouseUpAction action)
    {
        var vm = new MouseUpEditorViewModel();
        vm.LoadPointButtonFrom(action.X ?? 0, action.Y ?? 0, action.Button, action);
        return vm;
    }
}

/// <summary>Shared X/Y shape for DoubleClick and RightClick.</summary>
public abstract partial class PointOnlyEditorViewModelBase : ActionEditorViewModel
{
    [ObservableProperty]
    private int _x;

    [ObservableProperty]
    private int _y;

    public override string Summary => $"{ActionTypeName} at ({X}, {Y})";

    protected void LoadPointFrom(int x, int y, WorkflowAction action)
    {
        X = x;
        Y = y;
        LoadCommonFrom(action);
    }

    partial void OnXChanged(int value) => OnPropertyChanged(nameof(Summary));
    partial void OnYChanged(int value) => OnPropertyChanged(nameof(Summary));
}

public sealed class DoubleClickEditorViewModel : PointOnlyEditorViewModelBase
{
    public override string ActionTypeName => "DoubleClick";

    public override WorkflowAction Build(string id) => new DoubleClickAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds, X = X, Y = Y
    };

    public static DoubleClickEditorViewModel FromAction(DoubleClickAction action)
    {
        var vm = new DoubleClickEditorViewModel();
        vm.LoadPointFrom(action.X ?? 0, action.Y ?? 0, action);
        return vm;
    }
}

public sealed class RightClickEditorViewModel : PointOnlyEditorViewModelBase
{
    public override string ActionTypeName => "RightClick";

    public override WorkflowAction Build(string id) => new RightClickAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds, X = X, Y = Y
    };

    public static RightClickEditorViewModel FromAction(RightClickAction action)
    {
        var vm = new RightClickEditorViewModel();
        vm.LoadPointFrom(action.X ?? 0, action.Y ?? 0, action);
        return vm;
    }
}

public partial class ScrollEditorViewModel : ActionEditorViewModel
{
    [ObservableProperty]
    private int _deltaX;

    [ObservableProperty]
    private int _deltaY;

    public override string ActionTypeName => "Scroll";
    public override string Summary => $"Scroll ({DeltaX}, {DeltaY})";

    public override WorkflowAction Build(string id) => new ScrollAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds,
        DeltaX = DeltaX, DeltaY = DeltaY
    };

    public static ScrollEditorViewModel FromAction(ScrollAction action)
    {
        var vm = new ScrollEditorViewModel { DeltaX = action.DeltaX, DeltaY = action.DeltaY };
        vm.LoadCommonFrom(action);
        return vm;
    }

    partial void OnDeltaXChanged(int value) => OnPropertyChanged(nameof(Summary));
    partial void OnDeltaYChanged(int value) => OnPropertyChanged(nameof(Summary));
}
