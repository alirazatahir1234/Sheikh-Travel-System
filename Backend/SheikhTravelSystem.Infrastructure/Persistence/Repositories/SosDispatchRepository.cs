using System.Text.Json;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.SosDispatch;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class SosDispatchRepository(IDbConnectionFactory dbFactory) : ISosDispatchRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<VerifiedTeamCandidateRow>> GetVerifiedTeamCandidatesAsync(
        int tenantId, int excludeDriverId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<VerifiedTeamCandidateRow>(new CommandDefinition(
            """
            SELECT
                d.Id AS DriverId,
                d.UserId,
                d.FullName,
                d.Phone,
                vcl.Latitude,
                vcl.Longitude
            FROM Drivers d
            OUTER APPLY (
                SELECT TOP 1 ah.VehicleId
                FROM AssignmentHistory ah
                WHERE ah.DriverId = d.Id AND ah.TenantId = @TenantId AND ah.IsDeleted = 0 AND ah.Status = N'Active'
                ORDER BY ah.StartAt DESC
            ) activeVeh
            LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = activeVeh.VehicleId
            WHERE d.TenantId = @TenantId AND d.IsDeleted = 0 AND d.IsActive = 1
              AND d.VerificationStatus = N'Verified'
              AND d.Id <> @ExcludeDriverId
              AND d.Status IN (@Available, @OnTrip)
            """,
            new
            {
                TenantId = tenantId,
                ExcludeDriverId = excludeDriverId,
                Available = (int)DriverStatus.Available,
                OnTrip = (int)DriverStatus.OnTrip
            },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<(decimal? SpeedKmh, string? RouteJson, string? RouteSummary)> GetLiveTelemetryAndRouteAsync(
        int tenantId, int driverId, int? vehicleId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var from = DateTime.UtcNow.AddMinutes(-45);
        var points = (await connection.QueryAsync<(double Lat, double Lng, decimal? Speed, DateTime Ts)>(
            new CommandDefinition(
                """
                SELECT TOP 40 Latitude AS Lat, Longitude AS Lng, Speed, RecordedAt AS Ts
                FROM GpsPositions
                WHERE RecordedAt >= @From
                  AND (DriverId = @DriverId OR (@VehicleId IS NOT NULL AND VehicleId = @VehicleId))
                ORDER BY RecordedAt DESC
                """,
                new { From = from, DriverId = driverId, VehicleId = vehicleId },
                cancellationToken: ct))).ToList();

        if (points.Count == 0)
            return (null, null, null);

        var latest = points[0];
        var ordered = points.OrderBy(p => p.Ts).ToList();
        double routeKm = 0;
        for (var i = 1; i < ordered.Count; i++)
        {
            var km = GeoDistanceHelper.HaversineKm(
                ordered[i - 1].Lat, ordered[i - 1].Lng, ordered[i].Lat, ordered[i].Lng);
            if (km.HasValue) routeKm += km.Value;
        }

        var json = JsonSerializer.Serialize(
            ordered.Select(p => new { lat = p.Lat, lng = p.Lng, speed = p.Speed, at = p.Ts }),
            JsonOpts);
        var summary = $"Last {Math.Round(routeKm, 1)} km over {ordered.Count} GPS points (45 min window)";
        return (latest.Speed, json, summary);
    }

    public async Task<int?> GetPrimaryTowingPartnerIdAsync(int tenantId, string regionCode, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 Id FROM SosRecoveryPartners
            WHERE TenantId = @TenantId AND RegionCode = @RegionCode AND PartnerType = N'Towing'
              AND IsActive = 1 AND IsDeleted = 0
            ORDER BY Id
            """,
            new { TenantId = tenantId, RegionCode = regionCode },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SosRecoveryPartnerDto>> ListPartnersAsync(int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<SosRecoveryPartnerDto>(new CommandDefinition(
            """
            SELECT Id, RegionCode, PartnerType, Name, Phone, Email, Notes, IsActive
            FROM SosRecoveryPartners
            WHERE TenantId = @TenantId AND IsDeleted = 0
            ORDER BY RegionCode, PartnerType, Name
            """,
            new { TenantId = tenantId },
            cancellationToken: ct))).ToList();
    }

    public async Task<int> UpsertPartnerAsync(
        int tenantId, int? id, UpsertSosRecoveryPartnerRequest request, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (id is int existingId)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE SosRecoveryPartners SET
                    RegionCode = @RegionCode, PartnerType = @PartnerType, Name = @Name,
                    Phone = @Phone, Email = @Email, Notes = @Notes, IsActive = @IsActive,
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
                """,
                new
                {
                    Id = existingId,
                    TenantId = tenantId,
                    request.RegionCode,
                    request.PartnerType,
                    request.Name,
                    request.Phone,
                    request.Email,
                    request.Notes,
                    request.IsActive
                },
                cancellationToken: ct));
            return existingId;
        }

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO SosRecoveryPartners
                (TenantId, RegionCode, PartnerType, Name, Phone, Email, Notes, IsActive)
            VALUES (@TenantId, @RegionCode, @PartnerType, @Name, @Phone, @Email, @Notes, @IsActive);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new
            {
                TenantId = tenantId,
                request.RegionCode,
                request.PartnerType,
                request.Name,
                request.Phone,
                request.Email,
                request.Notes,
                request.IsActive
            },
            cancellationToken: ct));
    }

    public async Task SoftDeletePartnerAsync(int tenantId, int id, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE SosRecoveryPartners SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = id, TenantId = tenantId },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SosEventListItemDto>> ListEventsAsync(
        int tenantId, string? status, int limit, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var cap = Math.Clamp(limit, 1, 200);
        return (await connection.QueryAsync<SosEventListItemDto>(new CommandDefinition(
            $"""
            SELECT TOP (@Limit)
                s.Id, s.CreatedAt, s.Status, s.DriverId, d.FullName AS DriverName, d.Phone AS DriverPhone,
                s.VehicleId, s.Latitude, s.Longitude, s.SpeedKmh, s.LastRouteSummary,
                s.RegionCode, s.DispatchMode, s.AssignedResponderDriverId,
                rd.FullName AS AssignedTeamName, s.DistanceKm, s.EtaMinutes, s.ConfirmationMessage,
                s.SuggestedPartnerId, p.Name AS SuggestedPartnerName, s.Message
            FROM DriverSosAlerts s
            INNER JOIN Drivers d ON d.Id = s.DriverId
            LEFT JOIN Drivers rd ON rd.Id = s.AssignedResponderDriverId
            LEFT JOIN SosRecoveryPartners p ON p.Id = s.SuggestedPartnerId
            WHERE s.TenantId = @TenantId AND s.IsDeleted = 0
              AND (@Status IS NULL OR s.Status = @Status)
            ORDER BY s.CreatedAt DESC
            """,
            new { TenantId = tenantId, Status = status, Limit = cap },
            cancellationToken: ct))).ToList();
    }

    public async Task<SosEventListItemDto?> GetEventAsync(int tenantId, int id, CancellationToken ct = default)
    {
        var list = await ListEventsAsync(tenantId, null, 200, ct);
        return list.FirstOrDefault(e => e.Id == id);
    }

    public async Task<DriverSosStatusDto?> GetDriverSosStatusAsync(
        int tenantId, int driverId, int sosId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DriverSosStatusDto>(new CommandDefinition(
            """
            SELECT s.Id, s.Status, s.DispatchMode, rd.FullName AS AssignedTeamName,
                   s.EtaMinutes, s.ConfirmationMessage, s.CreatedAt, s.ResolvedAt
            FROM DriverSosAlerts s
            LEFT JOIN Drivers rd ON rd.Id = s.AssignedResponderDriverId
            WHERE s.Id = @Id AND s.TenantId = @TenantId AND s.DriverId = @DriverId AND s.IsDeleted = 0
            """,
            new { Id = sosId, TenantId = tenantId, DriverId = driverId },
            cancellationToken: ct));
    }

    public async Task ResolveEventAsync(int tenantId, int id, string resolvedBy, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE DriverSosAlerts SET Status = N'Resolved', ResolvedAt = GETUTCDATE(), ResolvedBy = @ResolvedBy
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """,
            new { Id = id, TenantId = tenantId, ResolvedBy = resolvedBy },
            cancellationToken: ct));
    }

    public async Task<int> InsertSosAlertWithDispatchAsync(
        int tenantId,
        int driverId,
        int? vehicleId,
        int? bookingId,
        double? lat,
        double? lng,
        string? message,
        SosDispatchPlan plan,
        DateTime createdAt,
        CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO DriverSosAlerts
                (TenantId, DriverId, VehicleId, BookingId, Latitude, Longitude, Message, Status, CreatedAt,
                 SpeedKmh, LastRouteJson, LastRouteSummary, RegionCode, AssignedResponderDriverId,
                 DispatchMode, DistanceKm, EtaMinutes, ConfirmationMessage, SuggestedPartnerId, IsDeleted)
            VALUES
                (@TenantId, @DriverId, @VehicleId, @BookingId, @Latitude, @Longitude, @Message, N'Open', @CreatedAt,
                 @SpeedKmh, @LastRouteJson, @LastRouteSummary, @RegionCode, @AssignedResponderDriverId,
                 @DispatchMode, @DistanceKm, @EtaMinutes, @ConfirmationMessage, @SuggestedPartnerId, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new
            {
                TenantId = tenantId,
                DriverId = driverId,
                VehicleId = vehicleId,
                BookingId = bookingId,
                Latitude = lat,
                Longitude = lng,
                Message = message,
                CreatedAt = createdAt,
                plan.SpeedKmh,
                plan.LastRouteJson,
                plan.LastRouteSummary,
                plan.RegionCode,
                AssignedResponderDriverId = plan.AssignedResponderDriverId,
                plan.DispatchMode,
                plan.DistanceKm,
                plan.EtaMinutes,
                plan.ConfirmationMessage,
                plan.SuggestedPartnerId
            },
            cancellationToken: ct));
    }

    private static class GeoDistanceHelper
    {
        public static double? HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371d;
            static double Rad(double d) => d * Math.PI / 180d;
            var dLat = Rad(lat2 - lat1);
            var dLon = Rad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                    + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return Math.Round(R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)), 2);
        }
    }
}
