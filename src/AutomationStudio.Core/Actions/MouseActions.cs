using System.Text.Json.Serialization;
using AutomationStudio.Abstractions;
using AutomationStudio.Core.Execution;

namespace AutomationStudio.Core.Actions;

public sealed class MoveMouseAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "MoveMouse";

    public required int X { get; init; }
    public required int Y { get; init; }

    public override Task ExecuteAsync(ActionExecutionContext context) =>
        context.DesktopController.MoveMouseAsync(new ScreenPoint(X, Y), context.CancellationToken);
}

public sealed class ClickAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "Click";

    public int? X { get; init; }
    public int? Y { get; init; }
    public MouseButton Button { get; init; } = MouseButton.Left;

    public override Task ExecuteAsync(ActionExecutionContext context) =>
        context.DesktopController.ClickAsync(Button, ToPoint(X, Y), context.CancellationToken);

    internal static ScreenPoint? ToPoint(int? x, int? y) =>
        x.HasValue && y.HasValue ? new ScreenPoint(x.Value, y.Value) : null;
}

public sealed class DoubleClickAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "DoubleClick";

    public int? X { get; init; }
    public int? Y { get; init; }

    public override Task ExecuteAsync(ActionExecutionContext context) =>
        context.DesktopController.DoubleClickAsync(ClickAction.ToPoint(X, Y), context.CancellationToken);
}

public sealed class RightClickAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "RightClick";

    public int? X { get; init; }
    public int? Y { get; init; }

    public override Task ExecuteAsync(ActionExecutionContext context) =>
        context.DesktopController.ClickAsync(MouseButton.Right, ClickAction.ToPoint(X, Y), context.CancellationToken);
}

public sealed class MouseDownAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "MouseDown";

    public MouseButton Button { get; init; } = MouseButton.Left;
    public int? X { get; init; }
    public int? Y { get; init; }

    public override Task ExecuteAsync(ActionExecutionContext context) =>
        context.DesktopController.MouseDownAsync(Button, ClickAction.ToPoint(X, Y), context.CancellationToken);
}

public sealed class MouseUpAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "MouseUp";

    public MouseButton Button { get; init; } = MouseButton.Left;
    public int? X { get; init; }
    public int? Y { get; init; }

    public override Task ExecuteAsync(ActionExecutionContext context) =>
        context.DesktopController.MouseUpAsync(Button, ClickAction.ToPoint(X, Y), context.CancellationToken);
}

public sealed class ScrollAction : WorkflowAction
{
    [JsonIgnore]
    public override string ActionType => "Scroll";

    public int DeltaX { get; init; }
    public int DeltaY { get; init; }

    public override Task ExecuteAsync(ActionExecutionContext context) =>
        context.DesktopController.ScrollAsync(DeltaX, DeltaY, context.CancellationToken);
}
