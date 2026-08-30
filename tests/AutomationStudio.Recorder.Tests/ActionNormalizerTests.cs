using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;
using AutomationStudio.Recorder.Normalization;

namespace AutomationStudio.Recorder.Tests;

/// <summary>
/// Pure-logic tests for the "Understand → Normalize" step of DAS.md section 25's recorder pipeline
/// — no input hardware, no display, entirely deterministic input → output.
/// </summary>
public class ActionNormalizerTests
{
    private static RecordedInputEvent Move(int ms, int x, int y) =>
        new(RecordedInputEventKind.MouseMove, TimeSpan.FromMilliseconds(ms), new ScreenPoint(x, y));

    private static RecordedInputEvent Down(int ms, int x, int y, MouseButton button = MouseButton.Left) =>
        new(RecordedInputEventKind.MouseDown, TimeSpan.FromMilliseconds(ms), new ScreenPoint(x, y), Button: button);

    private static RecordedInputEvent Up(int ms, int x, int y, MouseButton button = MouseButton.Left) =>
        new(RecordedInputEventKind.MouseUp, TimeSpan.FromMilliseconds(ms), new ScreenPoint(x, y), Button: button);

    private static RecordedInputEvent Scroll(int ms, int deltaY) =>
        new(RecordedInputEventKind.MouseScroll, TimeSpan.FromMilliseconds(ms), default, ScrollDeltaY: deltaY);

    private static RecordedInputEvent KeyDown(int ms, Key? key, string? character) =>
        new(RecordedInputEventKind.KeyDown, TimeSpan.FromMilliseconds(ms), default, Key: key, Character: character);

    private static RecordedInputEvent KeyUp(int ms, Key? key) =>
        new(RecordedInputEventKind.KeyUp, TimeSpan.FromMilliseconds(ms), default, Key: key);

    [Fact]
    public void Normalize_MouseMoveOnly_ProducesNoActions()
    {
        var actions = ActionNormalizer.Normalize([Move(0, 10, 10), Move(50, 20, 20), Move(100, 30, 30)]);

        Assert.Empty(actions);
    }

    [Fact]
    public void Normalize_ClickDownThenUpAtSameSpot_ProducesOneClickAction()
    {
        var events = new[] { Down(0, 300, 200), Up(50, 300, 200) };

        var actions = ActionNormalizer.Normalize(events);

        var click = Assert.Single(actions);
        var typed = Assert.IsType<ClickAction>(click);
        Assert.Equal(300, typed.X);
        Assert.Equal(200, typed.Y);
        Assert.Equal(MouseButton.Left, typed.Button);
    }

    [Fact]
    public void Normalize_RightClick_ProducesClickActionWithRightButton()
    {
        var events = new[] { Down(0, 50, 50, MouseButton.Right), Up(50, 50, 50, MouseButton.Right) };

        var actions = ActionNormalizer.Normalize(events);

        var click = Assert.IsType<ClickAction>(Assert.Single(actions));
        Assert.Equal(MouseButton.Right, click.Button);
    }

    [Fact]
    public void Normalize_DownMoveUpAtADistantSpot_ProducesADragAsSeparateDownAndUp()
    {
        var events = new[] { Down(0, 10, 10), Move(50, 100, 100), Up(100, 500, 500) };

        var actions = ActionNormalizer.Normalize(events);

        Assert.Equal(2, actions.Count);
        var down = Assert.IsType<MouseDownAction>(actions[0]);
        Assert.Equal(10, down.X);
        Assert.Equal(10, down.Y);
        var up = Assert.IsType<MouseUpAction>(actions[1]);
        Assert.Equal(500, up.X);
        Assert.Equal(500, up.Y);
    }

    [Fact]
    public void Normalize_TwoLeftClicksCloseTogetherAtTheSameSpot_ProducesOneDoubleClickAction()
    {
        var events = new[] { Down(0, 300, 200), Up(50, 300, 200), Down(150, 300, 200), Up(200, 300, 200) };

        var actions = ActionNormalizer.Normalize(events);

        var doubleClick = Assert.IsType<DoubleClickAction>(Assert.Single(actions));
        Assert.Equal(300, doubleClick.X);
        Assert.Equal(200, doubleClick.Y);
    }

    [Fact]
    public void Normalize_TwoClicksFarApartInTime_ProducesTwoSeparateClickActions()
    {
        var events = new[] { Down(0, 300, 200), Up(50, 300, 200), Down(3000, 300, 200), Up(3050, 300, 200) };

        var actions = ActionNormalizer.Normalize(events);

        // The first click, a Wait for the 2.9s gap, then the second click.
        Assert.Equal(3, actions.Count);
        Assert.IsType<ClickAction>(actions[0]);
        Assert.IsType<WaitAction>(actions[1]);
        Assert.IsType<ClickAction>(actions[2]);
    }

