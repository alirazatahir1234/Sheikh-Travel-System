namespace SheikhTravelSystem.Tests.TravelNexus.LiveSql;

/// <summary>
/// Opt-in live SQL Server fact for the TravelNexus end-to-end tests.
///   SHEIKHGO_TRAVELNEXUS_SQL_CONNECTION   — server-level connection string (Database=master). The fixture creates its
///                                            own throwaway database on that server and drops it afterwards.
///   SHEIKHGO_TRAVELNEXUS_REQUIRE_LIVE_SQL=1 — fail instead of soft-skipping when the connection is missing (CI).
///   SHEIKHGO_TRAVELNEXUS_KEEP_DB=1          — keep the database after the run for inspection.
/// Without the connection string the tests are reported as SKIPPED, never as passed. Nothing here ever logs the string.
/// </summary>
public sealed class TravelLiveSqlFactAttribute : FactAttribute
{
    public const string ConnectionEnv = "SHEIKHGO_TRAVELNEXUS_SQL_CONNECTION";
    public const string RequireEnv = "SHEIKHGO_TRAVELNEXUS_REQUIRE_LIVE_SQL";
    public const string KeepDbEnv = "SHEIKHGO_TRAVELNEXUS_KEEP_DB";

    public static bool Configured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionEnv));
    public static bool Required => Environment.GetEnvironmentVariable(RequireEnv) == "1";

    public TravelLiveSqlFactAttribute()
    {
        if (!Configured && !Required)
            Skip = $"Live SQL not configured: set {ConnectionEnv} to a server-level (Database=master) connection string of a throwaway SQL Server.";
    }
}
