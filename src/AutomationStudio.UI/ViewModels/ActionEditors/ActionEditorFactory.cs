using AutomationStudio.Core.Actions;

namespace AutomationStudio.UI.ViewModels.ActionEditors;

/// <summary>Maps a persisted/loaded <see cref="WorkflowAction"/> to the editor that can display and rebuild it.</summary>
public static class ActionEditorFactory
{
    public static ActionEditorViewModel FromAction(WorkflowAction action) => action switch
    {
        OpenApplicationAction a => OpenApplicationEditorViewModel.FromAction(a),
        CloseApplicationAction a => CloseApplicationEditorViewModel.FromAction(a),
        MoveMouseAction a => MoveMouseEditorViewModel.FromAction(a),
        ClickAction a => ClickEditorViewModel.FromAction(a),
        DoubleClickAction a => DoubleClickEditorViewModel.FromAction(a),
        RightClickAction a => RightClickEditorViewModel.FromAction(a),
        MouseDownAction a => MouseDownEditorViewModel.FromAction(a),
        MouseUpAction a => MouseUpEditorViewModel.FromAction(a),
        ScrollAction a => ScrollEditorViewModel.FromAction(a),
        TypeTextAction a => TypeTextEditorViewModel.FromAction(a),
        PressKeyAction a => PressKeyEditorViewModel.FromAction(a),
        KeyCombinationAction a => KeyCombinationEditorViewModel.FromAction(a),
        WaitAction a => WaitEditorViewModel.FromAction(a),
        TakeScreenshotAction a => TakeScreenshotEditorViewModel.FromAction(a),
        _ => throw new NotSupportedException($"No editor is registered for action type '{action.GetType().Name}'.")
    };
}

/// <summary>One entry in the action palette (section 19/8): a category, a label, and how to create a fresh default editor for it.</summary>
public sealed record ActionPaletteEntry(string Category, string Label, Func<ActionEditorViewModel> CreateEditor)
{
    public static IReadOnlyList<ActionPaletteEntry> All { get; } =
    [
        new("Application", "Open Application", () => new OpenApplicationEditorViewModel()),
        new("Application", "Close Application", () => new CloseApplicationEditorViewModel()),

        new("Mouse", "Move Mouse", () => new MoveMouseEditorViewModel()),
        new("Mouse", "Click", () => new ClickEditorViewModel()),
        new("Mouse", "Double Click", () => new DoubleClickEditorViewModel()),
        new("Mouse", "Right Click", () => new RightClickEditorViewModel()),
        new("Mouse", "Mouse Down", () => new MouseDownEditorViewModel()),
        new("Mouse", "Mouse Up", () => new MouseUpEditorViewModel()),
        new("Mouse", "Scroll", () => new ScrollEditorViewModel()),

        new("Keyboard", "Type Text", () => new TypeTextEditorViewModel()),
        new("Keyboard", "Press Key", () => new PressKeyEditorViewModel()),
        new("Keyboard", "Key Combination", () => new KeyCombinationEditorViewModel()),

        new("Timing", "Wait", () => new WaitEditorViewModel()),

        new("Screen", "Take Screenshot", () => new TakeScreenshotEditorViewModel()),
    ];
}
