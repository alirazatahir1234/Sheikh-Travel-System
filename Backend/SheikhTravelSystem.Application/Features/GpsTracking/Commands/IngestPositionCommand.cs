using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record IngestPositionCommand(IngestPositionDto Position) : IRequest<ApiResponse<bool>>;

public class IngestPositionCommandValidator : AbstractValidator<IngestPositionCommand>
{
    public IngestPositionCommandValidator()
    {
        RuleFor(x => x.Position.VehicleId).GreaterThan(0);
        RuleFor(x => x.Position.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Position.Longitude).InclusiveBetween(-180, 180);
    }
}

public class IngestPositionCommandHandler(
    IDbConnectionFactory dbFactory,
    INotificationService notifications,
    ILocationBroadcastService broadcaster)
    : IRequestHandler<IngestPositionCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(IngestPositionCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Position;
        var timestamp = DateTime.UtcNow;

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO VehicleTracking
              (VehicleId, DriverId, BookingId, GpsDeviceId, Latitude, Longitude, Speed, Heading, Altitude, Ignition, Timestamp, CreatedAt, IsDeleted)
              VALUES (@VehicleId, @DriverId, @BookingId, @GpsDeviceId, @Latitude, @Longitude, @Speed, @Heading, @Altitude, @Ignition, @Timestamp, @CreatedAt, 0)",
            new
            {
                dto.VehicleId,
                dto.DriverId,
                dto.BookingId,
                dto.GpsDeviceId,
                dto.Latitude,
                dto.Longitude,
                dto.Speed,
                dto.Heading,
                dto.Altitude,
                dto.Ignition,
                Timestamp = timestamp,
                CreatedAt = timestamp
            },
            cancellationToken: cancellationToken));

        if (dto.GpsDeviceId.HasValue)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE GpsDevices SET LastSeenAt = @Timestamp, LastIgnition = @Ignition, UpdatedAt = @Timestamp
                  WHERE Id = @Id AND IsDeleted = 0",
                new { Id = dto.GpsDeviceId.Value, Timestamp = timestamp, dto.Ignition },
                cancellationToken: cancellationToken));
        }

        await EvaluateAlertsAsync(connection, dto, timestamp, cancellationToken);

        await broadcaster.BroadcastLocationUpdateAsync(
            dto.VehicleId, dto.Latitude, dto.Longitude, dto.Speed, dto.Ignition, timestamp, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Position recorded.");
    }

    private async Task EvaluateAlertsAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var rules = await connection.QueryAsync<(int Id, int? VehicleId, decimal? SpeedLimitKmh, int? GeofenceId, bool AlertOnEnter, bool AlertOnExit)>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, SpeedLimitKmh, GeofenceId, AlertOnEnter, AlertOnExit
                  FROM GpsAlertRules WHERE IsActive = 1 AND IsDeleted = 0
                  AND (VehicleId IS NULL OR VehicleId = @VehicleId)",
                new { dto.VehicleId },
                cancellationToken: cancellationToken));

        foreach (var rule in rules)
        {
            if (rule.SpeedLimitKmh.HasValue && dto.Speed > rule.SpeedLimitKmh.Value)
            {
                await InsertAlertAsync(connection, rule.Id, dto, null, "speed_exceeded",
                    $"Speed {dto.Speed:F0} km/h exceeds limit {rule.SpeedLimitKmh:F0} km/h",
                    timestamp, cancellationToken);
                await notifications.CreateForAllAsync(
                    "Speed alert",
                    $"Vehicle #{dto.VehicleId} exceeded {rule.SpeedLimitKmh:F0} km/h (current {dto.Speed:F0} km/h).",
                    NotificationType.TripDelayed,
                    dto.VehicleId,
                    cancellationToken);
            }

            if (!rule.GeofenceId.HasValue)
            {
                continue;
            }

            var geofence = await connection.QueryFirstOrDefaultAsync<(int Id, string Name, double CenterLat, double CenterLng, double RadiusMeters)>(
                new CommandDefinition(
                    "SELECT Id, Name, CenterLat, CenterLng, RadiusMeters FROM Geofences WHERE Id = @Id AND IsActive = 1 AND IsDeleted = 0",
                    new { Id = rule.GeofenceId.Value },
                    cancellationToken: cancellationToken));

            if (geofence.Id == 0)
            {
                continue;
            }

            var inside = GpsGeoHelper.IsInsideCircle(
                dto.Latitude, dto.Longitude, geofence.CenterLat, geofence.CenterLng, geofence.RadiusMeters);

            var lastEvent = await connection.QueryFirstOrDefaultAsync<string?>(
                new CommandDefinition(
                    @"SELECT TOP 1 EventType FROM GpsAlertEvents
                      WHERE VehicleId = @VehicleId AND GeofenceId = @GeofenceId AND IsDeleted = 0
                      ORDER BY Timestamp DESC",
                    new { dto.VehicleId, GeofenceId = geofence.Id },
                    cancellationToken: cancellationToken));

            var wasInside = lastEvent == "geofence_enter";

            if (inside && !wasInside && rule.AlertOnEnter)
            {
                await InsertAlertAsync(connection, rule.Id, dto, geofence.Id, "geofence_enter",
                    $"Entered geofence: {geofence.Name}", timestamp, cancellationToken);
            }
            else if (!inside && wasInside && rule.AlertOnExit)
            {
                await InsertAlertAsync(connection, rule.Id, dto, geofence.Id, "geofence_exit",
                    $"Exited geofence: {geofence.Name}", timestamp, cancellationToken);
            }
        }
    }

    private static async Task InsertAlertAsync(
        System.Data.IDbConnection connection,
        int ruleId,
        IngestPositionDto dto,
        int? geofenceId,
        string eventType,
        string message,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO GpsAlertEvents
              (RuleId, VehicleId, GeofenceId, EventType, Latitude, Longitude, Speed, Message, Timestamp, CreatedAt, IsDeleted)
              VALUES (@RuleId, @VehicleId, @GeofenceId, @EventType, @Latitude, @Longitude, @Speed, @Message, @Timestamp, GETUTCDATE(), 0)",
            new
            {
                RuleId = ruleId,
                dto.VehicleId,
                GeofenceId = geofenceId,
                EventType = eventType,
                dto.Latitude,
                dto.Longitude,
                dto.Speed,
                Message = message,
                Timestamp = timestamp
            },
            cancellationToken: cancellationToken));
    }
}
