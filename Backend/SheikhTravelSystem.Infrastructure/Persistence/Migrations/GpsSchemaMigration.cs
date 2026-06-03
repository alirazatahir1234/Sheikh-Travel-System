using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsSchemaMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsDevices')
            CREATE TABLE GpsDevices (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                VehicleId INT NULL,
                UniqueId NVARCHAR(100) NOT NULL,
                Name NVARCHAR(200) NOT NULL,
                Protocol NVARCHAR(50) NULL,
                SupportsEngineCutoff BIT NOT NULL DEFAULT 0,
                LastIgnition BIT NULL,
                LastSeenAt DATETIME2 NULL,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CreatedBy NVARCHAR(100) NULL,
                UpdatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_GpsDevices_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id),
                CONSTRAINT UQ_GpsDevices_UniqueId UNIQUE (UniqueId)
            );
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleTracking' AND COLUMN_NAME = 'Heading')
                ALTER TABLE VehicleTracking ADD Heading FLOAT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleTracking' AND COLUMN_NAME = 'Altitude')
                ALTER TABLE VehicleTracking ADD Altitude FLOAT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleTracking' AND COLUMN_NAME = 'Ignition')
                ALTER TABLE VehicleTracking ADD Ignition BIT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleTracking' AND COLUMN_NAME = 'GpsDeviceId')
                ALTER TABLE VehicleTracking ADD GpsDeviceId INT NULL;
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Geofences')
            CREATE TABLE Geofences (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Name NVARCHAR(200) NOT NULL,
                AreaType NVARCHAR(20) NOT NULL DEFAULT 'circle',
                CenterLat FLOAT NOT NULL,
                CenterLng FLOAT NOT NULL,
                RadiusMeters FLOAT NOT NULL DEFAULT 500,
                GeoJson NVARCHAR(MAX) NULL,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CreatedBy NVARCHAR(100) NULL,
                UpdatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0
            );
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsAlertRules')
            CREATE TABLE GpsAlertRules (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                VehicleId INT NULL,
                SpeedLimitKmh DECIMAL(10,2) NULL,
                GeofenceId INT NULL,
                AlertOnEnter BIT NOT NULL DEFAULT 1,
                AlertOnExit BIT NOT NULL DEFAULT 1,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CreatedBy NVARCHAR(100) NULL,
                UpdatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_GpsAlertRules_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id),
                CONSTRAINT FK_GpsAlertRules_Geofences FOREIGN KEY (GeofenceId) REFERENCES Geofences(Id)
            );
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsAlertEvents')
            CREATE TABLE GpsAlertEvents (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                RuleId INT NULL,
                VehicleId INT NOT NULL,
                GeofenceId INT NULL,
                EventType NVARCHAR(50) NOT NULL,
                Latitude FLOAT NOT NULL,
                Longitude FLOAT NOT NULL,
                Speed DECIMAL(10,2) NOT NULL DEFAULT 0,
                Message NVARCHAR(500) NOT NULL,
                Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsAcknowledged BIT NOT NULL DEFAULT 0,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_GpsAlertEvents_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id),
                CONSTRAINT FK_GpsAlertEvents_Rules FOREIGN KEY (RuleId) REFERENCES GpsAlertRules(Id),
                CONSTRAINT FK_GpsAlertEvents_Geofences FOREIGN KEY (GeofenceId) REFERENCES Geofences(Id)
            );
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsDeviceCommands')
            CREATE TABLE GpsDeviceCommands (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                GpsDeviceId INT NOT NULL,
                CommandType NVARCHAR(50) NOT NULL,
                Status NVARCHAR(20) NOT NULL DEFAULT 'pending',
                RequestedBy NVARCHAR(100) NULL,
                RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CompletedAt DATETIME2 NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_GpsDeviceCommands_Devices FOREIGN KEY (GpsDeviceId) REFERENCES GpsDevices(Id)
            );
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VehicleTracking_GpsDeviceId')
                CREATE INDEX IX_VehicleTracking_GpsDeviceId ON VehicleTracking(GpsDeviceId, Timestamp) WHERE IsDeleted = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsAlertEvents_VehicleId')
                CREATE INDEX IX_GpsAlertEvents_VehicleId ON GpsAlertEvents(VehicleId, Timestamp DESC) WHERE IsDeleted = 0;
            """, cancellationToken: cancellationToken));

        await SeedGeofencesAsync(connection, logger, cancellationToken);
        await SeedDefaultSpeedRuleAsync(connection, logger, cancellationToken);

        logger.LogInformation("GPS schema migration completed.");
    }

    private static async Task SeedGeofencesAsync(System.Data.IDbConnection connection, ILogger logger, CancellationToken cancellationToken)
    {
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Geofences WHERE IsDeleted = 0", cancellationToken: cancellationToken));

        if (count > 0)
        {
            return;
        }

        const string sql = """
            INSERT INTO Geofences (Name, AreaType, CenterLat, CenterLng, RadiusMeters, IsActive, CreatedAt)
            VALUES
            (@Name, 'circle', @Lat, @Lng, @Radius, 1, GETUTCDATE())
            """;

        var samples = new[]
        {
            new { Name = "Sialkot Airport", Lat = 32.5356, Lng = 74.3639, Radius = 1500.0 },
            new { Name = "Lahore Airport", Lat = 31.5216, Lng = 74.4036, Radius = 2000.0 },
            new { Name = "Pasrur Bus Stand", Lat = 32.2620, Lng = 74.6630, Radius = 400.0 }
        };

        foreach (var s in samples)
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, s, cancellationToken: cancellationToken));
        }

        logger.LogInformation("Seeded {Count} sample geofences.", samples.Length);
    }

    private static async Task SeedDefaultSpeedRuleAsync(System.Data.IDbConnection connection, ILogger logger, CancellationToken cancellationToken)
    {
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM GpsAlertRules WHERE VehicleId IS NULL AND IsDeleted = 0", cancellationToken: cancellationToken));

        if (count > 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO GpsAlertRules (VehicleId, SpeedLimitKmh, AlertOnEnter, AlertOnExit, IsActive, CreatedAt)
            VALUES (NULL, 100, 0, 0, 1, GETUTCDATE())
            """, cancellationToken: cancellationToken));

        logger.LogInformation("Seeded default global speed alert rule (100 km/h).");
    }
}
