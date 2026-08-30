using System.Text;
using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;

namespace AutomationStudio.Recorder.Normalization;

/// <summary>
/// The "Understand → Normalize" step of DAS.md section 25's Record → Observe → Understand →
/// Normalize → Generate workflow pipeline: turns a raw <see cref="RecordedInputEvent"/> stream into
/// the same <see cref="WorkflowAction"/> types a human would pick building the workflow by hand.
/// Deliberately pure/deterministic (same input always produces the same actions) and has no
/// dependency on any live automation backend, so it's fully unit-testable without touching X11/Win32
/// at all — only <see cref="AutomationStudio.Linux.X11.X11InputRecorder"/> /
/// <see cref="AutomationStudio.Windows.Input.WindowsInputRecorder"/> need real input hardware.
/// </summary>
public static class ActionNormalizer
{
    /// <summary>Down-then-up at (nearly) the same spot within this long counts as a click rather than a drag.</summary>
    private static readonly TimeSpan ClickMaxDuration = TimeSpan.FromMilliseconds(500);

    /// <summary>Two clicks this close together (and at the same spot) merge into one DoubleClick.</summary>
    private static readonly TimeSpan DoubleClickMaxGap = TimeSpan.FromMilliseconds(400);

    private const int ClickPositionTolerance = 3;

    /// <summary>Consecutive wheel ticks this close together merge into one Scroll action instead of one action per notch.</summary>
    private static readonly TimeSpan ScrollCoalesceGap = TimeSpan.FromMilliseconds(300);

    /// <summary>A typing pause longer than this starts a new TypeText action rather than continuing the current one.</summary>
    private static readonly TimeSpan TypingCoalesceGap = TimeSpan.FromMilliseconds(1500);

    /// <summary>A gap between actions longer than this becomes an explicit Wait — DAS.md section 25's "Timing" signal, without which replay would fire everything back-to-back regardless of how long the user actually paused (e.g. waiting for a dialog to open).</summary>
    private static readonly TimeSpan WaitThreshold = TimeSpan.FromMilliseconds(500);

    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(10);

    private static readonly HashSet<Key> ComboOnlyModifiers =
    [
        Key.LeftControl, Key.RightControl, Key.LeftAlt, Key.RightAlt, Key.LeftSuper, Key.RightSuper
    ];

    private static readonly HashSet<Key> ShiftKeys = [Key.LeftShift, Key.RightShift];

