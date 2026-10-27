using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Trip-linked customer invoices, due dates, email tracking, fleet-manager invoice view.
/// </summary>
public static class ClientTripBillingMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.CustomerInvoices', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.CustomerInvoices', 'BookingId') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM sys.columns c
                        INNER JOIN sys.tables t ON t.object_id = c.object_id
                        WHERE t.name = N'CustomerInvoices' AND c.name = N'BookingId' AND c.is_nullable = 0)
                        ALTER TABLE dbo.CustomerInvoices ALTER COLUMN BookingId INT NULL;
                END

                IF COL_LENGTH('dbo.CustomerInvoices', 'TripId') IS NULL
                    ALTER TABLE dbo.CustomerInvoices ADD TripId INT NULL;
                IF COL_LENGTH('dbo.CustomerInvoices', 'TripNumber') IS NULL
                    ALTER TABLE dbo.CustomerInvoices ADD TripNumber NVARCHAR(30) NULL;
                IF COL_LENGTH('dbo.CustomerInvoices', 'CustomerEmail') IS NULL
                    ALTER TABLE dbo.CustomerInvoices ADD CustomerEmail NVARCHAR(200) NULL;
                IF COL_LENGTH('dbo.CustomerInvoices', 'DueDate') IS NULL
                    ALTER TABLE dbo.CustomerInvoices ADD DueDate DATETIME2 NULL;
                IF COL_LENGTH('dbo.CustomerInvoices', 'LastEmailedAt') IS NULL
                    ALTER TABLE dbo.CustomerInvoices ADD LastEmailedAt DATETIME2 NULL;
                IF COL_LENGTH('dbo.CustomerInvoices', 'EmailSentCount') IS NULL
                    ALTER TABLE dbo.CustomerInvoices ADD EmailSentCount INT NOT NULL
                        CONSTRAINT DF_CustomerInvoices_EmailSentCount DEFAULT (0);

                -- Legacy Issued → Pending for open balances.
                EXEC(N'UPDATE dbo.CustomerInvoices SET Status = N''Pending''
                    WHERE Status = N''Issued'' AND IsDeleted = 0');

                EXEC(N'UPDATE dbo.CustomerInvoices SET DueDate = DATEADD(day, 14, IssuedAt)
                    WHERE DueDate IS NULL AND IssuedAt IS NOT NULL AND IsDeleted = 0');

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_CustomerInvoices_TenantId_TripId'
                      AND object_id = OBJECT_ID(N'dbo.CustomerInvoices'))
                    EXEC(N'CREATE INDEX IX_CustomerInvoices_TenantId_TripId
                        ON dbo.CustomerInvoices (TenantId, TripId) WHERE IsDeleted = 0 AND TripId IS NOT NULL');
            END
            """, cancellationToken: cancellationToken));

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection,
            "FLEET_MANAGER",
            [FinancePermissions.InvoiceView],
            cancellationToken);

        logger.LogInformation("ClientTripBillingMigration applied (trip invoices + FLEET_MANAGER Invoice.View).");
    }
}
