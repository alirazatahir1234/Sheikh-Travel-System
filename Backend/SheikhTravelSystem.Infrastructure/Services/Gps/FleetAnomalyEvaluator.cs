using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using FleetAnomalyInsert = SheikhTravelSystem.Application.Common.Interfaces.Repositories.FleetAnomalyInsert;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class FleetAnomalyEvaluator(
    IFleetEvAnomalyRepository anomalyRepository,
    INotificationDecisionEngine decisionEngine,
    ILogger<FleetAnomalyEvaluator> logger) : IFleetAnomalyEvaluator
{
    public async Task EvaluateAsync(
        IDbConnection connection,
        IngestPositionDto dto,
        decimal? previousSpeedKmh,
        DateTime timestampUtc,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TenantId FROM Vehicles WHERE Id = @VehicleId AND IsDeleted = 0",
            new { dto.VehicleId }, cancellationToken: cancellationToken));

        if (!tenantId.HasValue || tenantId <= 0)
            return;

        var ctx = await connection.QuerySingleOrDefaultAsync<ActiveTripAnomalyContext>(
            new CommandDefinition("""
                SELECT TOP 1 t.DriverId, t.RouteId, t.Id AS TripId,
                       t.PickupLatitude, t.PickupLongitude, t.DestinationLatitude, t.DestinationLongitude
                FROM Trips t
                WHERE t.VehicleId = @VehicleId AND t.TenantId = @TenantId AND t.IsDeleted = 0
                  AND t.Status IN (5, 6, 7)
                ORDER BY t.PlannedStart DESC
                """, new { dto.VehicleId, TenantId = tenantId.Value }, cancellationToken: cancellationToken));

        var driverId = ctx?.DriverId;
        await TryNightDrivingAsync(connection, tenantId.Value, dto, driverId, timestampUtc, cancellationToken);
        await TrySuddenSpeedDropAsync(tenantId.Value, dto, driverId, previousSpeedKmh, timestampUtc, cancellationToken);
        if (ctx is not null)
            await TryOffRouteAsync(connection, tenantId.Value, dto, ctx, timestampUtc, cancellationToken);
    }

    private async Task TryNightDrivingAsync(
        IDbConnection connection,
        int tenantId,
        IngestPositionDto dto,
        int? driverId,
        DateTime timestampUtc,
        CancellationToken cancellationToken)
    {
        var hour = timestampUtc.Hour;
        if (hour is >= 6 and < 22)
            return;

        if (dto.Speed < 15)
            return;

        var known = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS(
                SELECT 1 FROM GpsPositions p
                WHERE p.VehicleId = @VehicleId
                  AND p.RecordedAt >= DATEADD(DAY, -30, @Now)
                  AND p.Latitude IS NOT NULL AND p.Longitude IS NOT NULL
                  AND (6371000 * ACOS(
                        CASE WHEN COS(RADIANS(@Lat)) * COS(RADIANS(p.Latitude)) *
                                  COS(RADIANS(p.Longitude) - RADIANS(@Lng)) +
                                  SIN(RADIANS(@Lat)) * SIN(RADIANS(p.Latitude)) BETWEEN -1 AND 1
                             THEN COS(RADIANS(@Lat)) * COS(RADIANS(p.Latitude)) *
                                  COS(RADIANS(p.Longitude) - RADIANS(@Lng)) +
                                  SIN(RADIANS(@Lat)) * SIN(RADIANS(p.Latitude))
                             ELSE 1 END
                  )) < 3000
            ) THEN 1 ELSE 0 END
            """, new { dto.VehicleId, dto.Latitude, dto.Longitude, Now = timestampUtc },
            cancellationToken: cancellationToken));

        if (known)
            return;

        await RaiseAsync(tenantId, dto, driverId, "night_unknown_area", "high",
            $"Night driving ({timestampUtc:HH:mm} UTC) in an area with no recent trip history.",
            cancellationToken);
    }

    private async Task TrySuddenSpeedDropAsync(
        int tenantId,
        IngestPositionDto dto,
        int? driverId,
        decimal? previousSpeedKmh,
        DateTime timestampUtc,
        CancellationToken cancellationToken)
    {
        if (previousSpeedKmh is null or < 40)
            return;

        if (previousSpeedKmh >= 40 && dto.Speed <= 5 && (previousSpeedKmh - dto.Speed) >= 30)
        {
            await RaiseAsync(tenantId, dto, driverId, "sudden_speed_drop", "medium",
                $"Speed dropped from {previousSpeedKmh:F0} to {dto.Speed:F0} km/h.",
                cancellationToken);
        }
    }

    private async Task TryOffRouteAsync(
        IDbConnection connection,
        int tenantId,
        IngestPositionDto dto,
        ActiveTripAnomalyContext ctx,
        DateTime timestampUtc,
        CancellationToken cancellationToken)
    {
        if (dto.Speed < 10)
            return;

        if (ctx.PickupLatitude is null || ctx.PickupLongitude is null
            || ctx.DestinationLatitude is null || ctx.DestinationLongitude is null)
            return;

        var distToCorridor = DistanceToSegmentMeters(
            dto.Latitude, dto.Longitude,
            ctx.PickupLatitude.Value, ctx.PickupLongitude.Value,
            ctx.DestinationLatitude.Value, ctx.DestinationLongitude.Value);

        if (distToCorridor <= 1200)
            return;

        await RaiseAsync(tenantId, dto, ctx.DriverId, "off_route", "high",
            $"Vehicle is ~{distToCorridor / 1000.0:F1} km off the trip corridor.",
            cancellationToken);
    }

    private sealed record ActiveTripAnomalyContext(
        int? DriverId,
        int? RouteId,
        int? TripId,
        double? PickupLatitude,
        double? PickupLongitude,
        double? DestinationLatitude,
        double? DestinationLongitude);

    private async Task RaiseAsync(
        int tenantId,
        IngestPositionDto dto,
        int? driverId,
        string type,
        string severity,
        string details,
        CancellationToken cancellationToken)
    {
        try
        {
            var recent = await anomalyRepository.ListAnomaliesAsync(tenantId, true, dto.VehicleId, cancellationToken);
            if (recent.Any(a => a.AnomalyType == type && (DateTime.UtcNow - a.CreatedAtUtc).TotalMinutes < 15))
                return;

            await anomalyRepository.InsertAnomalyAsync(new FleetAnomalyInsert(
                tenantId,
                dto.VehicleId,
                driverId ?? dto.DriverId,
                type,
                severity,
                dto.Latitude,
                dto.Longitude,
                dto.Speed,
                details), cancellationToken);

            await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                "fleet_anomaly",
                $"Fleet anomaly: {type.Replace('_', ' ')}",
                details,
                NotificationType.Sos,
                ReferenceId: dto.VehicleId,
                TenantId: tenantId,
                SuggestedPriority: 4,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Push
                ]), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fleet anomaly evaluation failed for vehicle {VehicleId}", dto.VehicleId);
        }
    }

    private static double DistanceToSegmentMeters(
        double pLat, double pLng,
        double aLat, double aLng,
        double bLat, double bLng)
    {
        // Project point onto segment AB in local equirectangular space (accurate enough for corridor checks).
        var x = (pLng - aLng) * Math.Cos((aLat + bLat) * Math.PI / 360);
        var y = pLat - aLat;
        var dx = (bLng - aLng) * Math.Cos((aLat + bLat) * Math.PI / 360);
        var dy = bLat - aLat;
        var lenSq = dx * dx + dy * dy;
        var t = lenSq <= 0 ? 0 : Math.Clamp((x * dx + y * dy) / lenSq, 0, 1);
        var projLat = aLat + t * dy;
        var projLng = aLng + t * (bLng - aLng);
        return HaversineMeters(pLat, pLng, projLat, projLng);
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
