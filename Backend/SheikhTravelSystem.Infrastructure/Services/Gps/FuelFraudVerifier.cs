using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.FuelFraud;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class FuelFraudVerifier(
    IFuelFraudRepository fuelFraudRepository,
    IGpsAlertWriter alertWriter,
    INotificationDecisionEngine decisionEngine,
    IOptions<GpsSettings> gpsSettings,
    ILogger<FuelFraudVerifier> logger) : IFuelFraudVerifier
{
    public const string EventType = "fuel_fraud";

    public async Task EvaluateAsync(
        IDbConnection connection,
        IngestPositionDto dto,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        var opts = gpsSettings.Value.FuelFraud;
        if (!opts.Enabled)
            return;

        var pending = await fuelFraudRepository.ListPendingForVehicleAsync(
            dto.VehicleId, opts.MaxOrderAgeMinutes, cancellationToken);
        if (pending.Count == 0)
            return;

        var leavingStation = dto.Speed >= opts.LeaveSpeedKmh;
        foreach (var order in pending)
        {
            var ageMinutes = (timestamp - order.CreatedAtUtc).TotalMinutes;
            if (!leavingStation && ageMinutes < opts.MinWaitBeforeVerifyMinutes)
                continue;

            if (!dto.FuelLevel.HasValue && ageMinutes < opts.ForceVerifyAfterMinutes)
                continue;

            var result = FuelFraudVerification.Evaluate(
                order.OrderedLiters,
                order.FuelLevelAtOrderPercent,
                dto.FuelLevel,
                order.TankCapacityLiters,
                opts.ToleranceLiters);

            int? alertId = null;
            if (result.Status == FuelFraudVerification.StatusFraudAlert)
            {
                var station = string.IsNullOrWhiteSpace(order.StationName)
                    ? "fuel station"
                    : order.StationName.Trim();
                var vehicleLabel = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                    """
                    SELECT COALESCE(NULLIF(LTRIM(RTRIM(RegistrationNumber)), N''), Name)
                    FROM Vehicles WHERE Id = @Id AND IsDeleted = 0
                    """,
                    new { Id = dto.VehicleId },
                    cancellationToken: cancellationToken));

                var reg = string.IsNullOrWhiteSpace(vehicleLabel) ? $"Vehicle #{dto.VehicleId}" : vehicleLabel;
                var shortLiters = result.ShortfallLiters ?? 0;
                var actual = result.ActualIncreaseLiters ?? 0;
                var message =
                    $"Fuel fraud alert — {reg}: ordered {order.OrderedLiters:N1} L at {station}, "
                    + $"OBD shows ~{actual:N1} L added (~{shortLiters:N1} L short).";

                alertId = await alertWriter.InsertAsync(
                    connection,
                    dto.VehicleId,
                    dto.Latitude,
                    dto.Longitude,
                    dto.Speed,
                    EventType,
                    message,
                    timestamp,
                    driverId: order.DriverId,
                    cancellationToken: cancellationToken);

                if (alertId > 0)
                {
                    await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                        EventType,
                        "Fuel fraud detected",
                        message,
                        NotificationType.EngineCommandSent,
                        ReferenceId: dto.VehicleId,
                        AlertEventId: alertId,
                        TenantId: order.TenantId,
                        SuggestedPriority: 3,
                        RequestedChannels:
                        [
                            NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Push
                        ]), cancellationToken);
                }
            }

            await fuelFraudRepository.CompleteVerificationAsync(new FuelPumpOrderVerificationUpdate(
                order.Id,
                result.Status,
                result.FuelLevelAfterPercent,
                result.ActualIncreaseLiters,
                result.ShortfallLiters,
                alertId,
                timestamp), cancellationToken);

            logger.LogInformation(
                "Fuel pump order {OrderId} verified as {Status} for vehicle {VehicleId}",
                order.Id, result.Status, dto.VehicleId);
        }
    }
}
