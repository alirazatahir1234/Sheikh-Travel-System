using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class GeofenceAlertNotifier(
    IDbConnectionFactory dbFactory,
    INotificationDecisionEngine decisionEngine,
    ILogger<GeofenceAlertNotifier> logger) : IGeofenceAlertNotifier
{
    public async Task NotifyAsync(GeofenceAlertNotice notice, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var notifyChannels = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT NotifyChannels FROM Geofences WHERE Id = @GeofenceId AND TenantId = @TenantId AND IsDeleted = 0",
            new { notice.GeofenceId, notice.TenantId },
            cancellationToken: cancellationToken));

        var chosen = GeofenceNotifyChannels.Parse(notifyChannels);
        var channels = chosen.Count == 0 ? notice.DefaultChannels : GeofenceNotifyChannels.ToDeliveryChannels(chosen);

        // Chosen users plus members of chosen roles, active and in this tenant only.
        var recipients = (await connection.QueryAsync<int>(new CommandDefinition(
            """
            SELECT u.Id
            FROM GeofenceRecipients r
            INNER JOIN Users u ON u.Id = r.UserId AND u.TenantId = @TenantId AND u.IsActive = 1 AND u.IsDeleted = 0
            WHERE r.GeofenceId = @GeofenceId AND r.TenantId = @TenantId AND r.UserId IS NOT NULL
            UNION
            SELECT u.Id
            FROM GeofenceRecipients r
            INNER JOIN UserRoles ur ON ur.RoleId = r.RoleId
            INNER JOIN Users u ON u.Id = ur.UserId AND u.TenantId = @TenantId AND u.IsActive = 1 AND u.IsDeleted = 0
            WHERE r.GeofenceId = @GeofenceId AND r.TenantId = @TenantId AND r.RoleId IS NOT NULL
            """,
            new { notice.GeofenceId, notice.TenantId },
            cancellationToken: cancellationToken))).ToList();

        if (channels.Count > 0)
        {
            await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                notice.EventType,
                notice.Title,
                notice.Message,
                NotificationType.TripDelayed,
                ReferenceId: notice.ReferenceId ?? notice.VehicleId,
                AlertEventId: notice.AlertEventId,
                TenantId: notice.TenantId,
                SuggestedPriority: notice.Priority,
                RequestedChannels: channels,
                TargetUserIds: recipients.Count > 0 ? recipients : null), cancellationToken);
        }

        if (!chosen.Contains(GeofenceNotifyChannels.DriverPush) || notice.DriverId is not int driverId)
            return;

        var driverUserId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT d.UserId
            FROM Drivers d
            INNER JOIN Users u ON u.Id = d.UserId AND u.TenantId = @TenantId AND u.IsActive = 1 AND u.IsDeleted = 0
            WHERE d.Id = @DriverId AND d.TenantId = @TenantId AND d.IsDeleted = 0
            """,
            new { DriverId = driverId, notice.TenantId },
            cancellationToken: cancellationToken));

        if (driverUserId is not int userId)
        {
            logger.LogDebug("Geofence {GeofenceId}: driver {DriverId} has no app account; driver push skipped",
                notice.GeofenceId, driverId);
            return;
        }

        // Its own event name, so the recipients' dispatch cooldown (per event and vehicle) does not swallow it.
        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            $"{notice.EventType}_driver",
            notice.Title,
            notice.Message,
            NotificationType.TripDelayed,
            ReferenceId: notice.VehicleId,
            TenantId: notice.TenantId,
            TargetUserId: userId,
            SuggestedPriority: notice.Priority,
            RequestedChannels: [NotificationChannels.Push]), cancellationToken);
    }
}
