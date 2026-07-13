namespace BuptGpaCalculator.Core.Persistence;

/// <summary>Initializes the Windows SQLite provider used by the local archive.</summary>
internal static class SqliteProviderBootstrap
{
    private static readonly object SyncRoot = new();
    private static bool isInitialized;

    /// <summary>Initializes the SQLite provider once for the current process.</summary>
    public static void EnsureInitialized()
    {
        lock (SyncRoot)
        {
            if (isInitialized)
            {
                return;
            }

            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            SQLitePCL.raw.FreezeProvider();
            isInitialized = true;
        }
    }
}
