using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Infrastructure.Services.Gps;

namespace SheikhTravelSystem.Tests.Security.Sg01;

/// <summary>
/// SG-01 Step 6a, review finding M1 — runs the real <see cref="GpsIngestScopeSql.Facts"/> query and Dapper's
/// mapping into <see cref="GpsIngestFacts"/> against a Development SQL Server. READ-ONLY (SELECT only).
/// Opt-in like the Phase 8 live-SQL tests:
///   SHEIKHGO_SG01_SQL_CONNECTION  — Development connection string (from your user-secrets; never production)
///   SHEIKHGO_SG01_REQUIRE_LIVE_SQL=1 — fail instead of soft-skipping when the connection is missing
/// Without the connection string the tests are reported as SKIPPED (via <see cref="LiveSqlFactAttribute"/>), never
/// as passed. With SHEIKHGO_SG01_REQUIRE_LIVE_SQL=1 and no connection string they run and FAIL explicitly.
/// </summary>
public class Sg01GpsIngestFactsLiveSqlTests
{
    private const string ConnectionEnv = "SHEIKHGO_SG01_SQL_CONNECTION";
    private const string RequireEnv = "SHEIKHGO_SG01_REQUIRE_LIVE_SQL";

    internal const string ConnectionEnvName = ConnectionEnv;
    internal const string RequireEnvName = RequireEnv;

    private static string Connection()
    {
        var cs = Environment.GetEnvironmentVariable(ConnectionEnv);
        if (string.IsNullOrWhiteSpace(cs))
            Assert.Fail($"{RequireEnv}=1 but {ConnectionEnv} is not set.");
        return cs!;
    }

    private static IngestPositionDto ForVehicle(int vehicleId) =>
        new(vehicleId, DriverId: null, BookingId: null, GpsDeviceId: null, Latitude: 0, Longitude: 0, Speed: 0m);

    [LiveSqlFact]
    public async Task Facts_query_executes_and_maps_for_an_existing_vehicle()
    {
        var cs = Connection();

        await using var connection = new SqlConnection(cs);
        var vehicle = await connection.QueryFirstOrDefaultAsync<(int Id, int? TenantId)>(
            "SELECT TOP 1 Id, TenantId FROM Vehicles WHERE IsDeleted = 0 AND TenantId IS NOT NULL ORDER BY Id");
        vehicle.Id.Should().BeGreaterThan(0,
            "the live check needs at least one non-deleted vehicle with a TenantId in the Development database");

        var facts = await GpsIngestScopeSql.LoadAsync(connection, ForVehicle(vehicle.Id), CancellationToken.None);

        facts.VehicleTenantId.Should().Be(vehicle.TenantId);
        facts.DeviceOk.Should().BeTrue("a null device id is treated as ok");
        facts.BookingOk.Should().BeTrue();
        facts.DriverOk.Should().BeTrue();
        facts.DriverHasStartedBooking.Should().BeFalse("no driver id was supplied");
    }

    [LiveSqlFact]
    public async Task Facts_query_fails_closed_for_an_unknown_vehicle()
    {
        var cs = Connection();

        await using var connection = new SqlConnection(cs);
        var facts = await GpsIngestScopeSql.LoadAsync(connection, ForVehicle(int.MaxValue), CancellationToken.None);

        facts.VehicleTenantId.Should().BeNull();
        GpsIngestScopePolicy.Decide(GpsIngestCaller.TrustedSystem, null, facts).Allowed.Should().BeFalse();
    }
}

/// <summary>
/// A <see cref="FactAttribute"/> that is SKIPPED (shown as skipped, not passed) unless the live-SQL connection
/// string is configured — or unless live SQL is required, in which case the test runs and fails on the missing string.
/// </summary>
public sealed class LiveSqlFactAttribute : FactAttribute
{
    public LiveSqlFactAttribute()
    {
        var configured = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(Sg01GpsIngestFactsLiveSqlTests.ConnectionEnvName));
        var required = Environment.GetEnvironmentVariable(Sg01GpsIngestFactsLiveSqlTests.RequireEnvName) == "1";
        if (!configured && !required)
            Skip = $"Live SQL not configured: set {Sg01GpsIngestFactsLiveSqlTests.ConnectionEnvName} to a Development connection string.";
    }
}
