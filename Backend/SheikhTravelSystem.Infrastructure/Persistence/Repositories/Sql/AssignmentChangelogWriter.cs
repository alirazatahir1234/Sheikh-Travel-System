using System.Data;
using Dapper;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

internal static class AssignmentChangelogWriter
{
    public static Task WriteAsync(IDbConnection conn, int tenantId, int assignmentId,
        int? oldVehicleId, int? newVehicleId, int? oldDriverId, int? newDriverId,
        string action, string? reason, string? by, CancellationToken ct,
        IDbTransaction? transaction = null)
        => conn.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO FleetAssignmentChangelog (TenantId, AssignmentId, OldVehicleId, NewVehicleId,
              OldDriverId, NewDriverId, ActionType, Reason, CreatedBy, CreatedAt)
              VALUES (@TenantId, @AssignmentId, @OldVehicleId, @NewVehicleId,
              @OldDriverId, @NewDriverId, @ActionType, @Reason, @CreatedBy, GETUTCDATE())",
            new
            {
                TenantId = tenantId,
                AssignmentId = assignmentId,
                OldVehicleId = oldVehicleId,
                NewVehicleId = newVehicleId,
                OldDriverId = oldDriverId,
                NewDriverId = newDriverId,
                ActionType = action,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                CreatedBy = by
            }, transaction: transaction, cancellationToken: ct));
}
