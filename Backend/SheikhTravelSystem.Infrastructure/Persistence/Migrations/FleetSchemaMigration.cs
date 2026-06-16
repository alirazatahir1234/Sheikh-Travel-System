using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 4 fleet schema migration. Extends the existing operational tables
/// (Vehicles, Drivers, Maintenance) with fleet lifecycle columns instead of
/// creating parallel Fleet* master tables, and adds the new transactional
/// tables for assignments, reservations and expenses plus a status master.
/// All statements are idempotent so the migration is safe to re-run.
/// </summary>
public static class FleetSchemaMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await ExtendVehiclesAsync(connection, cancellationToken);
        await ExtendDriversAsync(connection, cancellationToken);
        await ExtendMaintenanceAsync(connection, cancellationToken);
        await ExtendGpsDevicesAsync(connection, cancellationToken);
        await CreateAssignmentHistoryAsync(connection, cancellationToken);
        await CreateVehicleReservationsAsync(connection, cancellationToken);
        await CreateFleetExpensesAsync(connection, cancellationToken);
        await CreateStatusCodesAsync(connection, cancellationToken);
        await SeedStatusCodesAsync(connection, cancellationToken);
        await EnsureGpsPositionIndexAsync(connection, cancellationToken);
        await EnsureVehicleV1SchemaAsync(connection, cancellationToken);

        logger.LogInformation("Fleet schema migration completed.");
    }

    /// <summary>
    /// V1 vehicle constraints: tenant-scoped plate uniqueness and GPS device FK.
    /// </summary>
    private static async Task EnsureVehicleV1SchemaAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Vehicles_Tenant_Registration')
                CREATE UNIQUE INDEX UX_Vehicles_Tenant_Registration
                ON Vehicles (TenantId, RegistrationNumber)
                WHERE IsDeleted = 0;

            IF NOT EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE name = 'FK_Vehicles_GpsDevices' AND parent_object_id = OBJECT_ID('Vehicles'))
            BEGIN
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsDevices')
                    ALTER TABLE Vehicles ADD CONSTRAINT FK_Vehicles_GpsDevices
                        FOREIGN KEY (GpsDeviceId) REFERENCES GpsDevices(Id);
            END
            """, cancellationToken: ct));
    }

    private static async Task ExtendVehiclesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'VehicleCode')
                ALTER TABLE Vehicles ADD VehicleCode NVARCHAR(40) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'VIN')
                ALTER TABLE Vehicles ADD VIN NVARCHAR(64) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'Make')
                ALTER TABLE Vehicles ADD Make NVARCHAR(100) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'Color')
                ALTER TABLE Vehicles ADD Color NVARCHAR(40) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'VehicleType')
                ALTER TABLE Vehicles ADD VehicleType NVARCHAR(60) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'EngineNo')
                ALTER TABLE Vehicles ADD EngineNo NVARCHAR(80) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'ChassisNo')
                ALTER TABLE Vehicles ADD ChassisNo NVARCHAR(80) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'GpsDeviceId')
                ALTER TABLE Vehicles ADD GpsDeviceId INT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'PurchaseDate')
                ALTER TABLE Vehicles ADD PurchaseDate DATETIME2 NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'PurchasePrice')
                ALTER TABLE Vehicles ADD PurchasePrice DECIMAL(18,2) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'BranchId')
                ALTER TABLE Vehicles ADD BranchId INT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'DepartmentId')
                ALTER TABLE Vehicles ADD DepartmentId INT NULL;
            """, cancellationToken: ct));
    }

    private static async Task ExtendDriversAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Drivers' AND COLUMN_NAME = 'DriverCode')
                ALTER TABLE Drivers ADD DriverCode NVARCHAR(40) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Drivers' AND COLUMN_NAME = 'Nationality')
                ALTER TABLE Drivers ADD Nationality NVARCHAR(80) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Drivers' AND COLUMN_NAME = 'Email')
                ALTER TABLE Drivers ADD Email NVARCHAR(200) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Drivers' AND COLUMN_NAME = 'HireDate')
                ALTER TABLE Drivers ADD HireDate DATETIME2 NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Drivers' AND COLUMN_NAME = 'PhotoUrl')
                ALTER TABLE Drivers ADD PhotoUrl NVARCHAR(500) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Drivers' AND COLUMN_NAME = 'VerificationStatus')
                ALTER TABLE Drivers ADD VerificationStatus NVARCHAR(30) NOT NULL DEFAULT N'Pending';
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Drivers' AND COLUMN_NAME = 'BranchId')
                ALTER TABLE Drivers ADD BranchId INT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Drivers' AND COLUMN_NAME = 'DepartmentId')
                ALTER TABLE Drivers ADD DepartmentId INT NULL;
            """, cancellationToken: ct));
    }

    private static async Task ExtendMaintenanceAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'MaintenanceType')
                ALTER TABLE Maintenance ADD MaintenanceType NVARCHAR(60) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'OdometerReading')
                ALTER TABLE Maintenance ADD OdometerReading DECIMAL(12,2) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'NextDueMileage')
                ALTER TABLE Maintenance ADD NextDueMileage DECIMAL(12,2) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'BranchId')
                ALTER TABLE Maintenance ADD BranchId INT NULL;
            """, cancellationToken: ct));
    }

    private static async Task ExtendGpsDevicesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsDevices')
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'GpsDevices' AND COLUMN_NAME = 'Vendor')
                    ALTER TABLE GpsDevices ADD Vendor NVARCHAR(60) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'GpsDevices' AND COLUMN_NAME = 'Model')
                    ALTER TABLE GpsDevices ADD Model NVARCHAR(80) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'GpsDevices' AND COLUMN_NAME = 'SimNumber')
                    ALTER TABLE GpsDevices ADD SimNumber NVARCHAR(40) NULL;
            END
            """, cancellationToken: ct));
    }

    private static async Task CreateAssignmentHistoryAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AssignmentHistory')
            CREATE TABLE AssignmentHistory (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                VehicleId INT NOT NULL,
                DriverId INT NULL,
                BookingId INT NULL,
                AssignmentType NVARCHAR(30) NOT NULL DEFAULT N'Trip',
                Status NVARCHAR(30) NOT NULL DEFAULT N'Active',
                StartAt DATETIME2 NOT NULL,
                EndAt DATETIME2 NULL,
                Notes NVARCHAR(500) NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CreatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_AssignmentHistory_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id),
                CONSTRAINT FK_AssignmentHistory_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id)
            );
            """, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssignmentHistory_Vehicle')
                CREATE INDEX IX_AssignmentHistory_Vehicle ON AssignmentHistory (VehicleId, StartAt DESC) WHERE IsDeleted = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssignmentHistory_Driver')
                CREATE INDEX IX_AssignmentHistory_Driver ON AssignmentHistory (DriverId, StartAt DESC) WHERE IsDeleted = 0;
            """, cancellationToken: ct));
    }

    private static async Task CreateVehicleReservationsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'VehicleReservations')
            CREATE TABLE VehicleReservations (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                VehicleId INT NOT NULL,
                DriverId INT NULL,
                ReservedFrom DATETIME2 NOT NULL,
                ReservedTo DATETIME2 NOT NULL,
                Reason NVARCHAR(300) NULL,
                Status NVARCHAR(30) NOT NULL DEFAULT N'Reserved',
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CreatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_VehicleReservations_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateFleetExpensesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FleetExpenses')
            CREATE TABLE FleetExpenses (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                VehicleId INT NULL,
                DriverId INT NULL,
                Category NVARCHAR(60) NOT NULL,
                Amount DECIMAL(18,2) NOT NULL,
                Currency NVARCHAR(10) NOT NULL DEFAULT N'AED',
                ExpenseDate DATETIME2 NOT NULL,
                Description NVARCHAR(500) NULL,
                ReceiptUrl NVARCHAR(500) NULL,
                BranchId INT NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CreatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_FleetExpenses_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id)
            );
            """, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FleetExpenses_Vehicle_Date')
                CREATE INDEX IX_FleetExpenses_Vehicle_Date ON FleetExpenses (VehicleId, ExpenseDate DESC) WHERE IsDeleted = 0;
            """, cancellationToken: ct));
    }

    private static async Task CreateStatusCodesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FleetStatusCodes')
            CREATE TABLE FleetStatusCodes (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Category NVARCHAR(30) NOT NULL,
                Code NVARCHAR(30) NOT NULL,
                Label NVARCHAR(100) NOT NULL,
                ColorToken NVARCHAR(30) NULL,
                SortOrder INT NOT NULL DEFAULT 0,
                IsActive BIT NOT NULL DEFAULT 1,
                CONSTRAINT UQ_FleetStatusCodes_Category_Code UNIQUE (Category, Code)
            );
            """, cancellationToken: ct));
    }

    private static async Task SeedStatusCodesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var existing = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM FleetStatusCodes", cancellationToken: ct));
        if (existing > 0)
        {
            return;
        }

        var seeds = new (string Category, string Code, string Label, string Color, int Sort)[]
        {
            ("Vehicle", "New", "New", "primary", 1),
            ("Vehicle", "Available", "Available", "success", 2),
            ("Vehicle", "Assigned", "Assigned", "warning", 3),
            ("Vehicle", "OnTrip", "On Trip", "warning", 4),
            ("Vehicle", "Maintenance", "Maintenance", "danger", 5),
            ("Vehicle", "Reserved", "Reserved", "primary", 6),
            ("Vehicle", "Retired", "Retired", "muted", 7),
            ("Driver", "Available", "Available", "success", 1),
            ("Driver", "OnTrip", "On Trip", "warning", 2),
            ("Driver", "OffDuty", "Off Duty", "muted", 3),
            ("Driver", "Suspended", "Suspended", "danger", 4),
            ("Maintenance", "Scheduled", "Scheduled", "primary", 1),
            ("Maintenance", "InProgress", "In Progress", "warning", 2),
            ("Maintenance", "Completed", "Completed", "success", 3)
        };

        foreach (var s in seeds)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO FleetStatusCodes (Category, Code, Label, ColorToken, SortOrder)
                VALUES (@Category, @Code, @Label, @Color, @Sort);
                """, new { s.Category, s.Code, s.Label, s.Color, s.Sort }, cancellationToken: ct));
        }
    }

    private static async Task EnsureGpsPositionIndexAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsPositions')
            AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsPositions_Vehicle_Recorded_Desc')
                CREATE INDEX IX_GpsPositions_Vehicle_Recorded_Desc ON GpsPositions (VehicleId, RecordedAt DESC);
            """, cancellationToken: ct));
    }
}
