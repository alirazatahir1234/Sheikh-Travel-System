using System.Text.RegularExpressions;
using FluentAssertions;

namespace SheikhTravelSystem.Tests.Security.Sg01;

/// <summary>
/// SG-01 Pass 2, step 5 — trip child writes and driver refresh-token update must not
/// touch another company's rows. Child trip tables have no TenantId column, so every
/// statement must go through Trips (or Users.TenantId for the refresh token).
/// Zero-row tenant-scoped writes must fail loudly (NotFoundException), matching Step 4.
/// </summary>
public class Sg01TripDriverAppIsolationTests
{
    private static readonly string Backend = FindBackendRoot();

    [Fact]
    public void Trip_status_history_insert_is_bound_to_the_trip_tenant_and_fails_when_zero_rows()
    {
        var body = MethodBody("TripLifecycleSql.cs", "RecordStatusAsync");
        var sql = Normalize(body);

        sql.Should().Contain("INSERT INTO TripStatusHistory");
        sql.Should().Contain("FROM Trips t");
        sql.Should().Contain("t.Id = @TripId AND t.TenantId = @TenantId");
        sql.Should().NotContain("VALUES (@TripId",
            because: "an unconditional VALUES insert can attach history to another tenant's trip");
        body.Should().Contain("if (affected == 0)",
            because: "a missing/foreign-tenant trip must not look like a successful status write");
        body.Should().Contain("throw new NotFoundException(\"Trip\"",
            because: "zero-row status history must match Step 4 NotFound semantics");
    }

    [Fact]
    public void Trip_stop_replace_rejects_unconditional_values_and_requires_tenant_on_insert()
    {
        var body = MethodBody("TripLifecycleSql.cs", "ReplaceStopsAsync");
        var sql = Normalize(body);

        sql.Should().Contain("UPDATE TripStops SET IsDeleted = 1");
        sql.Should().Contain(
            "AND EXISTS ( SELECT 1 FROM Trips t WHERE t.Id = @TripId AND t.TenantId = @TenantId AND t.IsDeleted = 0)");
        sql.Should().Contain("INSERT INTO TripStops");
        sql.Should().Contain("SELECT @TripId, @Sequence");
        sql.Should().NotContain("VALUES (@TripId, @Sequence",
            because: "VALUES inserts skip the tenant trip gate");
        Regex.Matches(sql, Regex.Escape("t.TenantId = @TenantId"))
            .Count.Should().BeGreaterThanOrEqualTo(2);
        body.Should().Contain("if (inserted == 0)");
        body.Should().Contain("throw new NotFoundException(\"Trip\"");
    }

    [Theory]
    [InlineData("AddExpenseAsync", "TripExpenses")]
    [InlineData("AddDocumentAsync", "TripDocuments")]
    [InlineData("AddPassengerAsync", "TripPassengers")]
    public void Trip_child_inserts_use_output_id_and_fail_when_tenant_trip_missing(string method, string table)
    {
        var body = MethodBody("TripRepository.cs", method);
        var sql = Normalize(body);

        sql.Should().Contain($"INSERT INTO {table}");
        sql.Should().Contain("OUTPUT INSERTED.Id");
        sql.Should().Contain("FROM Trips t");
        sql.Should().Contain("t.Id = @TripId AND t.TenantId = @TenantId");
        sql.Should().NotContain("VALUES (@TripId",
            because: $"{method} must not insert without a tenant trip row");
        body.Should().Contain("tenantContext.GetRequiredTenantId()");
        body.Should().Contain("if (id is null or <= 0)");
        body.Should().Contain("throw new NotFoundException(\"Trip\"");
    }

    [Theory]
    [InlineData("SoftDeleteExpenseAsync", "TripExpenses", "TripExpense")]
    [InlineData("SoftDeleteDocumentAsync", "TripDocuments", "TripDocument")]
    [InlineData("SoftDeletePassengerAsync", "TripPassengers", "TripPassenger")]
    [InlineData("UpdatePassengerAsync", "TripPassengers", "TripPassenger")]
    public void Trip_child_updates_require_tenant_trip_and_fail_when_zero_rows(
        string method, string table, string notFoundName)
    {
        var body = MethodBody("TripRepository.cs", method);
        var sql = Normalize(body);

        sql.Should().Contain($"UPDATE {table}");
        sql.Should().Contain("EXISTS");
        sql.Should().Contain("t.Id = @TripId AND t.TenantId = @TenantId");
        body.Should().Contain("tenantContext.GetRequiredTenantId()");
        body.Should().Contain("if (affected == 0)");
        body.Should().Contain($"throw new NotFoundException(\"{notFoundName}\"");
    }

