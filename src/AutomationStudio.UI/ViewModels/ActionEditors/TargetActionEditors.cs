using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutomationStudio.UI.ViewModels.ActionEditors;

/// <summary>
/// Shared VisualTargetSpec shape for FindTarget, ClickTarget and VerifyTarget (design doc section
/// 12's Target Resolution Engine) — every tier (native element query, OCR text, template image,
/// fallback coordinates) is editable at once, since the resolver tries whichever ones are actually
/// filled in, in that fixed priority order.
/// </summary>
public abstract partial class VisualTargetEditorViewModelBase : ActionEditorViewModel
{
    [ObservableProperty]
    private string _targetText = "";

    [ObservableProperty]
    private string _automationId = "";

    [ObservableProperty]
    private string _windowTitleContains = "";

    [ObservableProperty]
    private string _ocrText = "";

    [ObservableProperty]
    private double _ocrMinConfidence = 0.5;

    [ObservableProperty]
    private string _templateImagePath = "";

    [ObservableProperty]
    private double _templateMinConfidence = 0.8;

    [ObservableProperty]
    private bool _useFallbackCoordinates;

    [ObservableProperty]
    private int _fallbackX;

    [ObservableProperty]
    private int _fallbackY;

    protected VisualTargetSpec BuildTarget() => new()
    {
        Text = string.IsNullOrWhiteSpace(TargetText) ? null : TargetText,
        AutomationId = string.IsNullOrWhiteSpace(AutomationId) ? null : AutomationId,
        WindowTitleContains = string.IsNullOrWhiteSpace(WindowTitleContains) ? null : WindowTitleContains,
        OcrText = string.IsNullOrWhiteSpace(OcrText) ? null : OcrText,
        OcrMinConfidence = OcrMinConfidence,
        TemplateImagePath = string.IsNullOrWhiteSpace(TemplateImagePath) ? null : TemplateImagePath,
        TemplateMinConfidence = TemplateMinConfidence,
        FallbackX = UseFallbackCoordinates ? FallbackX : null,
        FallbackY = UseFallbackCoordinates ? FallbackY : null
    };

    /// <summary>The highest-priority tier that's actually filled in — matches what the resolver would try first, for the action-list summary.</summary>
    protected string DescribeTarget()
    {
        if (!string.IsNullOrWhiteSpace(TargetText) || !string.IsNullOrWhiteSpace(AutomationId) || !string.IsNullOrWhiteSpace(WindowTitleContains))
        {
            return new ElementQuery(
                string.IsNullOrWhiteSpace(TargetText) ? null : TargetText,
                string.IsNullOrWhiteSpace(AutomationId) ? null : AutomationId,
                string.IsNullOrWhiteSpace(WindowTitleContains) ? null : WindowTitleContains).ToString();
        }

        if (!string.IsNullOrWhiteSpace(OcrText))
        {
            return $"text='{OcrText}' (OCR)";
        }

        if (!string.IsNullOrWhiteSpace(TemplateImagePath))
        {
            return $"template='{TemplateImagePath}'";
        }

        if (UseFallbackCoordinates)
        {
            return $"({FallbackX}, {FallbackY})";
        }

        return "(no target set)";
    }

    protected void LoadTargetFrom(VisualTargetSpec target, WorkflowAction action)
    {
        TargetText = target.Text ?? "";
        AutomationId = target.AutomationId ?? "";
        WindowTitleContains = target.WindowTitleContains ?? "";
        OcrText = target.OcrText ?? "";
        OcrMinConfidence = target.OcrMinConfidence;
        TemplateImagePath = target.TemplateImagePath ?? "";
        TemplateMinConfidence = target.TemplateMinConfidence;
        UseFallbackCoordinates = target.FallbackX.HasValue && target.FallbackY.HasValue;
        FallbackX = target.FallbackX ?? 0;
        FallbackY = target.FallbackY ?? 0;
        LoadCommonFrom(action);
    }

    partial void OnTargetTextChanged(string value) => OnPropertyChanged(nameof(Summary));
    partial void OnAutomationIdChanged(string value) => OnPropertyChanged(nameof(Summary));
    partial void OnWindowTitleContainsChanged(string value) => OnPropertyChanged(nameof(Summary));
    partial void OnOcrTextChanged(string value) => OnPropertyChanged(nameof(Summary));
    partial void OnTemplateImagePathChanged(string value) => OnPropertyChanged(nameof(Summary));
    partial void OnUseFallbackCoordinatesChanged(bool value) => OnPropertyChanged(nameof(Summary));
    partial void OnFallbackXChanged(int value) => OnPropertyChanged(nameof(Summary));
    partial void OnFallbackYChanged(int value) => OnPropertyChanged(nameof(Summary));
}

public sealed class FindTargetEditorViewModel : VisualTargetEditorViewModelBase
{
    public override string ActionTypeName => "FindTarget";
    public override string Summary => $"Find {DescribeTarget()}";

    public override WorkflowAction Build(string id) => new FindTargetAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds,
        Target = BuildTarget()
    };

    public static FindTargetEditorViewModel FromAction(FindTargetAction action)
    {
        var vm = new FindTargetEditorViewModel();
        vm.LoadTargetFrom(action.Target, action);
        return vm;
    }
}

public partial class ClickTargetEditorViewModel : VisualTargetEditorViewModelBase
{
    [ObservableProperty]
    private MouseButton _button = MouseButton.Left;

    public static IReadOnlyList<MouseButton> AllButtons { get; } = Enum.GetValues<MouseButton>();

    public override string ActionTypeName => "ClickTarget";
    public override string Summary => $"Click {DescribeTarget()}";

    public override WorkflowAction Build(string id) => new ClickTargetAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds,
        Target = BuildTarget(), Button = Button
    };

    public static ClickTargetEditorViewModel FromAction(ClickTargetAction action)
    {
        var vm = new ClickTargetEditorViewModel { Button = action.Button };
        vm.LoadTargetFrom(action.Target, action);
        return vm;
    }

    partial void OnButtonChanged(MouseButton value) => OnPropertyChanged(nameof(Summary));
}

public sealed class VerifyTargetEditorViewModel : VisualTargetEditorViewModelBase
{
    public override string ActionTypeName => "VerifyTarget";
    public override string Summary => $"Verify {DescribeTarget()} is on screen";

    public override WorkflowAction Build(string id) => new VerifyTargetAction
    {
        Id = id, Enabled = Enabled, RetryCount = RetryCount, TimeoutMilliseconds = TimeoutMilliseconds,
        Target = BuildTarget()
    };

    public static VerifyTargetEditorViewModel FromAction(VerifyTargetAction action)
    {
        var vm = new VerifyTargetEditorViewModel();
        vm.LoadTargetFrom(action.Target, action);
        return vm;
    }
}
