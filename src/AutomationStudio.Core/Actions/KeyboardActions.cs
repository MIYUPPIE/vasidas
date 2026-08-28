using AutomationStudio.Abstractions;
using AutomationStudio.Core.Execution;

namespace AutomationStudio.Core.Actions;

public sealed class TypeTextAction : WorkflowAction
{
    public override string ActionType => "TypeText";

    public required string Text { get; init; }

    public override Task ExecuteAsync(ActionExecutionContext context) =>
        context.DesktopController.TypeTextAsync(Text, context.CancellationToken);
}

public sealed class PressKeyAction : WorkflowAction
{
    public override string ActionType => "PressKey";

    public required Key Key { get; init; }

    public override Task ExecuteAsync(ActionExecutionContext context) =>
        context.DesktopController.PressKeyAsync(Key, context.CancellationToken);
}

public sealed class KeyCombinationAction : WorkflowAction
{
    public override string ActionType => "KeyCombination";

    public required IReadOnlyList<Key> Keys { get; init; }

    public override Task ExecuteAsync(ActionExecutionContext context) =>
        context.DesktopController.KeyCombinationAsync(Keys, context.CancellationToken);
}