    [Theory]
    [InlineData("AddPassengerAsync")]
    [InlineData("SoftDeletePassengerAsync")]
    public void Passenger_count_updates_are_scoped_by_trip_and_tenant(string method)
    {
        var body = MethodBody("TripRepository.cs", method);
        var sql = Normalize(body);

        sql.Should().Contain("UPDATE Trips SET PassengerCount");
        sql.Should().Contain("WHERE Id = @TripId AND TenantId = @TenantId");
        // Must not leave a TripId-only PassengerCount rewrite that another company could hit.
        Regex.IsMatch(sql, @"UPDATE Trips SET PassengerCount[\s\S]*?WHERE Id = @TripId(?!\s+AND TenantId)")
            .Should().BeFalse(because: $"{method} must not update PassengerCount by TripId alone");
    }

    [Fact]
    public void TripLifecycle_call_sites_pass_authenticated_tenant_id()
    {
        var tripRepo = File.ReadAllText(
            Directory.EnumerateFiles(Backend, "TripRepository.cs", SearchOption.AllDirectories)
                .First(p => !p.Contains("/obj/") && !p.Contains("/bin/")));

        // Every RecordStatus / ReplaceStops invocation must carry the tenant from ITenantContext.
        Regex.Matches(tripRepo, @"TripLifecycleSql\.RecordStatusAsync\([^;]+\)")
            .Cast<Match>()
            .Select(m => Normalize(m.Value))
            .Should().OnlyContain(c => c.Contains("tenantId,"),
                because: "status history must use the authenticated tenant, not trip id alone");

        Regex.Matches(tripRepo, @"TripLifecycleSql\.ReplaceStopsAsync\([^;]+\)")
            .Cast<Match>()
            .Select(m => Normalize(m.Value))
            .Should().OnlyContain(c => c.Contains("tenantId,"),
                because: "stop replace must use the authenticated tenant");
    }

    [Fact]
    public void Driver_refresh_token_update_is_scoped_to_the_login_tenant_and_documented_as_unused()
    {
        var path = Directory.EnumerateFiles(Backend, "DriverAppRepository.cs", SearchOption.AllDirectories)
            .First(p => !p.Contains("/obj/") && !p.Contains("/bin/"));
        var file = File.ReadAllText(path);
        var body = MethodBody("DriverAppRepository.cs", "UpdateUserRefreshTokenAsync");
        var sql = Normalize(body);

        sql.Should().Contain("UPDATE Users SET RefreshToken = @Token, RefreshTokenExpiryTime = @Expiry WHERE Id = @UserId AND TenantId = @TenantId");
        body.Should().Contain("if (affected == 0)");
        body.Should().Contain("throw new NotFoundException(\"User\"");
        Regex.IsMatch(sql, @"WHERE Id = @UserId(?!\s+AND TenantId)")
            .Should().BeFalse(because: "user-id-only update can rewrite another company's token");

        // L1: kept for interface compatibility; login uses AuthSessions — do not delete without a separate review.
        file.Should().Contain("SG-01 Step 5 (L1)");
        file.Should().Contain("AuthSessionService");
        file.Should().Contain("AuthRepository");
    }

    private static string MethodBody(string fileName, string methodName)
    {
        var path = Directory.EnumerateFiles(Backend, fileName, SearchOption.AllDirectories)
            .First(p => !p.Contains("/obj/") && !p.Contains("/bin/"));
        var text = File.ReadAllText(path);

        var declaration = Regex.Match(text, $@"\b(?:Task|Task<\w+>)\b[^\n;=]*\s{Regex.Escape(methodName)}\s*\(");
        declaration.Success.Should().BeTrue(because: $"{fileName} must still define {methodName}");
        var start = declaration.Index;

        var open = text.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0)
                return text[open..(i + 1)];
        }
        throw new InvalidOperationException($"Unbalanced braces in {fileName}::{methodName}");
    }

    private static string Normalize(string s) => Regex.Replace(s, @"\s+", " ");

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var backend = Path.Combine(dir.FullName, "Backend");
            if (Directory.Exists(Path.Combine(backend, "SheikhTravelSystem.Infrastructure")))
                return backend;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate Backend/ root.");
    }
}
