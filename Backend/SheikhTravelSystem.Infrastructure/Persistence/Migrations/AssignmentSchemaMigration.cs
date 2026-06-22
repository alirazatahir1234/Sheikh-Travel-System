using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Extends AssignmentHistory with standalone assignment fields (AssignmentNo, Reason)
/// and creates the FleetAssignmentChangelog audit table.
/// Idempotent — safe to re-run.
/// </summary>
public static class AssignmentSchemaMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await ExtendAssignmentHistoryAsync(connection, cancellationToken);
        await CreateAssignmentChangelogAsync(connection, cancellationToken);
        await EnsureAssignmentIndexesAsync(connection, cancellationToken);

        logger.LogInformation("Assignment schema migration completed.");
    }

    private static async Task ExtendAssignmentHistoryAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AssignmentHistory' AND COLUMN_NAME = 'AssignmentNo')
                ALTER TABLE AssignmentHistory ADD AssignmentNo NVARCHAR(30) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AssignmentHistory' AND COLUMN_NAME = 'Reason')
                ALTER TABLE AssignmentHistory ADD Reason NVARCHAR(300) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AssignmentHistory' AND COLUMN_NAME = 'ModifiedBy')
                ALTER TABLE AssignmentHistory ADD ModifiedBy NVARCHAR(100) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AssignmentHistory' AND COLUMN_NAME = 'ModifiedAt')
                ALTER TABLE AssignmentHistory ADD ModifiedAt DATETIME2 NULL;
            """, cancellationToken: ct));

        // Backfill AssignmentNo for existing rows that don't have one
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE AssignmentHistory
            SET AssignmentNo = CONCAT(N'ASN-', RIGHT(CONCAT(N'000000', CAST(Id AS NVARCHAR)), 6))
            WHERE AssignmentNo IS NULL AND IsDeleted = 0;
            """, cancellationToken: ct));
    }

    private static async Task CreateAssignmentChangelogAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FleetAssignmentChangelog')
            CREATE TABLE FleetAssignmentChangelog (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                AssignmentId INT NOT NULL,
                OldVehicleId INT NULL,
                NewVehicleId INT NULL,
                OldDriverId INT NULL,
                NewDriverId INT NULL,
                ActionType NVARCHAR(40) NOT NULL,
                Reason NVARCHAR(300) NULL,
                CreatedBy NVARCHAR(100) NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT FK_FleetAssignmentChangelog_Assignment FOREIGN KEY (AssignmentId) REFERENCES AssignmentHistory(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task EnsureAssignmentIndexesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssignmentHistory_Tenant_Status')
                CREATE INDEX IX_AssignmentHistory_Tenant_Status
                ON AssignmentHistory (TenantId, Status, StartAt DESC) WHERE IsDeleted = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FleetAssignmentChangelog_AssignmentId')
                CREATE INDEX IX_FleetAssignmentChangelog_AssignmentId
                ON FleetAssignmentChangelog (AssignmentId, CreatedAt DESC);
            """, cancellationToken: ct));
    }
}
