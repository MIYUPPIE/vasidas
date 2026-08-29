using System.Reflection;
using Microsoft.Data.Sqlite;

namespace AutomationStudio.Storage;

/// <summary>
/// Applies embedded <c>Migrations/*.sql</c> scripts in filename order, tracking what's already
/// applied in a <c>schema_migrations</c> table. Deliberately not an ORM migration framework — five
/// tables and one migration so far don't justify one; a numbered-scripts-plus-tracking-table is the
/// whole mechanism a project this size needs.
/// </summary>
internal static class MigrationRunner
{
    public static void ApplyPendingMigrations(SqliteConnection connection, Assembly assembly)
    {
        using (var createTable = connection.CreateCommand())
        {
            createTable.CommandText =
                "CREATE TABLE IF NOT EXISTS schema_migrations (name TEXT PRIMARY KEY, applied_at TEXT NOT NULL);";
            createTable.ExecuteNonQuery();
        }

        var applied = new HashSet<string>(StringComparer.Ordinal);
        using (var select = connection.CreateCommand())
        {
            select.CommandText = "SELECT name FROM schema_migrations;";
            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                applied.Add(reader.GetString(0));
            }
        }

        var migrationNames = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Migrations.", StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var resourceName in migrationNames)
        {
            if (applied.Contains(resourceName))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded migration '{resourceName}' could not be read.");
            using var reader = new StreamReader(stream);
            var sql = reader.ReadToEnd();

            using var transaction = connection.BeginTransaction();
            using (var apply = connection.CreateCommand())
            {
                apply.Transaction = transaction;
                apply.CommandText = sql;
                apply.ExecuteNonQuery();
            }

            using (var record = connection.CreateCommand())
            {
                record.Transaction = transaction;
                record.CommandText = "INSERT INTO schema_migrations (name, applied_at) VALUES ($name, $appliedAt);";
                record.Parameters.AddWithValue("$name", resourceName);
                record.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToString("O"));
                record.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }
}
