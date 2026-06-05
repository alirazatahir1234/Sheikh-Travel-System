using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class TenantSchemaMigration
{
    private static readonly string[] TenantScopedTables =
    [
        "Users", "Customers", "Vehicles", "Drivers", "Routes", "Bookings", "Payments",
        "FuelLogs", "Maintenance", "GpsDevices", "Geofences", "GpsAlertRules", "GpsAlertEvents",
        "GpsDeviceCommands", "GpsPositions", "GpsTrips", "Notifications", "AuditLogs",
        "DriverAllowanceRules", "PromoCodes"
    ];

    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Tenants')
            CREATE TABLE Tenants (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Name NVARCHAR(200) NOT NULL,
                Slug NVARCHAR(100) NOT NULL,
                IsActive BIT NOT NULL DEFAULT 1,
                LogoUrl NVARCHAR(500) NULL,
                PrimaryColor NVARCHAR(20) NULL,
                EnabledModulesJson NVARCHAR(MAX) NULL,
                PlanId NVARCHAR(50) NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT UQ_Tenants_Slug UNIQUE (Slug)
            );
            """, cancellationToken: cancellationToken));

        var tenantCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM Tenants", cancellationToken: cancellationToken));

        if (tenantCount == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                SET IDENTITY_INSERT Tenants ON;
                INSERT INTO Tenants (Id, Name, Slug, IsActive, EnabledModulesJson, CreatedAt)
                VALUES (1, N'Sheikh Travel', N'default', 1,
                    N'["dashboard","bookings","vehicles","drivers","customers","routes","fuel-logs","maintenance","gps-tracking","payments","reports","driver-allowance-rules","users","audit-logs"]',
                    GETUTCDATE());
                SET IDENTITY_INSERT Tenants OFF;
                """, cancellationToken: cancellationToken));
            logger.LogInformation("Seeded default tenant (Id=1, slug=default).");
        }

        foreach (var table in TenantScopedTables)
        {
            await AddTenantIdColumnAsync(connection, table, cancellationToken);
        }

        await AddTenantIdToDriversUserIdAsync(connection, logger, cancellationToken);
        await AddVehicleDocumentsTableAsync(connection, cancellationToken);
        await AddCustomerFavoriteRoutesTableAsync(connection, cancellationToken);
        await AddSeatHoldsTableAsync(connection, cancellationToken);

        logger.LogInformation("Tenant schema migration completed.");
    }

    private static async Task AddTenantIdColumnAsync(
        System.Data.IDbConnection connection, string table, CancellationToken cancellationToken)
    {
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @Table",
            new { Table = table },
            cancellationToken: cancellationToken));

        if (exists == 0) return;

        var colExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @Table AND COLUMN_NAME = 'TenantId'",
            new { Table = table },
            cancellationToken: cancellationToken));

        if (colExists == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"ALTER TABLE [{table}] ADD TenantId INT NOT NULL DEFAULT 1",
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            $"UPDATE [{table}] SET TenantId = 1 WHERE TenantId IS NULL OR TenantId = 0",
            cancellationToken: cancellationToken));
    }

    private static async Task AddTenantIdToDriversUserIdAsync(
        System.Data.IDbConnection connection, ILogger logger, CancellationToken cancellationToken)
    {
        var colExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Drivers' AND COLUMN_NAME = 'UserId'",
            cancellationToken: cancellationToken));

        if (colExists == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "ALTER TABLE Drivers ADD UserId INT NULL",
                cancellationToken: cancellationToken));
            logger.LogInformation("Added Drivers.UserId for driver app login link.");
        }
    }

    private static async Task AddVehicleDocumentsTableAsync(System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'VehicleDocuments')
            CREATE TABLE VehicleDocuments (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NOT NULL DEFAULT 1,
                VehicleId INT NOT NULL,
                DocumentType NVARCHAR(50) NOT NULL,
                FileUrl NVARCHAR(1000) NULL,
                ExpiryDate DATETIME2 NULL,
                Notes NVARCHAR(500) NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_VehicleDocuments_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id)
            );
            """, cancellationToken: cancellationToken));
    }

    private static async Task AddCustomerFavoriteRoutesTableAsync(System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CustomerFavoriteRoutes')
            CREATE TABLE CustomerFavoriteRoutes (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                CustomerId INT NOT NULL,
                RouteId INT NOT NULL,
                Label NVARCHAR(100) NULL,
                SortOrder INT NOT NULL DEFAULT 0,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_CustomerFavoriteRoutes_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
                CONSTRAINT FK_CustomerFavoriteRoutes_Routes FOREIGN KEY (RouteId) REFERENCES Routes(Id),
                CONSTRAINT UQ_CustomerFavoriteRoutes UNIQUE (CustomerId, RouteId)
            );
            """, cancellationToken: cancellationToken));
    }

    private static async Task AddSeatHoldsTableAsync(System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'BookingSeatHolds')
            CREATE TABLE BookingSeatHolds (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NOT NULL DEFAULT 1,
                VehicleId INT NOT NULL,
                SeatLabel NVARCHAR(20) NOT NULL,
                PickupTime DATETIME2 NOT NULL,
                SessionId NVARCHAR(100) NOT NULL,
                ExpiresAt DATETIME2 NOT NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT UQ_BookingSeatHolds UNIQUE (VehicleId, SeatLabel, PickupTime)
            );
            """, cancellationToken: cancellationToken));
    }
}
