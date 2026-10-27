using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class FleetTripModeRepository(IDbConnectionFactory dbFactory) : IFleetTripModeRepository
{
    public async Task<FleetTripModeSettingsDto> GetSettingsAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await EnsureTenantGpsRowAsync(connection, tenantId, cancellationToken);

        var row = await connection.QuerySingleAsync<(string Mode, string? TimeZone)>(new CommandDefinition(
            """
            SELECT COALESCE(g.TripCreationMode, 'Booking') AS Mode,
                   COALESCE(b.TimeZone, @DefaultTz) AS TimeZone
            FROM Tenants t
            LEFT JOIN TenantGpsSettings g ON g.TenantId = t.Id
            LEFT JOIN TenantBranding b ON b.TenantId = t.Id
            WHERE t.Id = @TenantId
            """,
            new { TenantId = tenantId, DefaultTz = TenantTimeZoneHelper.DefaultTimeZoneId },
            cancellationToken: cancellationToken));

        return ToDto(row.Mode, row.TimeZone);
    }

    public async Task<FleetTripModeSettingsDto> UpsertSettingsAsync(
        int tenantId,
        UpsertFleetTripModeSettingsRequest body,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await EnsureTenantGpsRowAsync(connection, tenantId, cancellationToken);

        var mode = FleetTripCreationModeNames.ToStorage(body.TripCreationMode);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE TenantGpsSettings
            SET TripCreationMode = @Mode, UpdatedAt = GETUTCDATE()
            WHERE TenantId = @TenantId
            """,
            new { TenantId = tenantId, Mode = mode },
            cancellationToken: cancellationToken));

        return await GetSettingsAsync(tenantId, cancellationToken);
    }

    public async Task<FleetTripCreationMode> GetModeForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var mode = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            SELECT COALESCE(g.TripCreationMode, 'Booking')
            FROM Vehicles v
            LEFT JOIN TenantGpsSettings g ON g.TenantId = v.TenantId
            WHERE v.Id = @VehicleId AND v.IsDeleted = 0
            """,
            new { VehicleId = vehicleId },
            cancellationToken: cancellationToken));

        return FleetTripCreationModeNames.Parse(mode);
    }

    public async Task<(int TenantId, string? TimeZoneId)> GetVehicleTenantContextAsync(
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<(int TenantId, string? TimeZoneId)>(new CommandDefinition(
            """
            SELECT v.TenantId, b.TimeZone AS TimeZoneId
            FROM Vehicles v
            LEFT JOIN TenantBranding b ON b.TenantId = v.TenantId
            WHERE v.Id = @VehicleId AND v.IsDeleted = 0
            """,
            new { VehicleId = vehicleId },
            cancellationToken: cancellationToken));
    }

    private static FleetTripModeSettingsDto ToDto(string modeStorage, string? timeZoneId)
    {
        var mode = FleetTripCreationModeNames.Parse(modeStorage);
        var description = mode switch
        {
            FleetTripCreationMode.AutoGpsDaily =>
                "GPS auto-trip: one trip per vehicle per day from first movement to last stop. Operational /trips stay separate.",
            _ =>
                "Booking mode: create trips from bookings on /trips. GPS trips use stop-based segment detection."
        };

        return new FleetTripModeSettingsDto(
            mode,
            timeZoneId ?? TenantTimeZoneHelper.DefaultTimeZoneId,
            description);
    }

    private static async Task EnsureTenantGpsRowAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            IF NOT EXISTS (SELECT 1 FROM TenantGpsSettings WHERE TenantId = @TenantId)
                INSERT INTO TenantGpsSettings (TenantId, TripCreationMode) VALUES (@TenantId, 'Booking');
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
    }
}