    [Fact]
    public void Normalize_ConsecutiveScrollTicks_CoalesceIntoOneScrollAction()
    {
        var events = new[] { Scroll(0, 1), Scroll(50, 1), Scroll(100, 1) };

        var actions = ActionNormalizer.Normalize(events);

        var scroll = Assert.IsType<ScrollAction>(Assert.Single(actions));
        Assert.Equal(3, scroll.DeltaY);
        Assert.Equal(0, scroll.DeltaX);
    }

    [Fact]
    public void Normalize_TypedCharacters_ProduceOneTypeTextAction()
    {
        var events = new[]
        {
            KeyDown(0, Key.H, "h"), KeyUp(30, Key.H),
            KeyDown(60, Key.I, "i"), KeyUp(90, Key.I),
        };

        var actions = ActionNormalizer.Normalize(events);

        var typeText = Assert.IsType<TypeTextAction>(Assert.Single(actions));
        Assert.Equal("hi", typeText.Text);
    }

    [Fact]
    public void Normalize_TypingWithALongPause_ProducesTwoTypeTextActionsWithAWaitBetween()
    {
        var events = new[]
        {
            KeyDown(0, Key.H, "h"), KeyUp(30, Key.H),
            KeyDown(5000, Key.I, "i"), KeyUp(5030, Key.I),
        };

        var actions = ActionNormalizer.Normalize(events);

        Assert.Equal(3, actions.Count);
        Assert.Equal("h", Assert.IsType<TypeTextAction>(actions[0]).Text);
        Assert.IsType<WaitAction>(actions[1]);
        Assert.Equal("i", Assert.IsType<TypeTextAction>(actions[2]).Text);
    }

    [Fact]
    public void Normalize_NamedKeyPress_ProducesPressKeyAction()
    {
        var events = new[] { KeyDown(0, Key.Enter, null), KeyUp(30, Key.Enter) };

        var actions = ActionNormalizer.Normalize(events);

        var press = Assert.IsType<PressKeyAction>(Assert.Single(actions));
        Assert.Equal(Key.Enter, press.Key);
    }

    [Fact]
    public void Normalize_ControlHeldWithALetter_ProducesKeyCombinationAction()
    {
        var events = new[]
        {
            KeyDown(0, Key.LeftControl, null),
            KeyDown(30, Key.A, "a"),
            KeyUp(60, Key.A),
            KeyUp(90, Key.LeftControl),
        };

        var actions = ActionNormalizer.Normalize(events);

        var combo = Assert.IsType<KeyCombinationAction>(Assert.Single(actions));
        Assert.Equal([Key.LeftControl, Key.A], combo.Keys);
    }

    [Fact]
    public void Normalize_ShiftHeldWhileTyping_IsTreatedAsPlainTypingNotACombo()
    {
        // Shift is already reflected in the captured character (a real uppercase 'H'), so this must
        // accumulate into TypeText, not become a KeyCombinationAction.
        var events = new[]
        {
            KeyDown(0, Key.LeftShift, null),
            KeyDown(30, Key.H, "H"),
            KeyUp(60, Key.H),
            KeyUp(90, Key.LeftShift),
        };

        var actions = ActionNormalizer.Normalize(events);

        var typeText = Assert.IsType<TypeTextAction>(Assert.Single(actions));
        Assert.Equal("H", typeText.Text);
    }

    [Fact]
    public void Normalize_ShiftHeldWithANamedKey_ProducesKeyCombinationAction()
    {
        var events = new[]
        {
            KeyDown(0, Key.LeftShift, null),
            KeyDown(30, Key.Tab, null),
            KeyUp(60, Key.Tab),
            KeyUp(90, Key.LeftShift),
        };

        var actions = ActionNormalizer.Normalize(events);

        var combo = Assert.IsType<KeyCombinationAction>(Assert.Single(actions));
        Assert.Equal([Key.LeftShift, Key.Tab], combo.Keys);
    }

    [Fact]
    public void Normalize_ALongPauseBetweenActions_InsertsAWaitAction()
    {
        var events = new[] { Down(0, 10, 10), Up(20, 10, 10), Down(3000, 200, 200), Up(3020, 200, 200) };

        var actions = ActionNormalizer.Normalize(events);

        var wait = Assert.IsType<WaitAction>(actions[1]);
        Assert.True(wait.Milliseconds is >= 2900 and <= 3000);
    }

    [Fact]
    public void Normalize_AShortPauseBetweenActions_DoesNotInsertAWait()
    {
        var events = new[] { Down(0, 10, 10), Up(20, 10, 10), Down(120, 200, 200), Up(140, 200, 200) };

        var actions = ActionNormalizer.Normalize(events);

        Assert.Equal(2, actions.Count);
        Assert.All(actions, a => Assert.IsNotType<WaitAction>(a));
    }

    [Fact]
    public void Normalize_EmptyEventList_ProducesNoActions()
    {
        Assert.Empty(ActionNormalizer.Normalize([]));
    }
}
