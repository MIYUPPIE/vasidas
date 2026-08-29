namespace AutomationStudio.Storage.Tests;

/// <summary>WAL mode leaves "-wal"/"-shm" sidecar files next to the main database file; tests need to clean up all three.</summary>
internal static class SqliteFileCleanup
{
    public static void TryDeleteDatabaseFiles(string databasePath)
    {
        foreach (var path in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" })
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }
}
