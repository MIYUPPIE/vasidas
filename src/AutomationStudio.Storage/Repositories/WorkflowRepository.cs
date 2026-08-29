using System.Globalization;
using System.Text.Json;
using AutomationStudio.Core.Actions;
using AutomationStudio.Core.Workflows;
using Microsoft.Data.Sqlite;

namespace AutomationStudio.Storage.Repositories;

/// <summary>
/// Plain ADO.NET rather than an ORM/micro-ORM: with five tables and simple queries, hand-written
/// SQL is both simpler and avoids any ambiguity in how a library would map SQLite's TEXT/INTEGER
/// storage classes back to <see cref="DateTimeOffset"/>/<see cref="bool"/> — this reads and writes
/// those conversions explicitly instead.
/// </summary>
public sealed class WorkflowRepository : IWorkflowRepository
{
    private readonly WorkflowDatabase _database;

    public WorkflowRepository(WorkflowDatabase database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<WorkflowSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, name, description, platform, enabled, updated_at FROM workflows ORDER BY updated_at DESC;";

        var results = new List<WorkflowSummary>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new WorkflowSummary(
                Id: reader.GetString(0),
                Name: reader.GetString(1),
                Description: reader.IsDBNull(2) ? null : reader.GetString(2),
                Platform: reader.IsDBNull(3) ? null : reader.GetString(3),
                Enabled: reader.GetInt64(4) != 0,
                UpdatedAtUtc: ParseTimestamp(reader.GetString(5))));
        }

        return results;
    }

    public async Task<WorkflowRecord?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();

        string name;
        string? description;
        int version;
        bool stopOnFailure;
        string? platform;
        DateTimeOffset createdAtUtc;
        DateTimeOffset updatedAtUtc;
        bool enabled;

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT name, description, version, platform, created_at, updated_at, enabled, stop_on_failure " +
                "FROM workflows WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);

            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            name = reader.GetString(0);
            description = reader.IsDBNull(1) ? null : reader.GetString(1);
            version = (int)reader.GetInt64(2);
            platform = reader.IsDBNull(3) ? null : reader.GetString(3);
            createdAtUtc = ParseTimestamp(reader.GetString(4));
            updatedAtUtc = ParseTimestamp(reader.GetString(5));
            enabled = reader.GetInt64(6) != 0;
            stopOnFailure = reader.GetInt64(7) != 0;
        }

        var actions = new List<WorkflowAction>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT configuration_json FROM workflow_actions WHERE workflow_id = $workflowId ORDER BY sequence ASC;";
            command.Parameters.AddWithValue("$workflowId", id);

            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var json = reader.GetString(0);
                var action = JsonSerializer.Deserialize<WorkflowAction>(json, WorkflowSerializer.Options)
                    ?? throw new InvalidOperationException($"Workflow action row for workflow '{id}' deserialized to null.");
                actions.Add(action);
            }
        }

        return new WorkflowRecord
        {
            Workflow = new Workflow
            {
                Id = id,
                Name = name,
                Description = description,
                Version = version,
                StopOnFailure = stopOnFailure,
                Actions = actions
            },
            Platform = platform,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
            Enabled = enabled
        };
    }

    public async Task SaveAsync(WorkflowRecord record, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText = """
                INSERT INTO workflows (id, name, description, version, platform, created_at, updated_at, enabled, stop_on_failure)
                VALUES ($id, $name, $description, $version, $platform, $createdAt, $updatedAt, $enabled, $stopOnFailure)
                ON CONFLICT(id) DO UPDATE SET
                    name = excluded.name,
                    description = excluded.description,
                    version = excluded.version,
                    platform = excluded.platform,
                    updated_at = excluded.updated_at,
                    enabled = excluded.enabled,
                    stop_on_failure = excluded.stop_on_failure;
                """;
            upsert.Parameters.AddWithValue("$id", record.Workflow.Id);
            upsert.Parameters.AddWithValue("$name", record.Workflow.Name);
            upsert.Parameters.AddWithValue("$description", (object?)record.Workflow.Description ?? DBNull.Value);
            upsert.Parameters.AddWithValue("$version", record.Workflow.Version);
            upsert.Parameters.AddWithValue("$platform", (object?)record.Platform ?? DBNull.Value);
            upsert.Parameters.AddWithValue("$createdAt", record.CreatedAtUtc.ToString("O"));
            upsert.Parameters.AddWithValue("$updatedAt", record.UpdatedAtUtc.ToString("O"));
            upsert.Parameters.AddWithValue("$enabled", record.Enabled ? 1 : 0);
            upsert.Parameters.AddWithValue("$stopOnFailure", record.Workflow.StopOnFailure ? 1 : 0);
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        using (var deleteActions = connection.CreateCommand())
        {
            deleteActions.Transaction = transaction;
            deleteActions.CommandText = "DELETE FROM workflow_actions WHERE workflow_id = $workflowId;";
            deleteActions.Parameters.AddWithValue("$workflowId", record.Workflow.Id);
            await deleteActions.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        for (var sequence = 0; sequence < record.Workflow.Actions.Count; sequence++)
        {
            var action = record.Workflow.Actions[sequence];
            using var insertAction = connection.CreateCommand();
            insertAction.Transaction = transaction;
            insertAction.CommandText = """
                INSERT INTO workflow_actions (id, workflow_id, sequence, action_type, configuration_json, enabled)
                VALUES ($id, $workflowId, $sequence, $actionType, $configurationJson, $enabled);
                """;
            insertAction.Parameters.AddWithValue("$id", action.Id);
            insertAction.Parameters.AddWithValue("$workflowId", record.Workflow.Id);
            insertAction.Parameters.AddWithValue("$sequence", sequence);
            insertAction.Parameters.AddWithValue("$actionType", action.ActionType);
            insertAction.Parameters.AddWithValue("$configurationJson", JsonSerializer.Serialize(action, WorkflowSerializer.Options));
            insertAction.Parameters.AddWithValue("$enabled", action.Enabled ? 1 : 0);
            await insertAction.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM workflows WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
