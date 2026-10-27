using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Driver in-app wallet, ledger, and cash-out requests (tenant-scoped).
/// </summary>
public static class DriverWalletMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.DriverWallets', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.DriverWallets (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DriverWallets PRIMARY KEY,
                    TenantId INT NOT NULL,
                    DriverId INT NOT NULL,
                    Balance DECIMAL(18,2) NOT NULL CONSTRAINT DF_DriverWallets_Balance DEFAULT (0),
                    Currency NVARCHAR(8) NOT NULL CONSTRAINT DF_DriverWallets_Currency DEFAULT (N'PKR'),
                    CashOutCadence NVARCHAR(16) NOT NULL CONSTRAINT DF_DriverWallets_Cadence DEFAULT (N'Weekly'),
                    BankAccountName NVARCHAR(200) NULL,
                    BankAccountNumber NVARCHAR(64) NULL,
                    BankName NVARCHAR(200) NULL,
                    JazzCashNumber NVARCHAR(32) NULL,
                    EasypaisaNumber NVARCHAR(32) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_DriverWallets_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_DriverWallets_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_DriverWallets_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT UQ_DriverWallets_Tenant_Driver UNIQUE (TenantId, DriverId)
                );
                CREATE INDEX IX_DriverWallets_Tenant ON dbo.DriverWallets (TenantId) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.DriverWalletLedger', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.DriverWalletLedger (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DriverWalletLedger PRIMARY KEY,
                    TenantId INT NOT NULL,
                    DriverId INT NOT NULL,
                    WalletId INT NOT NULL,
                    EntryType NVARCHAR(32) NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    BalanceAfter DECIMAL(18,2) NOT NULL,
                    ReferenceType NVARCHAR(64) NULL,
                    ReferenceId INT NULL,
                    Description NVARCHAR(500) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_DriverWalletLedger_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedByUserId INT NULL,
                    CONSTRAINT FK_DriverWalletLedger_Wallets FOREIGN KEY (WalletId) REFERENCES dbo.DriverWallets(Id)
                );
                CREATE INDEX IX_DriverWalletLedger_Tenant_Driver
                    ON dbo.DriverWalletLedger (TenantId, DriverId, CreatedAt DESC);
                CREATE UNIQUE INDEX UX_DriverWalletLedger_Idempotent
                    ON dbo.DriverWalletLedger (TenantId, ReferenceType, ReferenceId)
                    WHERE ReferenceType IS NOT NULL AND ReferenceId IS NOT NULL;
            END

            IF OBJECT_ID(N'dbo.DriverCashOutRequests', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.DriverCashOutRequests (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DriverCashOutRequests PRIMARY KEY,
                    TenantId INT NOT NULL,
                    DriverId INT NOT NULL,
                    WalletId INT NOT NULL,
                    GrossAmount DECIMAL(18,2) NOT NULL,
                    ProcessingFee DECIMAL(18,2) NOT NULL,
                    NetPayout DECIMAL(18,2) NOT NULL,
                    ProcessingFeeRate DECIMAL(9,6) NOT NULL,
                    Method NVARCHAR(32) NOT NULL,
                    DestinationSnapshot NVARCHAR(500) NULL,
                    Status NVARCHAR(32) NOT NULL CONSTRAINT DF_DriverCashOutRequests_Status DEFAULT (N'Pending'),
                    RequestedAt DATETIME2 NOT NULL CONSTRAINT DF_DriverCashOutRequests_RequestedAt DEFAULT (SYSUTCDATETIME()),
                    ProcessedAt DATETIME2 NULL,
                    ProcessedByUserId INT NULL,
                    PaymentReference NVARCHAR(128) NULL,
                    RejectionReason NVARCHAR(500) NULL,
                    Notes NVARCHAR(500) NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_DriverCashOutRequests_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_DriverCashOutRequests_Wallets FOREIGN KEY (WalletId) REFERENCES dbo.DriverWallets(Id)
                );
                CREATE INDEX IX_DriverCashOutRequests_Tenant_Status
                    ON dbo.DriverCashOutRequests (TenantId, Status, RequestedAt DESC) WHERE IsDeleted = 0;
                CREATE INDEX IX_DriverCashOutRequests_Tenant_Driver
                    ON dbo.DriverCashOutRequests (TenantId, DriverId, RequestedAt DESC) WHERE IsDeleted = 0;
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("DriverWalletMigration applied.");
    }
}
