using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.API.Controllers;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Infrastructure.Services.Gps;

namespace SheikhTravelSystem.Tests.Security.Sg01;

/// <summary>
/// SG-01 Step 6a — GPS ingest, ETA and history export must never cross a company boundary.
/// The ingest decision is pure (<see cref="GpsIngestScopePolicy"/>), so every cross-company case is
/// exercised directly; the SQL that feeds it and the ETA / export queries are pinned on their text.
/// </summary>
public class Sg01GpsIngestIsolationTests
{
    private static readonly string Backend = FindBackendRoot();

    private const int MyTenant = 1;
    private const int OtherTenant = 2;

    private static GpsIngestFacts Facts(
        int? vehicleTenant = MyTenant, bool device = true, bool booking = true, bool driver = true, bool started = true)
        => new(vehicleTenant, device, booking, driver, started);

    private static GpsIngestCaller Caller(string name) => Enum.Parse<GpsIngestCaller>(name);

    // ---- caller classification -----------------------------------------------------------

    [Fact]
    public void Trusted_Traccar_system_ingest_is_classified_and_uses_the_vehicles_own_tenant()
    {
        var caller = GpsIngestScopePolicy.Classify(userId: null, driverId: null, isTrustedTraccarIngest: true);
        caller.Should().Be(GpsIngestCaller.TrustedSystem);

        var decision = GpsIngestScopePolicy.Decide(caller, callerTenantId: null, Facts(vehicleTenant: 7));
        decision.Allowed.Should().BeTrue();
        decision.TenantId.Should().Be(7);
    }

    [Fact]
    public void Unauthenticated_callers_are_rejected()
    {
        // Anonymous HTTP request, or a background call outside the Traccar provenance scope.
        var caller = GpsIngestScopePolicy.Classify(null, null, isTrustedTraccarIngest: false);
        caller.Should().Be(GpsIngestCaller.Unauthenticated);

        var decision = GpsIngestScopePolicy.Decide(caller, null, Facts());
        decision.Allowed.Should().BeFalse();
        decision.Rejection.Should().Be(GpsIngestScopePolicy.AuthenticationRequired);
    }

    [Fact]
    public void Driver_is_identified_by_driver_claim_not_role_name()
    {
        GpsIngestScopePolicy.Classify(userId: 5, driverId: 9, isTrustedTraccarIngest: false)
            .Should().Be(GpsIngestCaller.Driver);
        GpsIngestScopePolicy.Classify(userId: 5, driverId: null, isTrustedTraccarIngest: false)
            .Should().Be(GpsIngestCaller.User);
        GpsIngestScopePolicy.Classify(userId: null, driverId: 9, isTrustedTraccarIngest: false)
            .Should().Be(GpsIngestCaller.Unauthenticated, "a driver claim without an authenticated user is not a driver");

        MethodBody("GpsTrackingRepository.cs", "IngestPositionAsync")
            .Should().NotContain("Role ==", "authorization is by identity/permission, never by role name");
    }

    // ---- cross-company rejection ------------------------------------------------------------

