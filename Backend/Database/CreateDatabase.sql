-- Sheikh Travel System - Database Creation Script
-- SQL Server
-- Column names match C# entity properties exactly for Dapper mapping

CREATE DATABASE SheikhTravelSystem;
GO
USE SheikhTravelSystem;
GO

-- =============================================
-- Users
-- =============================================
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(200) NOT NULL,
    Email NVARCHAR(200) NOT NULL,
    PasswordHash NVARCHAR(500) NOT NULL,
    Phone NVARCHAR(20),
    Role INT NOT NULL DEFAULT 1,          -- UserRole enum: Admin=1, Dispatcher=2, Driver=3, Accountant=4
    IsActive BIT NOT NULL DEFAULT 1,
    RefreshToken NVARCHAR(500),
    RefreshTokenExpiryTime DATETIME2,     -- matches C# entity property name
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);

-- =============================================
-- Customers
-- =============================================
CREATE TABLE Customers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(200) NOT NULL,
    Phone NVARCHAR(20) NOT NULL,
    Email NVARCHAR(200),
    Address NVARCHAR(500),
    CNIC NVARCHAR(20),
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0
);

-- =============================================
-- Vehicles
-- =============================================
CREATE TABLE Vehicles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    RegistrationNumber NVARCHAR(50) NOT NULL,
    Model NVARCHAR(100),
    [Year] INT,
    SeatingCapacity INT NOT NULL DEFAULT 0,
    FuelAverage DECIMAL(10,2) NOT NULL DEFAULT 0,
    FuelType INT NOT NULL DEFAULT 0,           -- FuelType enum: Petrol=1, Diesel=2, CNG=3
    CurrentMileage DECIMAL(12,2) NOT NULL DEFAULT 0,
    InsuranceExpiryDate DATETIME2,
    Status INT NOT NULL DEFAULT 1,             -- VehicleStatus: Available=1, OnTrip=2, Maintenance=3, Retired=4
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_Vehicles_Registration UNIQUE (RegistrationNumber)
);

-- =============================================
-- Drivers
-- =============================================
CREATE TABLE Drivers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(200) NOT NULL,
    Phone NVARCHAR(20) NOT NULL,
    LicenseNumber NVARCHAR(50),
    LicenseExpiryDate DATETIME2,
    CNIC NVARCHAR(20),
    [Address] NVARCHAR(500),
    Status INT NOT NULL DEFAULT 1,             -- DriverStatus: Available=1, OnTrip=2, OffDuty=3, Suspended=4
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0
);

-- =============================================
-- Routes
-- =============================================
CREATE TABLE Routes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NULL,
    Source NVARCHAR(200) NOT NULL,
    Destination NVARCHAR(200) NOT NULL,
    Distance DECIMAL(10,2) NOT NULL DEFAULT 0,
    EstimatedMinutes INT NULL,
    BasePrice DECIMAL(18,2) NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0
);

-- =============================================
-- Bookings
-- =============================================
CREATE TABLE Bookings (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CustomerId INT NOT NULL REFERENCES Customers(Id),
    RouteId INT REFERENCES Routes(Id),
    VehicleId INT REFERENCES Vehicles(Id),
    DriverId INT REFERENCES Drivers(Id),
    PickupTime DATETIME2 NOT NULL,
    DropoffTime DATETIME2,
    PassengerCount INT NOT NULL DEFAULT 1,
    TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    Status INT NOT NULL DEFAULT 1,             -- BookingStatus: Pending=1, Confirmed=2, Started=3, Completed=4, Cancelled=5
    Notes NVARCHAR(1000),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0
);

-- =============================================
-- Payments
-- =============================================
CREATE TABLE Payments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BookingId INT NOT NULL REFERENCES Bookings(Id),
    Amount DECIMAL(18,2) NOT NULL,
    PaymentMethod NVARCHAR(50),
    Status INT NOT NULL DEFAULT 1,             -- PaymentStatus: Pending=1, PartiallyPaid=2, Paid=3, Refunded=4
    PaymentDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    TransactionReference NVARCHAR(200),
    Notes NVARCHAR(1000),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0
);

-- =============================================
-- FuelLogs
-- =============================================
CREATE TABLE FuelLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VehicleId INT NOT NULL REFERENCES Vehicles(Id),
    DriverId INT REFERENCES Drivers(Id),
    Liters DECIMAL(10,2) NOT NULL,
    PricePerLiter DECIMAL(10,2) NOT NULL,
    TotalCost DECIMAL(18,2) NOT NULL,
    OdometerReading DECIMAL(12,2),
    FuelType INT NOT NULL DEFAULT 0,
    FuelDate DATETIME2 NOT NULL,
    Station NVARCHAR(200),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0
);

