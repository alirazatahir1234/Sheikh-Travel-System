using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Production symptom: Fleet dashboard shows TotalVehicles &gt; 0 while GET /vehicles
/// returns an empty page for FLEET_MANAGER / GPS_OPERATOR users.
///
/// Cause: Roles.ScopeLevel = Branch clamps Vehicles by Users/UserRoles.BranchId, while
/// GetFleetDashboardQuery counts the whole tenant. Vehicles on another branch (or a
/// mismatched home branch) disappear from the list.
///
/// Fix: treat operational fleet roles as company-wide for data scope (branch filtering
/// remains available via UI filters). BRANCH_MANAGER stays Branch-scoped.
/// </summary>
public static class FleetListCompanyScopeMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var hasScopeLevel = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = N'Roles' AND COLUMN_NAME = N'ScopeLevel'
            ) THEN 1 ELSE 0 END
            """, cancellationToken: cancellationToken));

        if (hasScopeLevel == 0)
        {
            logger.LogWarning("FleetListCompanyScopeMigration skipped — Roles.ScopeLevel column missing.");
            return;
        }

        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Roles
            SET ScopeLevel = N'Company'
            WHERE Code IN (
                N'FLEET_MANAGER',
                N'DISPATCHER',
                N'DRIVER_MANAGER',
                N'GPS_OPERATOR'
            )
            AND (
                ScopeLevel IS NULL
                OR LTRIM(RTRIM(ScopeLevel)) = N''
                OR ScopeLevel IN (N'Branch', N'Assigned', N'Tenant')
            );
            """, cancellationToken: cancellationToken));

        logger.LogInformation(
            "FleetListCompanyScopeMigration applied — set ScopeLevel=Company on {Count} role row(s).",
            updated);
    }
}