    public static IReadOnlyList<WorkflowAction> Normalize(IReadOnlyList<RecordedInputEvent> events)
    {
        var actions = new List<WorkflowAction>();
        var heldModifiers = new List<Key>();
        var typingBuffer = new StringBuilder();

        // typingStartedAt/typingLastCharAt track the buffer currently being accumulated;
        // lastActionEndedAt tracks the end of whatever action was most recently actually committed
        // to `actions` (a click, a drag's down/up, a scroll, a press/combo, or a flushed TypeText) —
        // kept deliberately separate so the "should I start a new TypeText buffer" gap check (against
        // the last character typed) never gets confused with the "should I insert a Wait before the
        // next action" gap check (against the last committed action).
        TimeSpan? typingStartedAt = null;
        TimeSpan? typingLastCharAt = null;
        TimeSpan? lastActionEndedAt = null;

        void InsertWaitIfNeeded(TimeSpan startsAt)
        {
            if (lastActionEndedAt is not { } last)
            {
                return;
            }

            var gap = startsAt - last;
            if (gap > WaitThreshold)
            {
                var capped = gap > MaxWait ? MaxWait : gap;
                actions.Add(new WaitAction { Milliseconds = (int)capped.TotalMilliseconds });
            }
        }

        void FlushTyping()
        {
            if (typingBuffer.Length == 0 || typingStartedAt is not { } startedAt)
            {
                return;
            }

            InsertWaitIfNeeded(startedAt);
            actions.Add(new TypeTextAction { Text = typingBuffer.ToString() });
            typingBuffer.Clear();
            lastActionEndedAt = typingLastCharAt;
            typingStartedAt = null;
            typingLastCharAt = null;
        }

        for (var i = 0; i < events.Count; i++)
        {
            var evt = events[i];

            switch (evt.Kind)
            {
                case RecordedInputEventKind.MouseMove:
                    continue; // a raw motion trail carries no replay-relevant signal on its own.

                case RecordedInputEventKind.MouseDown:
                {
                    FlushTyping();
                    var (upIndex, isClick) = FindMatchingUp(events, i, evt);

                    if (!isClick)
                    {
                        InsertWaitIfNeeded(evt.Elapsed);
                        actions.Add(new MouseDownAction { X = evt.Position.X, Y = evt.Position.Y, Button = evt.Button ?? MouseButton.Left });
                        lastActionEndedAt = evt.Elapsed;
                        continue; // a drag: its MouseUp (wherever it lands) is handled when the loop reaches it.
                    }

                    var button = evt.Button ?? MouseButton.Left;
                    var (doubleUpIndex, isDouble) = button == MouseButton.Left
                        ? TryFindDoubleClick(events, upIndex, evt)
                        : (upIndex, false);

                    InsertWaitIfNeeded(evt.Elapsed);
                    if (isDouble)
                    {
                        actions.Add(new DoubleClickAction { X = evt.Position.X, Y = evt.Position.Y });
                        lastActionEndedAt = events[doubleUpIndex].Elapsed;
                        i = doubleUpIndex;
                    }
                    else
                    {
                        actions.Add(new ClickAction { X = evt.Position.X, Y = evt.Position.Y, Button = button });
                        lastActionEndedAt = events[upIndex].Elapsed;
                        i = upIndex;
                    }

                    continue;
                }

                case RecordedInputEventKind.MouseUp:
                    // Only reached for an up with no click merge above — the end of a drag.
                    FlushTyping();
                    InsertWaitIfNeeded(evt.Elapsed);
                    actions.Add(new MouseUpAction { X = evt.Position.X, Y = evt.Position.Y, Button = evt.Button ?? MouseButton.Left });
                    lastActionEndedAt = evt.Elapsed;
                    continue;

                case RecordedInputEventKind.MouseScroll:
                {
                    FlushTyping();
                    InsertWaitIfNeeded(evt.Elapsed);
                    var (endIndex, deltaX, deltaY) = CoalesceScroll(events, i);
                    actions.Add(new ScrollAction { DeltaX = deltaX, DeltaY = deltaY });
                    lastActionEndedAt = events[endIndex].Elapsed;
                    i = endIndex;
                    continue;
                }

                case RecordedInputEventKind.KeyDown:
                {
                    if (evt.Key is { } heldKey && (ComboOnlyModifiers.Contains(heldKey) || ShiftKeys.Contains(heldKey)))
                    {
                        if (!heldModifiers.Contains(heldKey))
                        {
                            heldModifiers.Add(heldKey);
                        }

                        continue;
                    }

                    var isNamedKey = evt.Character is null;
                    var comboTriggered = isNamedKey
                        ? heldModifiers.Count > 0
                        : heldModifiers.Exists(m => !ShiftKeys.Contains(m));

                    if (comboTriggered)
                    {
                        FlushTyping();
                        if (evt.Key is { } thisKey)
                        {
                            InsertWaitIfNeeded(evt.Elapsed);
                            actions.Add(new KeyCombinationAction { Keys = [.. heldModifiers, thisKey] });
                            lastActionEndedAt = evt.Elapsed;
                        }
                        // else: the held-down key has no known Key mapping — nothing sensible to emit for it.

                        continue;
                    }

                    if (isNamedKey)
                    {
                        FlushTyping();
                        if (evt.Key is { } namedKey)
                        {
                            InsertWaitIfNeeded(evt.Elapsed);
                            actions.Add(new PressKeyAction { Key = namedKey });
                            lastActionEndedAt = evt.Elapsed;
                        }

                        continue;
                    }

                    // Plain typing: accumulate into the current TypeText action, starting a new one
                    // if there's been too long a pause since the last character. The Wait this pause
                    // deserves is inserted once, inside FlushTyping (against typingStartedAt) —
                    // not speculatively here, which would double-count it once the buffer is flushed.
                    if (typingBuffer.Length > 0 && typingLastCharAt is { } lastChar && evt.Elapsed - lastChar > TypingCoalesceGap)
                    {
                        FlushTyping();
                    }

                    if (typingBuffer.Length == 0)
                    {
                        typingStartedAt = evt.Elapsed;
                    }

                    typingBuffer.Append(evt.Character);
                    typingLastCharAt = evt.Elapsed;
                    continue;
                }

                case RecordedInputEventKind.KeyUp:
                    if (evt.Key is { } releasedKey)
                    {
                        heldModifiers.Remove(releasedKey);
                    }

                    continue;
            }
        }

        FlushTyping();
        return actions;
    }

