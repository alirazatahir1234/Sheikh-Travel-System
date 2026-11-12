using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Vehicle photos/documents whose <c>VehicleDocuments.TenantId</c> is NULL or differs from the owning
/// vehicle's company (legacy rows, vehicles moved between companies) were shown on the vehicle profile
/// (image join ignores the document tenant) but missing in the edit wizard (tenant-filtered documents API).
/// A document always belongs to its vehicle's company — realign it.
/// </summary>
public static class VehicleDocumentTenantAlignMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE vd
            SET vd.TenantId = v.TenantId, vd.UpdatedAt = GETUTCDATE()
            FROM VehicleDocuments vd
            INNER JOIN Vehicles v ON v.Id = vd.VehicleId
            WHERE v.TenantId IS NOT NULL
              AND (vd.TenantId IS NULL OR vd.TenantId <> v.TenantId);
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "VehicleDocumentTenantAlignMigration realigned {Count} vehicle document row(s) to their vehicle's company.",
            updated);
    }
}