    [Theory]
    [InlineData("User")]
    [InlineData("Driver")]
    public void Cross_company_vehicle_is_rejected(string callerName)
    {
        var caller = Caller(callerName);
        var decision = GpsIngestScopePolicy.Decide(caller, MyTenant, Facts(vehicleTenant: OtherTenant));

        decision.Allowed.Should().BeFalse();
        decision.Rejection.Should().Be(GpsIngestScopePolicy.ScopeRejection,
            "a driver must not learn another company's vehicle exists from a trip-specific message");
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Driver")]
    [InlineData("TrustedSystem")]
    public void Cross_company_or_unrelated_device_is_rejected(string callerName)
        => GpsIngestScopePolicy.Decide(Caller(callerName), MyTenant, Facts(device: false))
            .Rejection.Should().Be(GpsIngestScopePolicy.ScopeRejection);

    [Theory]
    [InlineData("User")]
    [InlineData("Driver")]
    [InlineData("TrustedSystem")]
    public void Cross_company_or_unrelated_booking_is_rejected(string callerName)
        => GpsIngestScopePolicy.Decide(Caller(callerName), MyTenant, Facts(booking: false))
            .Rejection.Should().Be(GpsIngestScopePolicy.ScopeRejection);

    [Theory]
    [InlineData("User")]
    [InlineData("Driver")]
    [InlineData("TrustedSystem")]
    public void Cross_company_driver_is_rejected(string callerName)
        => GpsIngestScopePolicy.Decide(Caller(callerName), MyTenant, Facts(driver: false))
            .Rejection.Should().Be(GpsIngestScopePolicy.ScopeRejection);

    [Fact]
    public void Missing_and_foreign_vehicles_get_the_same_answer()
    {
        var missing = GpsIngestScopePolicy.Decide(GpsIngestCaller.User, MyTenant, Facts(vehicleTenant: null));
        var foreign = GpsIngestScopePolicy.Decide(GpsIngestCaller.User, MyTenant, Facts(vehicleTenant: OtherTenant));

        missing.Rejection.Should().Be(foreign.Rejection, "a rejection must not reveal that another company's vehicle exists");
    }

    [Fact]
    public void Authenticated_user_without_tenant_context_is_rejected()
        => GpsIngestScopePolicy.Decide(GpsIngestCaller.User, callerTenantId: null, Facts())
            .Allowed.Should().BeFalse();

    [Fact]
    public void Own_company_driver_needs_a_started_booking_on_the_vehicle()
    {
        GpsIngestScopePolicy.Decide(GpsIngestCaller.Driver, MyTenant, Facts(started: false))
            .Rejection.Should().Be(GpsIngestScopePolicy.DriverTripRequired);
        GpsIngestScopePolicy.Decide(GpsIngestCaller.Driver, MyTenant, Facts())
            .Allowed.Should().BeTrue();
    }

    [Fact]
    public void Own_company_user_payload_is_allowed_for_the_callers_tenant()
    {
        var decision = GpsIngestScopePolicy.Decide(GpsIngestCaller.User, MyTenant, Facts());
        decision.Allowed.Should().BeTrue();
        decision.TenantId.Should().Be(MyTenant);
    }

    // ---- the SQL that feeds the decision -----------------------------------------------------

    [Fact]
    public void Ownership_facts_query_checks_every_id_against_the_vehicle_tenant()
    {
        var sql = Normalize(GpsIngestScopeSql.Facts);

        sql.Should().Contain("FROM Vehicles v WHERE v.Id = @VehicleId AND v.IsDeleted = 0");
        sql.Should().Contain("d.VehicleId = v.Id AND (d.TenantId = v.TenantId OR d.TenantId IS NULL)");
        sql.Should().Contain("b.TenantId = v.TenantId");
        sql.Should().Contain("t.VehicleId = v.Id AND t.TenantId = v.TenantId");
        sql.Should().Contain("dr.TenantId = v.TenantId");
        sql.Should().Contain("sb.DriverId = @DriverId AND sb.VehicleId = v.Id AND sb.Status = @Started AND sb.IsDeleted = 0 AND sb.TenantId = v.TenantId");
    }

    [Fact]
    public void Ingest_decides_scope_before_reading_or_writing_anything_else()
    {
        var body = MethodBody("GpsTrackingRepository.cs", "IngestPositionAsync");
        var decide = body.IndexOf("GpsIngestScopePolicy.Decide", StringComparison.Ordinal);

        decide.Should().BeGreaterThan(0);
        body.Should().Contain("isTrustedTraccarIngest: SheikhTravelSystem.Infrastructure.Traccar.TraccarIngestProvenance.IsActive");
        body.IndexOf("FROM VehicleCurrentLocation", StringComparison.Ordinal).Should().BeGreaterThan(decide);
        body.IndexOf("telemetryUpdater.UpdateAsync", StringComparison.Ordinal).Should().BeGreaterThan(decide);
        body.IndexOf("positionIngestion.IngestAsync", StringComparison.Ordinal).Should().BeGreaterThan(decide);
    }

    [Fact]
    public void Ingest_telemetry_writes_are_guarded_by_the_validated_vehicle()
    {
        Regex.Matches(MethodBody("GpsTrackingRepository.cs", "IngestPositionAsync"), "expectedVehicleId: dto.VehicleId")
            .Count.Should().Be(2);
        Normalize(MethodBody("GpsDeviceTelemetryUpdater.cs", "UpdateAsync"))
            .Should().Contain("(@ExpectedVehicleId IS NULL OR VehicleId = @ExpectedVehicleId)");
    }

    [Fact]
    public void Booking_resolution_is_bound_to_the_tenant()
    {
        var sql = Normalize(MethodBody("GpsPositionIngestionHelper.cs", "ResolveActiveBookingIdAsync"));
        sql.Should().Contain("SELECT Id FROM Bookings WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0");
        sql.Should().Contain("WHERE VehicleId = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0");
    }

    [Theory]
    [InlineData("EvaluateLowFuelAsync")]
    [InlineData("EvaluateLowBatteryAsync")]
    [InlineData("EvaluatePowerCutAsync")]
    [InlineData("EvaluateGpsUnreliableAsync")]
    [InlineData("EvaluateGpsLostAsync")]
    public void Ingest_alert_dedup_reads_are_bound_to_the_tenant(string method)
        => Normalize(MethodBody("GpsTrackingRepository.Ingest.cs", method))
            .Should().Contain("INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId");

    // ---- write permission: GPS.View alone cannot ingest -------------------------------------

    [Theory]
    [InlineData(typeof(GpsTrackingController), "IngestPosition")]
    [InlineData(typeof(TrackingController), "UpdateLocation")]
    public void Position_write_endpoints_require_a_gps_write_permission(Type controller, string action)
    {
        var method = controller.GetMethod(action, BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull();

        var policies = method!.GetCustomAttributes<RequirePermissionAttribute>(inherit: true)
            .Select(a => a.Policy)
            .ToList();

        policies.Should().Contain(GpsPermissions.DeviceManage,
            "GPS.View is a read permission and must not authorise writing positions on its own");
        policies.Should().NotContain(AnalyticsPermissions.GpsView,
            "the action-level requirement must be the write permission; GPS.View stays only at class level");
    }

    // ---- ETA and history export isolation ---------------------------------------------------

    [Fact]
    public void Eta_reads_only_the_callers_booking_vehicle_and_position()
    {
        var sql = Normalize(MethodBody("GpsTrackingRepository.cs", "GetGpsEtaAsync"));

        sql.Should().Contain("tenantContext.GetRequiredTenantId()");
        sql.Should().Contain("WHERE b.Id = @Id AND b.TenantId = @TenantId AND b.IsDeleted = 0");
        sql.Should().Contain("LEFT JOIN Routes r ON r.Id = b.RouteId AND r.TenantId = b.TenantId");
        sql.Should().Contain("t.BookingId = b.Id AND t.TenantId = b.TenantId");
        sql.Should().Contain("INNER JOIN Vehicles v ON v.Id = vcl.VehicleId AND v.TenantId = @TenantId");
        sql.Should().Contain("SELECT Name FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId");
    }

    [Fact]
    public void History_export_checks_vehicle_ownership_before_loading_positions()
    {
        var body = MethodBody("GpsTrackingRepository.cs", "GetHistoryExportAsync");
        var ownership = body.IndexOf("SELECT 1 FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId", StringComparison.Ordinal);
        var load = body.IndexOf("LoadFullPositionsAsync(", StringComparison.Ordinal);

        ownership.Should().BeGreaterThan(0);
        load.Should().BeGreaterThan(ownership, "no position or Traccar call may happen before the ownership check");
        body.Should().Contain("FailResponse(\"Vehicle not found.\")");
    }

    [Fact]
    public void History_position_loader_scopes_device_and_positions_to_the_tenant()
    {
        var sql = Normalize(MethodBody("GpsTrackingRepository.MovedHelpers.cs", "LoadFullPositionsAsync"));

        sql.Should().Contain("INNER JOIN Vehicles v ON v.Id = d.VehicleId AND v.TenantId = @TenantId");
        sql.Should().Contain("INNER JOIN Vehicles v ON v.Id = p.VehicleId AND v.TenantId = @TenantId");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static string MethodBody(string fileName, string methodName)
    {
        var path = Directory.EnumerateFiles(Backend, fileName, SearchOption.AllDirectories)
            .First(p => !p.Contains("/obj/") && !p.Contains("/bin/"));
        var text = File.ReadAllText(path);

        var declaration = Regex.Match(text, $@"\bTask\b[^;=]*?\s{Regex.Escape(methodName)}\s*\(");
        declaration.Success.Should().BeTrue(because: $"{fileName} must still define {methodName}");

        var open = text.IndexOf('{', declaration.Index);
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
