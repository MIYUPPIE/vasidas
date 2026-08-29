using AutomationStudio.Core.Workflows;
using AutomationStudio.Storage;
using AutomationStudio.Storage.Repositories;

namespace AutomationStudio.Storage.Tests;

public class ExecutionRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ExecutionRepository _repository;
    private readonly WorkflowRepository _workflowRepository;

    public ExecutionRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"automationstudio-test-{Guid.NewGuid():n}.db");
        var database = new WorkflowDatabase(_dbPath);
        _repository = new ExecutionRepository(database);
        _workflowRepository = new WorkflowRepository(database);
    }

    public void Dispose()
    {
        SqliteFileCleanup.TryDeleteDatabaseFiles(_dbPath);
    }

    /// <summary>executions.workflow_id has a foreign key to workflows.id, so tests need a real parent row first.</summary>
    private Task SeedWorkflowAsync(string id) => _workflowRepository.SaveAsync(new WorkflowRecord
    {
        Workflow = new Workflow { Id = id, Name = id, Actions = [] },
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    });

    [Fact]
    public async Task StartExecutionAsync_ThenListExecutionsAsync_ShowsItAsRunning()
    {
        await SeedWorkflowAsync("wf-1");
        var startedAt = DateTimeOffset.UtcNow;
        var executionId = await _repository.StartExecutionAsync("wf-1", startedAt);

        var executions = await _repository.ListExecutionsAsync("wf-1");

        var execution = Assert.Single(executions);
        Assert.Equal(executionId, execution.Id);
        Assert.Equal("wf-1", execution.WorkflowId);
        Assert.Equal("Running", execution.Status);
        Assert.Null(execution.CompletedAtUtc);
        Assert.Null(execution.Error);
    }

    [Fact]
    public async Task CompleteExecutionAsync_UpdatesStatusCompletionTimeAndError()
    {
        await SeedWorkflowAsync("wf-1");
        var executionId = await _repository.StartExecutionAsync("wf-1", DateTimeOffset.UtcNow);
        var completedAt = DateTimeOffset.UtcNow.AddSeconds(5);

        await _repository.CompleteExecutionAsync(executionId, completedAt, "Failed", "boom");

        var execution = Assert.Single(await _repository.ListExecutionsAsync("wf-1"));
        Assert.Equal("Failed", execution.Status);
        Assert.Equal("boom", execution.Error);
        Assert.NotNull(execution.CompletedAtUtc);
    }

    [Fact]
    public async Task AddLogEntryAsync_ThenGetLogEntriesAsync_ReturnsEntriesInTimestampOrder()
    {
        await SeedWorkflowAsync("wf-1");
        var executionId = await _repository.StartExecutionAsync("wf-1", DateTimeOffset.UtcNow);
        var baseTime = DateTimeOffset.UtcNow;

        await _repository.AddLogEntryAsync(new ExecutionLogEntry(executionId, baseTime, "Information", "started", null, null));
        await _repository.AddLogEntryAsync(new ExecutionLogEntry(
            executionId, baseTime.AddSeconds(1), "Error", "click failed", "action-1", "/tmp/fail.png"));

        var entries = await _repository.GetLogEntriesAsync(executionId);

        Assert.Equal(2, entries.Count);
        Assert.Equal("started", entries[0].Message);
        Assert.Equal("click failed", entries[1].Message);
        Assert.Equal("action-1", entries[1].ActionId);
        Assert.Equal("/tmp/fail.png", entries[1].ScreenshotPath);
    }

    [Fact]
    public async Task ListExecutionsAsync_OnlyReturnsExecutionsForTheRequestedWorkflow()
    {
        await SeedWorkflowAsync("wf-1");
        await SeedWorkflowAsync("wf-2");
        await _repository.StartExecutionAsync("wf-1", DateTimeOffset.UtcNow);
        await _repository.StartExecutionAsync("wf-2", DateTimeOffset.UtcNow);

        var wf1Executions = await _repository.ListExecutionsAsync("wf-1");

        Assert.Single(wf1Executions);
        Assert.Equal("wf-1", wf1Executions[0].WorkflowId);
    }
}
