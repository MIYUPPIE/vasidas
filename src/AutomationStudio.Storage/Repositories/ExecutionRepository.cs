using System.Globalization;

namespace AutomationStudio.Storage.Repositories;

public sealed class ExecutionRepository : IExecutionRepository
{
    private readonly WorkflowDatabase _database;

    public ExecutionRepository(WorkflowDatabase database)
    {
        _database = database;
    }

    public async Task<string> StartExecutionAsync(string workflowId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString("n");

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO executions (id, workflow_id, started_at, status) VALUES ($id, $workflowId, $startedAt, $status);";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$workflowId", workflowId);
        command.Parameters.AddWithValue("$startedAt", startedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$status", "Running");
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return id;
    }

    public async Task CompleteExecutionAsync(
        string executionId, DateTimeOffset completedAtUtc, string status, string? error, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE executions SET completed_at = $completedAt, status = $status, error = $error WHERE id = $id;";
        command.Parameters.AddWithValue("$id", executionId);
        command.Parameters.AddWithValue("$completedAt", completedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddLogEntryAsync(ExecutionLogEntry entry, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO execution_logs (execution_id, timestamp, level, message, action_id, screenshot_path)
            VALUES ($executionId, $timestamp, $level, $message, $actionId, $screenshotPath);
            """;
        command.Parameters.AddWithValue("$executionId", entry.ExecutionId);
        command.Parameters.AddWithValue("$timestamp", entry.TimestampUtc.ToString("O"));
        command.Parameters.AddWithValue("$level", entry.Level);
        command.Parameters.AddWithValue("$message", entry.Message);
        command.Parameters.AddWithValue("$actionId", (object?)entry.ActionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$screenshotPath", (object?)entry.ScreenshotPath ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ExecutionRecord>> ListExecutionsAsync(
        string workflowId, int limit = 50, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, workflow_id, started_at, completed_at, status, error FROM executions " +
            "WHERE workflow_id = $workflowId ORDER BY started_at DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$workflowId", workflowId);
        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<ExecutionRecord>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new ExecutionRecord(
                Id: reader.GetString(0),
                WorkflowId: reader.GetString(1),
                StartedAtUtc: ParseTimestamp(reader.GetString(2)),
                CompletedAtUtc: reader.IsDBNull(3) ? null : ParseTimestamp(reader.GetString(3)),
                Status: reader.GetString(4),
                Error: reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return results;
    }

    public async Task<IReadOnlyList<ExecutionLogEntry>> GetLogEntriesAsync(string executionId, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT execution_id, timestamp, level, message, action_id, screenshot_path FROM execution_logs " +
            "WHERE execution_id = $executionId ORDER BY timestamp ASC, id ASC;";
        command.Parameters.AddWithValue("$executionId", executionId);

        var results = new List<ExecutionLogEntry>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new ExecutionLogEntry(
                ExecutionId: reader.GetString(0),
                TimestampUtc: ParseTimestamp(reader.GetString(1)),
                Level: reader.GetString(2),
                Message: reader.GetString(3),
                ActionId: reader.IsDBNull(4) ? null : reader.GetString(4),
                ScreenshotPath: reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return results;
    }

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
