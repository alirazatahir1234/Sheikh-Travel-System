using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Operational Trips module: Trips, TripStops, TripStatusHistory.
/// Idempotent — safe to re-run.
/// </summary>
public static class TripsModuleMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Trips')
            CREATE TABLE Trips (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NOT NULL DEFAULT 1,
                TripNumber NVARCHAR(30) NOT NULL DEFAULT '',
                BookingId INT NULL,
                CustomerId INT NOT NULL,
                RouteId INT NULL,
                TripName NVARCHAR(200) NOT NULL,
                TripType INT NOT NULL DEFAULT 2,
                PickupAddress NVARCHAR(500) NULL,
                PickupLatitude FLOAT NULL,
                PickupLongitude FLOAT NULL,
                DestinationAddress NVARCHAR(500) NULL,
                DestinationLatitude FLOAT NULL,
                DestinationLongitude FLOAT NULL,
                TripDate DATE NOT NULL,
                PlannedStart DATETIME2 NOT NULL,
                PlannedEnd DATETIME2 NULL,
                EstimatedDurationMinutes INT NULL,
                DriverId INT NULL,
                AssistantDriverId INT NULL,
                VehicleId INT NULL,
                PassengerCount INT NOT NULL DEFAULT 1,
                Priority INT NOT NULL DEFAULT 2,
                Status INT NOT NULL DEFAULT 1,
                DriverNotes NVARCHAR(1000) NULL,
                PlannedDistanceKm DECIMAL(18,2) NULL,
                ActualDistanceKm DECIMAL(18,2) NULL,
                ActualStart DATETIME2 NULL,
                ActualEnd DATETIME2 NULL,
                CancellationReason NVARCHAR(500) NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CreatedBy NVARCHAR(100) NULL,
                UpdatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_Trips_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
                CONSTRAINT FK_Trips_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(Id),
                CONSTRAINT FK_Trips_Routes FOREIGN KEY (RouteId) REFERENCES Routes(Id),
                CONSTRAINT FK_Trips_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id),
                CONSTRAINT FK_Trips_AssistantDrivers FOREIGN KEY (AssistantDriverId) REFERENCES Drivers(Id),
                CONSTRAINT FK_Trips_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id)
            );

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Trips_Tenant_Status' AND object_id = OBJECT_ID('Trips'))
                CREATE INDEX IX_Trips_Tenant_Status ON Trips (TenantId, Status) WHERE IsDeleted = 0;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Trips_Tenant_TripDate' AND object_id = OBJECT_ID('Trips'))
                CREATE INDEX IX_Trips_Tenant_TripDate ON Trips (TenantId, TripDate) WHERE IsDeleted = 0;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Trips_TripNumber' AND object_id = OBJECT_ID('Trips'))
                CREATE UNIQUE INDEX IX_Trips_TripNumber ON Trips (TenantId, TripNumber) WHERE IsDeleted = 0 AND TripNumber <> '';
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TripStops')
            CREATE TABLE TripStops (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TripId INT NOT NULL,
                Sequence INT NOT NULL,
                Location NVARCHAR(500) NOT NULL,
                Latitude FLOAT NULL,
                Longitude FLOAT NULL,
                Eta DATETIME2 NULL,
                ArrivalTime DATETIME2 NULL,
                DepartureTime DATETIME2 NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_TripStops_Trips FOREIGN KEY (TripId) REFERENCES Trips(Id)
            );

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TripStops_TripId' AND object_id = OBJECT_ID('TripStops'))
                CREATE INDEX IX_TripStops_TripId ON TripStops (TripId, Sequence) WHERE IsDeleted = 0;
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TripStatusHistory')
            CREATE TABLE TripStatusHistory (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TripId INT NOT NULL,
                FromStatus INT NULL,
                ToStatus INT NOT NULL,
                ChangedAtUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                ChangedBy NVARCHAR(100) NULL,
                Note NVARCHAR(500) NULL,
                CONSTRAINT FK_TripStatusHistory_Trips FOREIGN KEY (TripId) REFERENCES Trips(Id)
            );

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TripStatusHistory_TripId' AND object_id = OBJECT_ID('TripStatusHistory'))
                CREATE INDEX IX_TripStatusHistory_TripId ON TripStatusHistory (TripId, ChangedAtUtc);
            """, cancellationToken: cancellationToken));

        logger.LogInformation("TripsModuleMigration applied successfully.");
    }
}
