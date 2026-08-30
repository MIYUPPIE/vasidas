namespace AutomationStudio.Abstractions;

public enum RecordedInputEventKind
{
    MouseMove,
    MouseDown,
    MouseUp,
    MouseScroll,
    KeyDown,
    KeyUp
}

/// <summary>
/// One captured global input event (DAS.md section 25: "Mouse, Keyboard... Timing"). This is the
/// inverse of a <see cref="WorkflowAction"/> in the way <see cref="IInputRecorder"/> is the inverse
/// of <see cref="IDesktopController"/> — raw observation rather than a command to carry out.
/// <see cref="Elapsed"/> is relative to when recording started, which is all the normalizer (turning
/// this stream back into a <c>Workflow</c>) needs; wall-clock time is never meaningful on replay.
/// </summary>
public sealed record RecordedInputEvent(
    RecordedInputEventKind Kind,
    TimeSpan Elapsed,
    ScreenPoint Position,
    MouseButton? Button = null,
    int ScrollDeltaX = 0,
    int ScrollDeltaY = 0,
    Key? Key = null,
    string? Character = null);

/// <summary>
/// Captures real, global mouse/keyboard input for the Recorder/Teaching system (DAS.md section 25)
/// — observation, not injection, which is why this is a separate interface from
/// <see cref="IDesktopController"/> rather than a mode on it.
/// </summary>
public interface IInputRecorder : IDisposable
{
    /// <summary>Begins capturing global input. Throws <see cref="InvalidOperationException"/> if already recording.</summary>
    void Start();

    /// <summary>Stops capturing and returns every event captured since <see cref="Start"/>, in chronological order. Throws <see cref="InvalidOperationException"/> if not currently recording.</summary>
    IReadOnlyList<RecordedInputEvent> Stop();
}
