using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class PortalSchemaMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // Create new portal tables first so a later ALTER failure does not block them.
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PromoCodes')
            CREATE TABLE PromoCodes (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Code NVARCHAR(50) NOT NULL,
                DiscountPercent DECIMAL(5,2) NULL,
                DiscountFixed DECIMAL(18,2) NULL,
                ValidFrom DATETIME2 NULL,
                ValidTo DATETIME2 NULL,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT UQ_PromoCodes_Code UNIQUE (Code)
            );
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CustomerSavedAddresses')
            CREATE TABLE CustomerSavedAddresses (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                CustomerId INT NOT NULL,
                Label NVARCHAR(100) NOT NULL,
                AddressLine NVARCHAR(500) NOT NULL,
                Latitude FLOAT NOT NULL,
                Longitude FLOAT NOT NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_CustomerSavedAddresses_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id)
            );
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CustomerNotifications')
            CREATE TABLE CustomerNotifications (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                CustomerId INT NOT NULL,
                Title NVARCHAR(200) NOT NULL,
                Message NVARCHAR(1000) NOT NULL,
                NotificationType NVARCHAR(50) NOT NULL,
                BookingId INT NULL,
                IsRead BIT NOT NULL DEFAULT 0,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_CustomerNotifications_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id)
            );
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CustomerLoyalty')
            CREATE TABLE CustomerLoyalty (
                CustomerId INT PRIMARY KEY,
                Points INT NOT NULL DEFAULT 0,
                Tier NVARCHAR(50) NOT NULL DEFAULT 'Bronze',
                UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT FK_CustomerLoyalty_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id)
            );
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CustomerWallets')
            CREATE TABLE CustomerWallets (
                CustomerId INT PRIMARY KEY,
                Balance DECIMAL(18,2) NOT NULL DEFAULT 0,
                UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT FK_CustomerWallets_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id)
            );
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'VehicleSeatLayouts')
            CREATE TABLE VehicleSeatLayouts (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                VehicleId INT NOT NULL,
                SeatLabel NVARCHAR(10) NOT NULL,
                RowIndex INT NOT NULL,
                ColIndex INT NOT NULL,
                IsActive BIT NOT NULL DEFAULT 1,
                CONSTRAINT FK_VehicleSeatLayouts_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id),
                CONSTRAINT UQ_VehicleSeatLayouts_Label UNIQUE (VehicleId, SeatLabel)
            );
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'BookingSeats')
            CREATE TABLE BookingSeats (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                BookingId INT NOT NULL,
                SeatLabel NVARCHAR(10) NOT NULL,
                CONSTRAINT FK_BookingSeats_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(Id),
                CONSTRAINT UQ_BookingSeats UNIQUE (BookingId, SeatLabel)
            );
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM PromoCodes WHERE Code = 'SHEIKH10' AND IsDeleted = 0)
            INSERT INTO PromoCodes (Code, DiscountPercent, IsActive) VALUES ('SHEIKH10', 10, 1);
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'PreferredPaymentMethod')
                ALTER TABLE Bookings ADD PreferredPaymentMethod NVARCHAR(50) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'PickupAddress')
                ALTER TABLE Bookings ADD PickupAddress NVARCHAR(500) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'DropoffAddress')
                ALTER TABLE Bookings ADD DropoffAddress NVARCHAR(500) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'PickupLat')
                ALTER TABLE Bookings ADD PickupLat FLOAT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'PickupLng')
                ALTER TABLE Bookings ADD PickupLng FLOAT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'DropLat')
                ALTER TABLE Bookings ADD DropLat FLOAT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'DropLng')
                ALTER TABLE Bookings ADD DropLng FLOAT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'QuotedDistanceKm')
                ALTER TABLE Bookings ADD QuotedDistanceKm DECIMAL(10,2) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'QuotedDurationMinutes')
                ALTER TABLE Bookings ADD QuotedDurationMinutes INT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'AdultCount')
                ALTER TABLE Bookings ADD AdultCount INT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'ChildCount')
                ALTER TABLE Bookings ADD ChildCount INT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'LuggageCount')
                ALTER TABLE Bookings ADD LuggageCount INT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'PromoCodeId')
                ALTER TABLE Bookings ADD PromoCodeId INT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'DiscountAmount')
                ALTER TABLE Bookings ADD DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0;
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Drivers' AND COLUMN_NAME = 'Rating')
                ALTER TABLE Drivers ADD Rating DECIMAL(3,2) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Drivers' AND COLUMN_NAME = 'YearsExperience')
                ALTER TABLE Drivers ADD YearsExperience INT NULL;
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Routes' AND COLUMN_NAME = 'EstimatedDurationMinutes')
                ALTER TABLE Routes ADD EstimatedDurationMinutes INT NULL;
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH('Routes', 'EstimatedDurationMinutes') IS NOT NULL
            UPDATE Routes SET EstimatedDurationMinutes = COALESCE(EstimatedMinutes,
                CASE WHEN Distance > 0 THEN CAST(CEILING(Distance / 70.0 * 60) AS INT) ELSE 120 END)
            WHERE EstimatedDurationMinutes IS NULL;
            """, cancellationToken: cancellationToken));

        logger.LogInformation("Portal schema migration applied.");
    }
}
