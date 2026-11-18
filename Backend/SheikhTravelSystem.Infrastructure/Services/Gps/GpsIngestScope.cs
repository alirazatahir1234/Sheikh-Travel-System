using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

/// <summary>Who is submitting a GPS position (SG-01 Step 6a).</summary>
internal enum GpsIngestCaller
{
    /// <summary>Authenticated user carrying a driver identity claim.</summary>
    Driver,

    /// <summary>Authenticated non-driver user (endpoint already demands a GPS write permission).</summary>
    User,

    /// <summary>
    /// Traccar sync (background loop or manual-sync request) running inside the server-only
    /// <c>TraccarIngestProvenance</c> scope. Never derived from anything a client sends.
    /// </summary>
    TrustedSystem,

    /// <summary>Anything else — never allowed to ingest.</summary>
    Unauthenticated
}

/// <summary>
/// Ownership facts for one ingest payload, all evaluated against the vehicle's own tenant in a single query.
/// A null optional id (device / booking / driver) counts as "ok".
/// </summary>
internal sealed record GpsIngestFacts(
    int? VehicleTenantId,
    bool DeviceOk,
    bool BookingOk,
    bool DriverOk,
    bool DriverHasStartedBooking);

internal sealed record GpsIngestDecision(bool Allowed, int TenantId, string? Rejection)
{
    public static GpsIngestDecision Allow(int tenantId) => new(true, tenantId, null);
    public static GpsIngestDecision Reject(string message) => new(false, 0, message);
}

/// <summary>
/// SG-01 Step 6a — decides whether a GPS position may be written and for which tenant.
/// Pure logic so every cross-company case is unit-testable without a database.
/// </summary>
internal static class GpsIngestScopePolicy
{
    /// <summary>
    /// One message for every cross-company or relationship mismatch, so a rejection never reveals
    /// whether another company's vehicle, device, booking or driver exists.
    /// </summary>
    public const string ScopeRejection = "Position rejected.";

    public const string AuthenticationRequired = "Authentication required.";

    public const string DriverTripRequired = "GPS ingest only allowed during your active started trip for this vehicle.";

    /// <summary>
    /// Trusted only when the server-only Traccar provenance scope is active (H1: manual sync runs inside an
    /// authenticated HTTP request but must still use the Traccar path). Otherwise a logged-in user is a
    /// Driver (driver identity claim) or User, and everything else — including anonymous HTTP — is rejected.
    /// </summary>
    public static GpsIngestCaller Classify(int? userId, int? driverId, bool isTrustedTraccarIngest)
    {
        if (isTrustedTraccarIngest)
            return GpsIngestCaller.TrustedSystem;

        if (userId is > 0)
            return driverId is > 0 ? GpsIngestCaller.Driver : GpsIngestCaller.User;

        return GpsIngestCaller.Unauthenticated;
    }

    public static GpsIngestDecision Decide(GpsIngestCaller caller, int? callerTenantId, GpsIngestFacts facts)
    {
        if (caller == GpsIngestCaller.Unauthenticated)
            return GpsIngestDecision.Reject(AuthenticationRequired);

        if (facts.VehicleTenantId is not > 0)
            return GpsIngestDecision.Reject(ScopeRejection);

        var vehicleTenantId = facts.VehicleTenantId.Value;

        if (caller is GpsIngestCaller.User or GpsIngestCaller.Driver
            && callerTenantId != vehicleTenantId)
            return GpsIngestDecision.Reject(ScopeRejection);

        if (!facts.DeviceOk || !facts.BookingOk || !facts.DriverOk)
            return GpsIngestDecision.Reject(ScopeRejection);

        if (caller == GpsIngestCaller.Driver && !facts.DriverHasStartedBooking)
            return GpsIngestDecision.Reject(DriverTripRequired);

        return GpsIngestDecision.Allow(vehicleTenantId);
    }
}

/// <summary>SQL for <see cref="GpsIngestFacts"/>. Every relationship is checked against the vehicle's tenant.</summary>
internal static class GpsIngestScopeSql
{
    // Device ownership follows TrackerTenantSql: d.TenantId, or the vehicle's tenant when the device row has none
    // (Traccar-provisioned devices start unowned). Booking must be the vehicle's own booking or carry a trip on it.
    public const string Facts = """
        SELECT v.TenantId AS VehicleTenantId,
               CAST(CASE WHEN @GpsDeviceId IS NULL THEN 1
                         WHEN EXISTS (
                             SELECT 1 FROM GpsDevices d
                             WHERE d.Id = @GpsDeviceId AND d.IsDeleted = 0
                               AND d.VehicleId = v.Id
                               AND (d.TenantId = v.TenantId OR d.TenantId IS NULL)) THEN 1
                         ELSE 0 END AS bit) AS DeviceOk,
               CAST(CASE WHEN @BookingId IS NULL THEN 1
                         WHEN EXISTS (
                             SELECT 1 FROM Bookings b
                             WHERE b.Id = @BookingId AND b.IsDeleted = 0 AND b.TenantId = v.TenantId
                               AND (b.VehicleId = v.Id
                                    OR EXISTS (SELECT 1 FROM Trips t
                                               WHERE t.BookingId = b.Id AND t.VehicleId = v.Id
                                                 AND t.TenantId = v.TenantId AND t.IsDeleted = 0))) THEN 1
                         ELSE 0 END AS bit) AS BookingOk,
               CAST(CASE WHEN @DriverId IS NULL THEN 1
                         WHEN EXISTS (
                             SELECT 1 FROM Drivers dr
                             WHERE dr.Id = @DriverId AND dr.IsDeleted = 0 AND dr.TenantId = v.TenantId) THEN 1
                         ELSE 0 END AS bit) AS DriverOk,
               CAST(CASE WHEN @DriverId IS NOT NULL AND EXISTS (
                             SELECT 1 FROM Bookings sb
                             WHERE sb.DriverId = @DriverId AND sb.VehicleId = v.Id AND sb.Status = @Started
                               AND sb.IsDeleted = 0 AND sb.TenantId = v.TenantId) THEN 1
                         ELSE 0 END AS bit) AS DriverHasStartedBooking
        FROM Vehicles v
        WHERE v.Id = @VehicleId AND v.IsDeleted = 0
        """;

    public static async Task<GpsIngestFacts> LoadAsync(
        IDbConnection connection, IngestPositionDto dto, CancellationToken cancellationToken)
    {
        var facts = await connection.QueryFirstOrDefaultAsync<GpsIngestFacts>(new CommandDefinition(
            Facts,
            new
            {
                dto.VehicleId,
                dto.GpsDeviceId,
                dto.BookingId,
                dto.DriverId,
                Started = (int)BookingStatus.Started
            },
            cancellationToken: cancellationToken));

        return facts ?? new GpsIngestFacts(null, false, false, false, false);
    }
}
