using AutomationStudio.Abstractions;
using AutomationStudio.Core.Actions;

namespace AutomationStudio.Recorder.Tests;

public class TeachingSessionTests
{
    [Fact]
    public async Task StartAsync_StartsTheRecorder()
    {
        var recorder = new FakeInputRecorder();
        var session = new TeachingSession(recorder, new FakeWindowManager());

        await session.StartAsync();

        Assert.True(recorder.Started);
    }

    [Fact]
    public async Task Stop_NamesTheWorkflowAfterTheCommand_AndSlugifiesItsId()
    {
        var recorder = new FakeInputRecorder();
        var session = new TeachingSession(recorder, new FakeWindowManager());
        await session.StartAsync();

        var workflow = session.Stop("Start church service");

        Assert.Equal("Start church service", workflow.Name);
        Assert.Equal("StartChurchService", workflow.Id);
    }

    [Fact]
    public async Task Stop_WhenAWindowWasFocusedAtStart_PrependsAFocusApplicationAction()
    {
        var recorder = new FakeInputRecorder { EventsToReturn = [] };
        var windowManager = new FakeWindowManager { ActiveWindow = new WindowInfo("OBS Studio", 1234, "obs") };
        var session = new TeachingSession(recorder, windowManager);
        await session.StartAsync();

        var workflow = session.Stop("Open obs");

        var focus = Assert.IsType<FocusApplicationAction>(workflow.Actions[0]);
        Assert.Equal("obs", focus.ProcessName);
    }

    [Fact]
    public async Task Stop_WhenNoWindowWasFocused_DoesNotPrependAFocusAction()
    {
        var recorder = new FakeInputRecorder();
        var session = new TeachingSession(recorder, new FakeWindowManager { ActiveWindow = null });
        await session.StartAsync();

        var workflow = session.Stop("Do something");

        Assert.DoesNotContain(workflow.Actions, a => a is FocusApplicationAction);
    }

    [Fact]
    public async Task Stop_NormalizesTheCapturedEventsIntoActions()
    {
        var recorder = new FakeInputRecorder
        {
            EventsToReturn =
            [
                new RecordedInputEvent(RecordedInputEventKind.MouseDown, TimeSpan.Zero, new ScreenPoint(10, 10), Button: MouseButton.Left),
                new RecordedInputEvent(RecordedInputEventKind.MouseUp, TimeSpan.FromMilliseconds(50), new ScreenPoint(10, 10), Button: MouseButton.Left),
            ]
        };
        var session = new TeachingSession(recorder, new FakeWindowManager());
        await session.StartAsync();

        var workflow = session.Stop("Click here");

        Assert.Contains(workflow.Actions, a => a is ClickAction);
    }

    [Fact]
    public async Task Stop_CalledTwiceForDifferentCommands_UsesTheSecondSessionsEventsOnly()
    {
        var recorder = new FakeInputRecorder();
        var session = new TeachingSession(recorder, new FakeWindowManager());

        await session.StartAsync();
        recorder.EventsToReturn = [new RecordedInputEvent(RecordedInputEventKind.MouseMove, TimeSpan.Zero, new ScreenPoint(1, 1))];
        var first = session.Stop("First command");

        await session.StartAsync();
        recorder.EventsToReturn = [new RecordedInputEvent(RecordedInputEventKind.MouseMove, TimeSpan.Zero, new ScreenPoint(2, 2))];
        var second = session.Stop("Second command");

        Assert.Equal("FirstCommand", first.Id);
        Assert.Equal("SecondCommand", second.Id);
    }
}
