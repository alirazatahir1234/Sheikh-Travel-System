using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class GpsTrackingRepository
{
    private async Task EvaluateWhatsAppTripProximityAsync(
        IngestPositionDto dto,
        DateTime recordedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            var speedKmh = (double)dto.Speed;
            TrackingPositionCache.Set(dto.VehicleId, new TrackingPositionDto(
                dto.Latitude,
                dto.Longitude,
                dto.Heading,
                speedKmh,
                recordedAt));

            using var connection = dbFactory.CreateConnection();
            var trip = await connection.QuerySingleOrDefaultAsync<(int TripId, int TenantId)>(new CommandDefinition("""
                SELECT TOP 1 Id AS TripId, TenantId
                FROM Trips
                WHERE VehicleId = @VehicleId AND IsDeleted = 0 AND Status = @Started
                ORDER BY Id DESC
                """, new { dto.VehicleId, Started = (int)TripStatus.Started }, cancellationToken: cancellationToken));

            if (trip.TripId == 0)
                return;

            await mediator.Send(new EvaluateTripProximityCommand(
                trip.TenantId,
                trip.TripId,
                dto.Latitude,
                dto.Longitude,
                speedKmh,
                recordedAt), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "WhatsApp trip proximity evaluation skipped for vehicle {VehicleId}", dto.VehicleId);
        }
    }
}
