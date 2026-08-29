using AutomationStudio.Storage;
using AutomationStudio.Storage.Repositories;

namespace AutomationStudio.Storage.Tests;

public class MigrationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"automationstudio-test-{Guid.NewGuid():n}.db");

    public void Dispose()
    {
        SqliteFileCleanup.TryDeleteDatabaseFiles(_dbPath);
    }

    [Fact]
    public async Task ReopeningAnExistingDatabase_DoesNotReapplyOrFailMigrations()
    {
        // Simulates an app restart: a fresh WorkflowDatabase instance against a db file that
        // already has the schema applied. Migrations are tracked in schema_migrations precisely so
        // this doesn't try (and fail) to re-run CREATE TABLE on an already-migrated file.
        var firstOpen = new WorkflowRepository(new WorkflowDatabase(_dbPath));
        await firstOpen.SaveAsync(new WorkflowRecord
        {
            Workflow = new AutomationStudio.Core.Workflows.Workflow { Id = "wf-1", Name = "test", Actions = [] },
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        var secondOpen = new WorkflowRepository(new WorkflowDatabase(_dbPath));
        var loaded = await secondOpen.GetAsync("wf-1");

        Assert.NotNull(loaded);
        Assert.Equal("test", loaded.Workflow.Name);
    }
}
