using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

/// <summary>
/// Shared notification dispatch for harsh braking alerts (position ingest + Traccar event sync).
/// Lives in Infrastructure because it loads vehicle labels via SQL (ADR-001 / ADR-003).
/// </summary>
public static class GpsHarshBrakingAlertDispatch
{
    public const int DedupMinutes = 10;

    public static async Task DispatchIfAllowedAsync(
        INotificationDecisionEngine decisionEngine,
        IDbConnection connection,
        int vehicleId,
        int alertId,
        decimal speedKmh,
        int? tenantId,
        CancellationToken cancellationToken = default)
    {
        tenantId ??= await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TenantId FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
            new { Id = vehicleId },
            cancellationToken: cancellationToken));

        if (tenantId is null or <= 0)
            return;

        var vehicle = await connection.QueryFirstOrDefaultAsync<(string? PlateNumber, string? Name)>(
            new CommandDefinition(
                "SELECT RegistrationNumber AS PlateNumber, Name FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                new { Id = vehicleId },
                cancellationToken: cancellationToken));

        var label = !string.IsNullOrWhiteSpace(vehicle.PlateNumber)
            ? vehicle.PlateNumber.Trim()
            : !string.IsNullOrWhiteSpace(vehicle.Name)
                ? vehicle.Name.Trim()
                : $"#{vehicleId}";

        var title = "Harsh braking";
        var body = speedKmh > 0
            ? $"Vehicle {label}: harsh braking detected (approx. {speedKmh:F0} km/h)."
            : $"Vehicle {label}: harsh braking / hard stop detected.";

        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            TraccarHarshEventMapper.HarshBraking,
            title,
            body,
            NotificationType.TripDelayed,
            ReferenceId: vehicleId,
            AlertEventId: alertId,
            TenantId: tenantId,
            SuggestedPriority: 3,
            RequestedChannels:
            [
                NotificationChannels.InApp, NotificationChannels.Browser,
                NotificationChannels.Push, NotificationChannels.Sms
            ]), cancellationToken);
    }
}
