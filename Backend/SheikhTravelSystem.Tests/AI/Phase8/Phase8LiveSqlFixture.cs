using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace SheikhTravelSystem.Tests.AI.Phase8;

/// <summary>
/// Discovers CI-friendly SQL + SheikhGo-MCP for Phase 8 live E2E.
/// Skips when unavailable unless <c>SHEIKHGO_PHASE8_LIVE_SQL=require</c>.
/// Never logs connection strings or passwords.
/// </summary>
public static class Phase8LiveSqlFixture
{
    public const int TenantA = 9001;
    public const int TenantB = 9002;
    public const int ExpectedCountA = 3;
    public const int ExpectedCountB = 5;

    public const string RequireEnv = "SHEIKHGO_PHASE8_LIVE_SQL";
    public const string ConnectionEnv = "SHEIKHGO_PHASE8_SQL_CONNECTION";
    public const string McpEntryEnv = "SHEIKHGO_PHASE8_MCP_ENTRY";

    public sealed record Availability(
        bool Available,
        string? SkipReason,
        string? ConnectionString,
        string? McpEntryPath,
        bool RequireLive);

    public static Availability Probe()
    {
        var require = string.Equals(
            Environment.GetEnvironmentVariable(RequireEnv),
            "require",
            StringComparison.OrdinalIgnoreCase);

        var cs = FirstNonEmpty(
            Environment.GetEnvironmentVariable(ConnectionEnv),
            Environment.GetEnvironmentVariable("SHEIKHGO_DB_CONNECTION"),
            BuildDefaultLocalCiConnection());

        if (string.IsNullOrWhiteSpace(cs))
        {
            return new Availability(
                false,
                require
                    ? $"{RequireEnv}=require but no SQL connection (set {ConnectionEnv})"
                    : "No Phase 8 SQL connection configured",
                null,
                null,
                require);
        }

        if (!CanOpen(cs))
        {
            return new Availability(
                false,
                require
                    ? $"{RequireEnv}=require but SQL is unreachable"
                    : "Phase 8 SQL unreachable — skipping live MCP E2E",
                null,
                null,
                require);
        }

        var mcp = ResolveMcpEntry();
        if (string.IsNullOrWhiteSpace(mcp) || !File.Exists(mcp))
        {
            return new Availability(
                false,
                require
                    ? $"{RequireEnv}=require but SheikhGo-MCP dist/index.js not found (set {McpEntryEnv})"
                    : "SheikhGo-MCP entry not found — skipping live MCP E2E",
                Redact(cs),
                null,
                require);
        }

        return new Availability(true, null, cs, mcp, require);
    }

    public static void EnsureFixtures(string connectionString)
    {
        var seedPath = Path.Combine(
            AppContext.BaseDirectory,
            "AI", "Phase8", "Fixtures", "phase8-live-sql-seed.sql");
        if (!File.Exists(seedPath))
            throw new FileNotFoundException("Phase 8 seed SQL missing from test output.", seedPath);

        var script = File.ReadAllText(seedPath);
        // Master connection for CREATE DATABASE; then run batches.
        var masterCs = RewriteDatabase(connectionString, "master");
        ExecuteBatches(masterCs, script);

        // Verify counts without logging data beyond integers.
        using var conn = new SqlConnection(RewriteDatabase(connectionString, "SheikhGo_Phase8_CI"));
        conn.Open();
        var a = ScalarInt(conn, "SELECT COUNT(*) FROM dbo.Vehicles WHERE TenantId=@t AND IsDeleted=0 AND Status NOT IN (4,5)", TenantA);
        var b = ScalarInt(conn, "SELECT COUNT(*) FROM dbo.Vehicles WHERE TenantId=@t AND IsDeleted=0 AND Status NOT IN (4,5)", TenantB);
        if (a != ExpectedCountA || b != ExpectedCountB)
            throw new InvalidOperationException(
                $"Phase 8 fixture counts unexpected (A={a} expected {ExpectedCountA}, B={b} expected {ExpectedCountB}).");
    }

    public static string Phase8DbConnection(string anyConnection)
        => RewriteDatabase(anyConnection, "SheikhGo_Phase8_CI");

    /// <summary>Safe for assertions / diagnostics — never includes password.</summary>
    public static string DescribeConnection(string connectionString)
    {
        try
        {
            var b = new SqlConnectionStringBuilder(connectionString);
            return $"Server={b.DataSource};Database={b.InitialCatalog};User={b.UserID};TrustServerCertificate={b.TrustServerCertificate}";
        }
        catch
        {
            return "(unparseable connection — redacted)";
        }
    }

    private static string? BuildDefaultLocalCiConnection()
    {
        // Local docker-compose.db.yml defaults — CI overrides via env. Password never logged.
        var host = Environment.GetEnvironmentVariable("SHEIKHGO_PHASE8_SQL_HOST") ?? "127.0.0.1,1433";
        var user = Environment.GetEnvironmentVariable("SHEIKHGO_PHASE8_SQL_USER") ?? "sa";
        var password = Environment.GetEnvironmentVariable("SHEIKHGO_PHASE8_SQL_PASSWORD")
                       ?? Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
            return null;

        return $"Server={host};Database=master;User Id={user};Password={password};TrustServerCertificate=True;Encrypt=False;";
    }

    private static string? ResolveMcpEntry()
    {
        var env = Environment.GetEnvironmentVariable(McpEntryEnv)
                  ?? Environment.GetEnvironmentVariable("SHEIKHGO_MCP_ARGS");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var path = env.Trim().Trim('"');
            if (path.StartsWith('['))
            {
                // JSON array form from SHEIKHGO_MCP_ARGS
                try
                {
                    var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(path);
                    if (arr is { Length: > 0 })
                        return arr[0];
                }
                catch
                {
                    /* fall through */
                }
            }
            return path;
        }

        // Sibling repo next to Sheikh-Travel-System (dev + many CI checkouts).
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
            return null;

        var sibling = Path.GetFullPath(Path.Combine(repoRoot, "..", "SheikhGo-MCP", "dist", "index.js"));
        if (File.Exists(sibling))
            return sibling;

        var nested = Path.Combine(repoRoot, "SheikhGo-MCP", "dist", "index.js");
        return File.Exists(nested) ? nested : null;
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "azure-pipelines.yml")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static bool CanOpen(string connectionString)
    {
        try
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            return conn.State == ConnectionState.Open;
        }
        catch
        {
            return false;
        }
    }

    private static void ExecuteBatches(string connectionString, string script)
    {
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        foreach (var batch in batches)
        {
            var sql = batch.Trim();
            if (string.IsNullOrWhiteSpace(sql))
                continue;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 60;
            cmd.ExecuteNonQuery();
        }
    }

    private static int ScalarInt(SqlConnection conn, string sql, int tenantId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@t", tenantId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static string RewriteDatabase(string connectionString, string database)
    {
        var b = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = database };
        return b.ConnectionString;
    }

    private static string Redact(string cs)
    {
        try
        {
            var b = new SqlConnectionStringBuilder(cs) { Password = "***" };
            return b.ConnectionString;
        }
        catch
        {
            return "***";
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
