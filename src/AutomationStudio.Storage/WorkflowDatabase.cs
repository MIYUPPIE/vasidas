using System.Reflection;
using Microsoft.Data.Sqlite;

namespace AutomationStudio.Storage;

/// <summary>
/// Owns the SQLite connection string and applies embedded migrations on first use. One instance
/// per database file; <see cref="Repositories.WorkflowRepository"/> and
/// <see cref="Repositories.ExecutionRepository"/> each open their own short-lived connections from
/// it per call rather than sharing one long-lived connection, since Microsoft.Data.Sqlite
/// connections are cheap to open and this avoids any cross-repository connection-state coupling.
/// </summary>
public sealed class WorkflowDatabase
{
    private readonly string _connectionString;
    private readonly object _migrationLock = new();
    private bool _migrated;

    public WorkflowDatabase(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true
        }.ToString();
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        EnsureMigrated(connection);
        return connection;
    }

    private void EnsureMigrated(SqliteConnection connection)
    {
        lock (_migrationLock)
        {
            if (_migrated)
            {
                return;
            }

            MigrationRunner.ApplyPendingMigrations(connection, typeof(WorkflowDatabase).Assembly);
            _migrated = true;
        }
    }
}