-- =============================================
-- Maintenance
-- =============================================
CREATE TABLE Maintenance (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VehicleId INT NOT NULL REFERENCES Vehicles(Id),
    Description NVARCHAR(500) NOT NULL,
    Cost DECIMAL(18,2) NOT NULL,
    MaintenanceDate DATETIME2 NOT NULL,
    NextDueDate DATETIME2,
    Status INT NOT NULL DEFAULT 1,             -- MaintenanceStatus: Scheduled=1, InProgress=2, Completed=3
    ServiceProvider NVARCHAR(200),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0
);

-- =============================================
-- VehicleTracking
-- =============================================
CREATE TABLE VehicleTracking (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VehicleId INT NOT NULL REFERENCES Vehicles(Id),
    DriverId INT REFERENCES Drivers(Id),
    BookingId INT REFERENCES Bookings(Id),
    Latitude FLOAT NOT NULL,
    Longitude FLOAT NOT NULL,
    Speed DECIMAL(10,2) NOT NULL DEFAULT 0,
    [Timestamp] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0
);

-- =============================================
-- Notifications
-- =============================================
CREATE TABLE Notifications (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NULL REFERENCES Users(Id),      -- NULL for global notifications
    Title NVARCHAR(200) NOT NULL,
    [Message] NVARCHAR(1000) NOT NULL,
    Type INT NOT NULL DEFAULT 0,
    IsRead BIT NOT NULL DEFAULT 0,
    ReferenceId INT,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0
);

-- =============================================
-- AuditLogs
-- =============================================
CREATE TABLE AuditLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    [Action] NVARCHAR(200) NOT NULL,
    EntityName NVARCHAR(100) NOT NULL,
    EntityId INT,
    OldValues NVARCHAR(MAX),
    NewValues NVARCHAR(MAX),
    UserId NVARCHAR(100),
    IpAddress NVARCHAR(50),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0
);

-- =============================================
-- Seed Admin User (password: Admin@123)
-- Login uses Email (no separate Username column). FullName is the display name.
-- PasswordHash: BCrypt work factor 12, matches BcryptPasswordHasher in the API
-- =============================================
INSERT INTO Users (FullName, Email, PasswordHash, Phone, Role, IsActive)
VALUES (
    'System Admin',
    'admin@sheikhtravel.com',
    '$2a$12$rEQCd6VbrekNPFaxT5f8pO5lZ78BlfKdcFrkhYN1sltX2Tk4iHCX2',
    '03001234567',
    1,
    1
);

-- =============================================
-- Indexes
-- =============================================
CREATE INDEX IX_Bookings_CustomerId ON Bookings(CustomerId) WHERE IsDeleted = 0;
CREATE INDEX IX_Bookings_VehicleId ON Bookings(VehicleId) WHERE IsDeleted = 0;
CREATE INDEX IX_Bookings_DriverId ON Bookings(DriverId) WHERE IsDeleted = 0;
CREATE INDEX IX_Bookings_Status ON Bookings(Status) WHERE IsDeleted = 0;
CREATE INDEX IX_Bookings_PickupTime ON Bookings(PickupTime) WHERE IsDeleted = 0;
CREATE INDEX IX_Payments_BookingId ON Payments(BookingId) WHERE IsDeleted = 0;
CREATE INDEX IX_Payments_Status ON Payments(Status) WHERE IsDeleted = 0;
CREATE INDEX IX_FuelLogs_VehicleId ON FuelLogs(VehicleId) WHERE IsDeleted = 0;
CREATE INDEX IX_FuelLogs_FuelDate ON FuelLogs(FuelDate) WHERE IsDeleted = 0;
CREATE INDEX IX_Maintenance_VehicleId ON Maintenance(VehicleId) WHERE IsDeleted = 0;
CREATE INDEX IX_VehicleTracking_VehicleId ON VehicleTracking(VehicleId, [Timestamp]) WHERE IsDeleted = 0;
CREATE INDEX IX_Notifications_UserId ON Notifications(UserId, IsRead) WHERE IsDeleted = 0;
CREATE INDEX IX_AuditLogs_EntityName ON AuditLogs(EntityName, EntityId);
CREATE INDEX IX_Users_Email ON Users(Email) WHERE IsDeleted = 0;