    /// <summary>Looks ahead from a MouseDown for the matching MouseUp (same button). Returns IsClick=false if the recording ends first, another button/key event intervenes, or the up lands too far from the down (a drag) or too long after it.</summary>
    private static (int UpIndex, bool IsClick) FindMatchingUp(IReadOnlyList<RecordedInputEvent> events, int downIndex, RecordedInputEvent down)
    {
        for (var j = downIndex + 1; j < events.Count; j++)
        {
            var e = events[j];
            if (e.Elapsed - down.Elapsed > ClickMaxDuration)
            {
                return (downIndex, false);
            }

            if (e.Kind == RecordedInputEventKind.MouseUp && e.Button == down.Button)
            {
                var closeEnough = Math.Abs(e.Position.X - down.Position.X) <= ClickPositionTolerance &&
                                   Math.Abs(e.Position.Y - down.Position.Y) <= ClickPositionTolerance;
                return (j, closeEnough);
            }

            if (e.Kind is RecordedInputEventKind.MouseDown or RecordedInputEventKind.KeyDown or RecordedInputEventKind.KeyUp)
            {
                return (downIndex, false); // something else happened mid-press — not a simple click.
            }
        }

        return (downIndex, false); // recording ended mid-press.
    }

    /// <summary>Looks ahead from the first click's MouseUp for a second click at (nearly) the same spot within <see cref="DoubleClickMaxGap"/>.</summary>
    private static (int UpIndex, bool IsDouble) TryFindDoubleClick(IReadOnlyList<RecordedInputEvent> events, int firstUpIndex, RecordedInputEvent down)
    {
        var firstUp = events[firstUpIndex];
        for (var j = firstUpIndex + 1; j < events.Count; j++)
        {
            var e = events[j];
            if (e.Elapsed - firstUp.Elapsed > DoubleClickMaxGap)
            {
                return (firstUpIndex, false);
            }

            if (e.Kind == RecordedInputEventKind.MouseDown && e.Button == down.Button)
            {
                var closeEnough = Math.Abs(e.Position.X - down.Position.X) <= ClickPositionTolerance &&
                                   Math.Abs(e.Position.Y - down.Position.Y) <= ClickPositionTolerance;
                if (!closeEnough)
                {
                    return (firstUpIndex, false);
                }

                var (secondUpIndex, isSecondClick) = FindMatchingUp(events, j, e);
                return isSecondClick ? (secondUpIndex, true) : (firstUpIndex, false);
            }

            if (e.Kind is RecordedInputEventKind.KeyDown or RecordedInputEventKind.KeyUp or RecordedInputEventKind.MouseScroll)
            {
                return (firstUpIndex, false);
            }
        }

        return (firstUpIndex, false);
    }

    private static (int EndIndex, int DeltaX, int DeltaY) CoalesceScroll(IReadOnlyList<RecordedInputEvent> events, int startIndex)
    {
        var deltaX = 0;
        var deltaY = 0;
        var endIndex = startIndex;

        for (var j = startIndex; j < events.Count; j++)
        {
            var e = events[j];
            if (e.Kind != RecordedInputEventKind.MouseScroll)
            {
                break;
            }

            if (j > startIndex && e.Elapsed - events[j - 1].Elapsed > ScrollCoalesceGap)
            {
                break;
            }

            deltaX += e.ScrollDeltaX;
            deltaY += e.ScrollDeltaY;
            endIndex = j;
        }

        return (endIndex, deltaX, deltaY);
    }
}
