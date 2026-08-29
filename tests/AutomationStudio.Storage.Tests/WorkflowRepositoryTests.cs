using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Workflows;
using AutomationStudio.Storage;
using AutomationStudio.Storage.Repositories;

namespace AutomationStudio.Storage.Tests;

public class WorkflowRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly WorkflowRepository _repository;

    public WorkflowRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"automationstudio-test-{Guid.NewGuid():n}.db");
        _repository = new WorkflowRepository(new WorkflowDatabase(_dbPath));
    }

    public void Dispose()
    {
        SqliteFileCleanup.TryDeleteDatabaseFiles(_dbPath);
    }

    private static WorkflowRecord BuildSampleRecord(string id = "wf-1") => new()
    {
        Workflow = new Workflow
        {
            Id = id,
            Name = "OBS Recording",
            Description = "Starts and stops a recording.",
            Version = 1,
            StopOnFailure = true,
            Actions =
            [
                new OpenApplicationAction { Path = "obs", Arguments = "--startrecording" },
                new WaitAction { Milliseconds = 5000 },
                new ClickAction { X = 742, Y = 510, RetryCount = 2 },
                new TakeScreenshotAction { SavePath = "out.png" },
            ]
        },
        Platform = "Linux",
        Enabled = true,
        CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        UpdatedAtUtc = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)
    };

    [Fact]
    public async Task GetAsync_ForUnknownId_ReturnsNull()
    {
        var result = await _repository.GetAsync("does-not-exist");
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsWorkflowAndActionsInOrder()
    {
        var record = BuildSampleRecord();

        await _repository.SaveAsync(record);
        var loaded = await _repository.GetAsync(record.Workflow.Id);

        Assert.NotNull(loaded);
        Assert.Equal(record.Workflow.Id, loaded.Workflow.Id);
        Assert.Equal(record.Workflow.Name, loaded.Workflow.Name);
        Assert.Equal(record.Workflow.Description, loaded.Workflow.Description);
        Assert.Equal(record.Workflow.Version, loaded.Workflow.Version);
        Assert.Equal(record.Workflow.StopOnFailure, loaded.Workflow.StopOnFailure);
        Assert.Equal(record.Platform, loaded.Platform);
        Assert.Equal(record.Enabled, loaded.Enabled);
        Assert.Equal(record.CreatedAtUtc, loaded.CreatedAtUtc);
        Assert.Equal(record.UpdatedAtUtc, loaded.UpdatedAtUtc);

        Assert.Equal(record.Workflow.Actions.Count, loaded.Workflow.Actions.Count);
        Assert.IsType<OpenApplicationAction>(loaded.Workflow.Actions[0]);
        Assert.IsType<WaitAction>(loaded.Workflow.Actions[1]);
        Assert.IsType<ClickAction>(loaded.Workflow.Actions[2]);
        Assert.IsType<TakeScreenshotAction>(loaded.Workflow.Actions[3]);

        var loadedClick = Assert.IsType<ClickAction>(loaded.Workflow.Actions[2]);
        Assert.Equal(742, loadedClick.X);
        Assert.Equal(510, loadedClick.Y);
        Assert.Equal(2, loadedClick.RetryCount);
    }

    [Fact]
    public async Task SaveAsync_CalledTwiceForSameId_UpsertsRatherThanDuplicating()
    {
        var record = BuildSampleRecord();
        await _repository.SaveAsync(record);

        var updated = new WorkflowRecord
        {
            Workflow = new Workflow
            {
                Id = record.Workflow.Id,
                Name = "Renamed",
                Version = record.Workflow.Version,
                Actions = [new WaitAction { Milliseconds = 1 }]
            },
            Platform = record.Platform,
            Enabled = record.Enabled,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.UpdatedAtUtc.AddMinutes(5)
        };

        await _repository.SaveAsync(updated);

        var all = await _repository.ListAsync();
        Assert.Single(all, w => w.Id == record.Workflow.Id);

        var loaded = await _repository.GetAsync(record.Workflow.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Renamed", loaded.Workflow.Name);
        Assert.Single(loaded.Workflow.Actions); // stale actions from the first save must not remain
        Assert.IsType<WaitAction>(loaded.Workflow.Actions[0]);
    }

    [Fact]
    public async Task ListAsync_ReturnsSummariesOrderedByMostRecentlyUpdated()
    {
        var older = BuildSampleRecord("wf-older");
        var newer = BuildSampleRecord("wf-newer");
        await _repository.SaveAsync(older);
        await _repository.SaveAsync(new WorkflowRecord
        {
            Workflow = newer.Workflow,
            Platform = newer.Platform,
            Enabled = newer.Enabled,
            CreatedAtUtc = newer.CreatedAtUtc,
            UpdatedAtUtc = older.UpdatedAtUtc.AddDays(1)
        });

        var summaries = await _repository.ListAsync();

        Assert.Equal("wf-newer", summaries[0].Id);
        Assert.Equal("wf-older", summaries[1].Id);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheWorkflow()
    {
        var record = BuildSampleRecord();
        await _repository.SaveAsync(record);

        await _repository.DeleteAsync(record.Workflow.Id);

        Assert.Null(await _repository.GetAsync(record.Workflow.Id));
        Assert.Empty(await _repository.ListAsync());
    }
}
