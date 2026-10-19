using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class CustomerInvoicesMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.CustomerInvoices', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CustomerInvoices (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerInvoices PRIMARY KEY,
                    TenantId INT NOT NULL,
                    BookingId INT NOT NULL,
                    CustomerId INT NOT NULL,
                    InvoiceNumber NVARCHAR(40) NOT NULL,
                    TotalAmount DECIMAL(18,2) NOT NULL,
                    PaidAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CustomerInvoices_PaidAmount DEFAULT (0),
                    Balance DECIMAL(18,2) NOT NULL,
                    Status NVARCHAR(20) NOT NULL,
                    IssuedAt DATETIME2 NULL,
                    CustomerName NVARCHAR(200) NULL,
                    BookingNumber NVARCHAR(50) NULL,
                    RouteName NVARCHAR(300) NULL,
                    Notes NVARCHAR(500) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_CustomerInvoices_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_CustomerInvoices_IsDeleted DEFAULT (0),
                    CONSTRAINT UQ_CustomerInvoices_Tenant_Number UNIQUE (TenantId, InvoiceNumber),
                    CONSTRAINT FK_CustomerInvoices_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
                );
                CREATE INDEX IX_CustomerInvoices_TenantId_Status
                    ON dbo.CustomerInvoices (TenantId, Status) WHERE IsDeleted = 0;
                CREATE INDEX IX_CustomerInvoices_TenantId_BookingId
                    ON dbo.CustomerInvoices (TenantId, BookingId) WHERE IsDeleted = 0;
            END
            ELSE
            BEGIN
                IF COL_LENGTH('dbo.CustomerInvoices', 'CustomerName') IS NULL
                    ALTER TABLE dbo.CustomerInvoices ADD CustomerName NVARCHAR(200) NULL;
                IF COL_LENGTH('dbo.CustomerInvoices', 'BookingNumber') IS NULL
                    ALTER TABLE dbo.CustomerInvoices ADD BookingNumber NVARCHAR(50) NULL;
                IF COL_LENGTH('dbo.CustomerInvoices', 'RouteName') IS NULL
                    ALTER TABLE dbo.CustomerInvoices ADD RouteName NVARCHAR(300) NULL;
                IF COL_LENGTH('dbo.CustomerInvoices', 'Notes') IS NULL
                    ALTER TABLE dbo.CustomerInvoices ADD Notes NVARCHAR(500) NULL;

                -- Promote INT Status → NVARCHAR if an earlier draft migration created INT.
                IF EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'CustomerInvoices'
                      AND COLUMN_NAME = 'Status' AND DATA_TYPE = 'int')
                BEGIN
                    ALTER TABLE dbo.CustomerInvoices ADD StatusText NVARCHAR(20) NULL;
                    EXEC(N'UPDATE dbo.CustomerInvoices SET StatusText = CASE Status
                        WHEN 1 THEN N''Draft'' WHEN 2 THEN N''Issued'' WHEN 3 THEN N''Paid'' WHEN 4 THEN N''Void''
                        ELSE N''Issued'' END');
                    ALTER TABLE dbo.CustomerInvoices DROP COLUMN Status;
                    EXEC sp_rename 'dbo.CustomerInvoices.StatusText', 'Status', 'COLUMN';
                    ALTER TABLE dbo.CustomerInvoices ALTER COLUMN Status NVARCHAR(20) NOT NULL;
                END
            END

            IF OBJECT_ID(N'dbo.Modules', N'U') IS NOT NULL
            BEGIN
                UPDATE Modules SET Status = N'Active', Route = N'/invoices'
                WHERE ModuleCode = N'INVOICES';
            END

            IF OBJECT_ID(N'dbo.PlatformSettings', N'U') IS NOT NULL
            BEGIN
                INSERT INTO PlatformSettings (TenantId, Category, [Key], Value, DataType)
                SELECT t.Id, N'Numbering', N'InvoicePrefix', N'INV-', N'string'
                FROM Tenants t
                WHERE NOT EXISTS (
                    SELECT 1 FROM PlatformSettings ps
                    WHERE ps.TenantId = t.Id AND ps.Category = N'Numbering' AND ps.[Key] = N'InvoicePrefix'
                );
            END
            """, cancellationToken: cancellationToken));
        logger.LogInformation("CustomerInvoicesMigration applied.");
    }
}
