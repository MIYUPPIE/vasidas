using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Workflows;
using AutomationStudio.Storage.Repositories;

namespace AutomationStudio.Storage.Tests;

public class WorkflowCommandRegistryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly WorkflowRepository _repository;
    private readonly WorkflowCommandRegistry _registry;

    public WorkflowCommandRegistryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"automationstudio-test-{Guid.NewGuid():n}.db");
        _repository = new WorkflowRepository(new WorkflowDatabase(_dbPath));
        _registry = new WorkflowCommandRegistry(_repository);
    }

    public void Dispose()
    {
        SqliteFileCleanup.TryDeleteDatabaseFiles(_dbPath);
    }

    private async Task SaveWorkflowAsync(string id, string name)
    {
        await _repository.SaveAsync(new WorkflowRecord
        {
            Workflow = new Workflow { Id = id, Name = name, Actions = [new WaitAction { Milliseconds = 1 }] },
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    [Fact]
    public async Task ResolveWorkflowIdAsync_ForATaughtCommandsName_ReturnsItsWorkflowId()
    {
        await SaveWorkflowAsync("StartChurchService", "start church service");

        var resolved = await _registry.ResolveWorkflowIdAsync("start church service");

        Assert.Equal("StartChurchService", resolved);
    }

    [Fact]
    public async Task ResolveWorkflowIdAsync_MatchIsCaseAndWhitespaceInsensitive()
    {
        await SaveWorkflowAsync("StartChurchService", "  Start Church Service  ");

        var resolved = await _registry.ResolveWorkflowIdAsync("start church service");

        Assert.Equal("StartChurchService", resolved);
    }

    [Fact]
    public async Task ResolveWorkflowIdAsync_ForAnUnregisteredPhrase_ReturnsNull()
    {
        await SaveWorkflowAsync("StartChurchService", "start church service");

        var resolved = await _registry.ResolveWorkflowIdAsync("open obs");

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveWorkflowIdAsync_EmptyText_ReturnsNull()
    {
        var resolved = await _registry.ResolveWorkflowIdAsync("");

        Assert.Null(resolved);
    }
}
