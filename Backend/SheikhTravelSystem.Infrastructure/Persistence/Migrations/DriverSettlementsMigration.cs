using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Creates DriverSettlements (+ optional lines) for commercial MVP driver payouts.
/// </summary>
public static class DriverSettlementsMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.DriverSettlements', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.DriverSettlements (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DriverSettlements PRIMARY KEY,
                    TenantId INT NOT NULL,
                    DriverId INT NOT NULL,
                    PeriodStart DATE NOT NULL,
                    PeriodEnd DATE NOT NULL,
                    GrossCollected DECIMAL(18,2) NOT NULL CONSTRAINT DF_DriverSettlements_Gross DEFAULT (0),
                    FuelDeducted DECIMAL(18,2) NOT NULL CONSTRAINT DF_DriverSettlements_Fuel DEFAULT (0),
                    AllowanceAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_DriverSettlements_Allowance DEFAULT (0),
                    Adjustments DECIMAL(18,2) NOT NULL CONSTRAINT DF_DriverSettlements_Adj DEFAULT (0),
                    Payable DECIMAL(18,2) NOT NULL,
                    Status NVARCHAR(20) NOT NULL,
                    PaidAt DATETIME2 NULL,
                    Reference NVARCHAR(100) NULL,
                    Notes NVARCHAR(500) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_DriverSettlements_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_DriverSettlements_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_DriverSettlements_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
                );

                CREATE INDEX IX_DriverSettlements_Tenant_Driver
                    ON dbo.DriverSettlements (TenantId, DriverId) WHERE IsDeleted = 0;

                CREATE INDEX IX_DriverSettlements_Tenant_Status
                    ON dbo.DriverSettlements (TenantId, Status) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.DriverSettlementLines', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.DriverSettlementLines (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DriverSettlementLines PRIMARY KEY,
                    SettlementId INT NOT NULL,
                    TenantId INT NOT NULL,
                    LineType NVARCHAR(30) NOT NULL,
                    Description NVARCHAR(300) NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    ReferenceId INT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_DriverSettlementLines_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT FK_DriverSettlementLines_Settlement
                        FOREIGN KEY (SettlementId) REFERENCES dbo.DriverSettlements(Id)
                );

                CREATE INDEX IX_DriverSettlementLines_Settlement
                    ON dbo.DriverSettlementLines (SettlementId);
            END

            IF OBJECT_ID(N'dbo.Permissions', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = N'Settlement.View')
                    INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                    VALUES (N'Finance', N'Settlement.View', N'View driver settlements');
                IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = N'Settlement.Manage')
                    INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                    VALUES (N'Finance', N'Settlement.Manage', N'Generate/approve/pay driver settlements');
                IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = N'Invoice.Manage')
                    INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                    VALUES (N'Finance', N'Invoice.Manage', N'Issue and void customer invoices');
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("DriverSettlementsMigration applied.");
    }
}
