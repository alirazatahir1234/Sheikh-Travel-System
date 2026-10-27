using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Notifications;

public sealed class NotificationRecipientResolver(IDbConnectionFactory dbFactory)
{
    public async Task<IReadOnlyList<int>> ResolveUserIdsAsync(
        NotificationDecisionRequest request,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        if (request.TargetUserId is int directUserId)
            return [directUserId];

        if (request.Broadcast)
        {
            if (!NotificationRecipientPolicy.AllowsBroadcast(request.EventType))
                return [];

            using var broadcastConnection = dbFactory.CreateConnection();
            return (await broadcastConnection.QueryAsync<int>(new CommandDefinition("""
                SELECT Id FROM Users
                WHERE IsDeleted = 0 AND IsActive = 1 AND TenantId = @TenantId
                """,
                new { TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();
        }

        var roles = NotificationRecipientPolicy.RolesFor(request.EventType);
        var userIds = new HashSet<int>();

        using var connection = dbFactory.CreateConnection();
        foreach (var role in roles)
        {
            // Legacy Users.Role column (enum).
            var legacyUsers = await connection.QueryAsync<int>(new CommandDefinition("""
                SELECT Id FROM Users
                WHERE IsDeleted = 0 AND IsActive = 1 AND TenantId = @TenantId AND Role = @Role
                """,
                new { TenantId = tenantId, Role = (int)role },
                cancellationToken: cancellationToken));

            foreach (var id in legacyUsers)
                userIds.Add(id);

            // Platform RBAC (UserRoles → Roles.Code). Many ERP operators are TENANT_ADMIN /
            // FLEET_MANAGER via UserRoles while still needing booking/trip inbox items.
            var codes = PlatformCodesFor(role);
            if (codes.Count == 0)
                continue;

            var rbacUsers = await connection.QueryAsync<int>(new CommandDefinition("""
                SELECT DISTINCT u.Id
                FROM Users u
                INNER JOIN UserRoles ur ON ur.UserId = u.Id
                INNER JOIN Roles r ON r.Id = ur.RoleId AND r.TenantId = u.TenantId
                WHERE u.IsDeleted = 0 AND u.IsActive = 1 AND u.TenantId = @TenantId
                  AND r.IsActive = 1 AND r.Code IN @Codes
                """,
                new { TenantId = tenantId, Codes = codes },
                cancellationToken: cancellationToken));

            foreach (var id in rbacUsers)
                userIds.Add(id);
        }

        foreach (var contextualUserId in await ResolveContextualUserIdsAsync(connection, request, tenantId, cancellationToken))
            userIds.Add(contextualUserId);

        return userIds.ToList();
    }

    private static IReadOnlyList<string> PlatformCodesFor(UserRole role) => role switch
    {
        UserRole.Admin => ["SUPER_ADMIN", "TENANT_ADMIN", "FLEET_MANAGER", "DRIVER_MANAGER"],
        UserRole.Dispatcher => ["DISPATCHER", "GPS_OPERATOR"],
        UserRole.Driver => ["DRIVER"],
        UserRole.Accountant => ["ACCOUNTANT"],
        _ => []
    };

    private static async Task<IReadOnlyList<int>> ResolveContextualUserIdsAsync(
        System.Data.IDbConnection connection,
        NotificationDecisionRequest request,
        int tenantId,
        CancellationToken cancellationToken)
    {
        if (request.ReferenceId is not int referenceId)
            return [];

        return request.EventType.ToLowerInvariant() switch
        {
            // Ops roles resolved above; also include the booking's assigned driver (Drivers.UserId)
            // so sheikh-driver inbox sees the job when ERP creates a booking with DriverId set.
            "booking_created" => await QueryBookingDriverUserIds(
                connection, referenceId, tenantId, cancellationToken),

            "payment_received" => [],

            // trip_created stays ops-only (role policy); assigned driver is notified via trip_driver_assigned.
            "trip_driver_assigned" or "trip_started" or "trip_completed" or "trip_delayed"
                or "trip_cancelled" or "trip_updated" or "trip_driver_arriving" => await QueryTripContextUserIds(
                    connection, referenceId, tenantId, cancellationToken),

            "driver_fatigue_prompt" => await QueryFatigueEventDriverUserIds(
                connection, referenceId, tenantId, cancellationToken),

            _ => []
        };
    }

    private static async Task<IReadOnlyList<int>> QueryBookingDriverUserIds(
        System.Data.IDbConnection connection,
        int bookingId,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<int?>(new CommandDefinition("""
            SELECT DISTINCT d.UserId
            FROM Bookings b
            INNER JOIN Drivers d ON d.Id = b.DriverId
            WHERE b.Id = @BookingId AND b.TenantId = @TenantId
              AND b.IsDeleted = 0 AND d.UserId IS NOT NULL
            """,
            new { BookingId = bookingId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        return rows.Where(x => x is int id && id > 0).Select(x => x!.Value).ToList();
    }

    private static async Task<IReadOnlyList<int>> QueryFatigueEventDriverUserIds(
        System.Data.IDbConnection connection,
        int fatigueEventId,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<int?>(new CommandDefinition("""
            SELECT d.UserId
            FROM DriverFatigueEvents e
            INNER JOIN Drivers d ON d.Id = e.DriverId
            WHERE e.Id = @Id AND e.TenantId = @TenantId AND d.UserId IS NOT NULL
            """,
            new { Id = fatigueEventId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        return rows.Where(x => x is int id && id > 0).Select(x => x!.Value).ToList();
    }

    private static async Task<IReadOnlyList<int>> QueryTripContextUserIds(
        System.Data.IDbConnection connection,
        int tripId,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<int?>(new CommandDefinition("""
            SELECT DISTINCT d.UserId
            FROM Trips t
            LEFT JOIN Drivers d ON d.Id = t.DriverId
            WHERE t.Id = @TripId AND t.TenantId = @TenantId AND d.UserId IS NOT NULL
            """,
            new { TripId = tripId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        return rows.Where(x => x is int id && id > 0).Select(x => x!.Value).ToList();
    }
}
