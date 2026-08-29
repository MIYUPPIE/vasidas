using System.Text.Json.Serialization;
using AutomationStudio.Core.Execution;

namespace AutomationStudio.Core.Actions;

/// <summary>
/// Base type for every action a workflow can contain. The "type" discriminator below is exactly
/// the JSON action format fixed by section 9 of the design doc — this list is deliberately scoped
/// to the Phase 1 action set from section 40 (Mouse, Keyboard, Open/Close application, Wait,
/// Screenshot). Later phases add derived types here as their finders/conditions land; nothing here
/// depends on those, so Phase 1 stands on its own.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(OpenApplicationAction), "OpenApplication")]
[JsonDerivedType(typeof(CloseApplicationAction), "CloseApplication")]
[JsonDerivedType(typeof(MoveMouseAction), "MoveMouse")]
[JsonDerivedType(typeof(ClickAction), "Click")]
[JsonDerivedType(typeof(DoubleClickAction), "DoubleClick")]
[JsonDerivedType(typeof(RightClickAction), "RightClick")]
[JsonDerivedType(typeof(MouseDownAction), "MouseDown")]
[JsonDerivedType(typeof(MouseUpAction), "MouseUp")]
[JsonDerivedType(typeof(ScrollAction), "Scroll")]
[JsonDerivedType(typeof(TypeTextAction), "TypeText")]
[JsonDerivedType(typeof(PressKeyAction), "PressKey")]
[JsonDerivedType(typeof(KeyCombinationAction), "KeyCombination")]
[JsonDerivedType(typeof(WaitAction), "Wait")]
[JsonDerivedType(typeof(TakeScreenshotAction), "TakeScreenshot")]
[JsonDerivedType(typeof(FindElementAction), "FindElement")]
[JsonDerivedType(typeof(ClickElementAction), "ClickElement")]
[JsonDerivedType(typeof(TypeIntoElementAction), "TypeIntoElement")]
[JsonDerivedType(typeof(FindTextAction), "FindText")]
[JsonDerivedType(typeof(ClickTextAction), "ClickText")]
[JsonDerivedType(typeof(VerifyTextAction), "VerifyText")]
public abstract class WorkflowAction
{
    public string Id { get; init; } = Guid.NewGuid().ToString("n");
    public bool Enabled { get; init; } = true;

    /// <summary>How many times to retry after a failure, per section 15. 0 = no retry.</summary>
    public int RetryCount { get; init; }

    public int TimeoutMilliseconds { get; init; } = 30_000;

    [JsonIgnore]
    public abstract string ActionType { get; }

    /// <summary>
    /// Performs the action. Implementations should throw on failure rather than swallow errors —
    /// the engine (not the action) owns retry, timeout and result-recording policy.
    /// </summary>
    public abstract Task ExecuteAsync(ActionExecutionContext context);
}
