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
            var roleUsers = await connection.QueryAsync<int>(new CommandDefinition("""
                SELECT Id FROM Users
                WHERE IsDeleted = 0 AND IsActive = 1 AND TenantId = @TenantId AND Role = @Role
                """,
                new { TenantId = tenantId, Role = (int)role },
                cancellationToken: cancellationToken));

            foreach (var id in roleUsers)
                userIds.Add(id);
        }

        foreach (var contextualUserId in await ResolveContextualUserIdsAsync(connection, request, tenantId, cancellationToken))
            userIds.Add(contextualUserId);

        return userIds.ToList();
    }

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
            // Role-based recipients (Admin/Finance) are resolved above; Customers has no UserId link.
            "booking_created" or "payment_received" => [],

            "trip_driver_assigned" or "trip_started" or "trip_completed" or "trip_delayed"
                or "trip_cancelled" or "trip_updated" or "trip_driver_arriving" => await QueryTripContextUserIds(
                    connection, referenceId, tenantId, cancellationToken),

            _ => []
        };
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

    private static async Task<IReadOnlyList<int>> QueryUserIds(
        System.Data.IDbConnection connection,
        string sql,
        int referenceId,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<int>(new CommandDefinition(
            sql,
            new { ReferenceId = referenceId, TenantId = tenantId },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }
}
