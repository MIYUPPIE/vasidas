using AutomationStudio.Abstractions;
using AutomationStudio.Applications.Planning;
using AutomationStudio.Recorder;

namespace AutomationStudio.Applications.Tests;

public class TeachingCoordinatorTests
{
    private readonly FakeInputRecorder _recorder = new();
    private readonly FakeWindowManager _windowManager = new();
    private readonly FakeWorkflowRepository _repository = new();
    private readonly TeachingCoordinator _coordinator;

    public TeachingCoordinatorTests()
    {
        var session = new TeachingSession(_recorder, _windowManager);
        _coordinator = new TeachingCoordinator(session, _repository);
    }

    [Fact]
    public void BeginTeaching_MovesToAwaitingCommandName()
    {
        _coordinator.BeginTeaching();

        Assert.Equal(TeachingState.AwaitingCommandName, _coordinator.State);
    }

    [Fact]
    public async Task StartRecordingAsync_StartsTheInputRecorder_AndMovesToRecording()
    {
        await _coordinator.StartRecordingAsync("Start church service");

        Assert.True(_recorder.Started);
        Assert.Equal(TeachingState.Recording, _coordinator.State);
        Assert.Equal("Start church service", _coordinator.PendingCommandName);
    }

    [Fact]
    public async Task FinishTeachingAsync_PersistsTheGeneratedWorkflow_AndReturnsToIdle()
    {
        _recorder.EventsToReturn =
        [
            new RecordedInputEvent(RecordedInputEventKind.MouseDown, TimeSpan.Zero, new ScreenPoint(1, 1), Button: MouseButton.Left),
            new RecordedInputEvent(RecordedInputEventKind.MouseUp, TimeSpan.FromMilliseconds(20), new ScreenPoint(1, 1), Button: MouseButton.Left),
        ];
        await _coordinator.StartRecordingAsync("Start church service");

        var workflow = await _coordinator.FinishTeachingAsync();

        Assert.Equal("Start church service", workflow.Name);
        Assert.Equal("StartChurchService", workflow.Id);
        Assert.Contains("StartChurchService", _repository.SavedIds);
        Assert.Equal(TeachingState.Idle, _coordinator.State);
        Assert.Null(_coordinator.PendingCommandName);
    }

    [Fact]
    public async Task FinishTeachingAsync_TheSameCommandTaughtTwice_OverwritesRatherThanDuplicating()
    {
        await _coordinator.StartRecordingAsync("Start church service");
        await _coordinator.FinishTeachingAsync();

        await _coordinator.StartRecordingAsync("Start church service");
        await _coordinator.FinishTeachingAsync();

        Assert.Single(_repository.RecordsById);
    }

    [Fact]
    public async Task StartRecordingAsync_WhileAlreadyRecording_Throws()
    {
        await _coordinator.StartRecordingAsync("First");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _coordinator.StartRecordingAsync("Second"));
    }

    [Fact]
    public async Task FinishTeachingAsync_WhenNotRecording_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _coordinator.FinishTeachingAsync());
    }

    [Fact]
    public async Task CancelTeaching_WhileRecording_StopsTheRecorderAndDiscardsTheResult_ReturningToIdle()
    {
        await _coordinator.StartRecordingAsync("Abandoned command");

        _coordinator.CancelTeaching();

        Assert.Equal(TeachingState.Idle, _coordinator.State);
        Assert.Null(_coordinator.PendingCommandName);
        Assert.Empty(_repository.RecordsById);
    }

    [Fact]
    public void CancelTeaching_WhenIdle_IsANoOp()
    {
        _coordinator.CancelTeaching();

        Assert.Equal(TeachingState.Idle, _coordinator.State);
    }
}
