-- =============================================
-- Sheikh Travel System - Complete Database Schema
-- SQL Server / Azure SQL Database
-- Database: SheikhTravelSystemDB
-- =============================================
-- This script creates all tables, indexes, and seed data
-- Column names match C# entity properties exactly for Dapper mapping
-- =============================================

-- For Azure SQL Database: Connect to the target database before running
-- For SQL Server: Uncomment the CREATE DATABASE and USE statements below

-- CREATE DATABASE SheikhTravelSystemDB;
-- GO
-- USE SheikhTravelSystemDB;
-- GO

-- =============================================
-- DROP EXISTING TABLES (Optional - for clean rebuild)
-- =============================================
-- Uncomment to drop all tables before recreating

-- DROP TABLE IF EXISTS VehicleTracking;
-- DROP TABLE IF EXISTS Notifications;
-- DROP TABLE IF EXISTS AuditLogs;
-- DROP TABLE IF EXISTS DriverAllowanceRules;
-- DROP TABLE IF EXISTS Maintenance;
-- DROP TABLE IF EXISTS FuelLogs;
-- DROP TABLE IF EXISTS Payments;
-- DROP TABLE IF EXISTS Bookings;
-- DROP TABLE IF EXISTS Routes;
-- DROP TABLE IF EXISTS Drivers;
-- DROP TABLE IF EXISTS Vehicles;
-- DROP TABLE IF EXISTS Customers;
-- DROP TABLE IF EXISTS Users;

-- =============================================
-- TABLES
-- =============================================

-- =============================================
-- Users Table
-- =============================================
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(200) NOT NULL,
    Email NVARCHAR(200) NOT NULL,
    PasswordHash NVARCHAR(500) NOT NULL,
    Phone NVARCHAR(20),
    Role INT NOT NULL DEFAULT 1,              -- UserRole: Admin=1, Dispatcher=2, Driver=3, Accountant=4
    IsActive BIT NOT NULL DEFAULT 1,
    RefreshToken NVARCHAR(500),
    RefreshTokenExpiryTime DATETIME2,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);

-- =============================================
-- Customers Table
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
-- Vehicles Table
-- =============================================
CREATE TABLE Vehicles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    RegistrationNumber NVARCHAR(50) NOT NULL,
    Model NVARCHAR(100),
    [Year] INT,
    SeatingCapacity INT NOT NULL DEFAULT 0,
    FuelAverage DECIMAL(10,2) NOT NULL DEFAULT 0,
    FuelType INT NOT NULL DEFAULT 0,          -- FuelType: Petrol=1, Diesel=2, CNG=3
    CurrentMileage DECIMAL(12,2) NOT NULL DEFAULT 0,
    InsuranceExpiryDate DATETIME2,
    Status INT NOT NULL DEFAULT 1,            -- VehicleStatus: Available=1, OnTrip=2, Maintenance=3, Retired=4
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_Vehicles_Registration UNIQUE (RegistrationNumber)
);

-- =============================================
-- Drivers Table
-- =============================================
CREATE TABLE Drivers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(200) NOT NULL,
    Phone NVARCHAR(20) NOT NULL,
    LicenseNumber NVARCHAR(50),
    LicenseExpiryDate DATETIME2,
    CNIC NVARCHAR(20),
    [Address] NVARCHAR(500),
    Status INT NOT NULL DEFAULT 1,            -- DriverStatus: Available=1, OnTrip=2, OffDuty=3, Suspended=4
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0
);

-- =============================================
-- Routes Table
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
-- Bookings Table
-- =============================================
CREATE TABLE Bookings (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BookingNumber NVARCHAR(20) NOT NULL DEFAULT '',
    CustomerId INT NOT NULL,
    RouteId INT,
    VehicleId INT,
    DriverId INT,
    PickupTime DATETIME2 NOT NULL,
    DropoffTime DATETIME2,
    PassengerCount INT NOT NULL DEFAULT 1,
    TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    Status INT NOT NULL DEFAULT 1,            -- BookingStatus: Pending=1, Confirmed=2, Started=3, Completed=4, Cancelled=5
    Notes NVARCHAR(1000),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Bookings_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id),
    CONSTRAINT FK_Bookings_Routes FOREIGN KEY (RouteId) REFERENCES Routes(Id),
    CONSTRAINT FK_Bookings_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id),
    CONSTRAINT FK_Bookings_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id)
);

-- =============================================
-- Payments Table
-- =============================================
CREATE TABLE Payments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BookingId INT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    PaymentMethod NVARCHAR(50),
    Status INT NOT NULL DEFAULT 1,            -- PaymentStatus: Pending=1, PartiallyPaid=2, Paid=3, Refunded=4
    PaymentDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    TransactionReference NVARCHAR(200),
    Notes NVARCHAR(1000),
    ReceiptImageData NVARCHAR(MAX) NULL,      -- Base64 image data or data URL for receipt/proof
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Payments_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(Id)
);

-- =============================================
-- FuelLogs Table
-- =============================================
CREATE TABLE FuelLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VehicleId INT NOT NULL,
    DriverId INT,
    Liters DECIMAL(10,2) NOT NULL,
    PricePerLiter DECIMAL(10,2) NOT NULL,
    TotalCost DECIMAL(18,2) NOT NULL,
    OdometerReading DECIMAL(12,2),
    FuelType INT NOT NULL DEFAULT 0,          -- FuelType: Petrol=1, Diesel=2, CNG=3
    FuelDate DATETIME2 NOT NULL,
    Station NVARCHAR(200),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_FuelLogs_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id),
    CONSTRAINT FK_FuelLogs_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id)
);

-- =============================================
-- Maintenance Table
-- =============================================
CREATE TABLE Maintenance (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VehicleId INT NOT NULL,
    Description NVARCHAR(500) NOT NULL,
    Cost DECIMAL(18,2) NOT NULL,
    MaintenanceDate DATETIME2 NOT NULL,
    NextDueDate DATETIME2,
    Status INT NOT NULL DEFAULT 1,            -- MaintenanceStatus: Scheduled=1, InProgress=2, Completed=3
    ServiceProvider NVARCHAR(200),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Maintenance_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id)
);

-- =============================================
-- VehicleTracking Table
-- =============================================
CREATE TABLE VehicleTracking (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VehicleId INT NOT NULL,
    DriverId INT,
    BookingId INT,
    Latitude FLOAT NOT NULL,
    Longitude FLOAT NOT NULL,
    Speed DECIMAL(10,2) NOT NULL DEFAULT 0,
    [Timestamp] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_VehicleTracking_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id),
    CONSTRAINT FK_VehicleTracking_Drivers FOREIGN KEY (DriverId) REFERENCES Drivers(Id),
    CONSTRAINT FK_VehicleTracking_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(Id)
);

-- =============================================
-- Notifications Table
-- =============================================
CREATE TABLE Notifications (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NULL,                          -- NULL for global notifications
    Title NVARCHAR(200) NOT NULL,
    [Message] NVARCHAR(1000) NOT NULL,
    Type INT NOT NULL DEFAULT 0,              -- NotificationType: BookingCreated=1, TripDelayed=2, VehicleOffline=3, PaymentReceived=4
    IsRead BIT NOT NULL DEFAULT 0,
    ReferenceId INT,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
);

-- =============================================
-- AuditLogs Table
-- =============================================
CREATE TABLE AuditLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    [Action] NVARCHAR(200) NOT NULL,
    EntityName NVARCHAR(100) NOT NULL,
    EntityId INT,
    OldValues NVARCHAR(MAX),
    NewValues NVARCHAR(MAX),
    UserId INT,
    IpAddress NVARCHAR(50),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0
);

-- =============================================
-- DriverAllowanceRules Table
-- =============================================
CREATE TABLE DriverAllowanceRules (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    CalculationType INT NOT NULL,            -- AllowanceCalculationType: FixedAmount=1, PerKm=2, PerDay=3, ProfitPercent=4
    [Value] DECIMAL(18,2) NOT NULL,          -- PKR, PKR/km, PKR/day, or percentage (0-100)
    Priority INT NOT NULL DEFAULT 0,         -- Lower number = higher priority
    MinDistanceKm DECIMAL(10,2) NULL,        -- Optional filter: minimum route distance
    MaxDistanceKm DECIMAL(10,2) NULL,        -- Optional filter: maximum route distance
    VehicleFuelType INT NULL,                -- Optional filter: matches Vehicles.FuelType
    RouteFilter NVARCHAR(200) NULL,          -- Optional filter: route Source/Destination/Name contains
    IsActive BIT NOT NULL DEFAULT 1,
    Notes NVARCHAR(1000),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CreatedBy NVARCHAR(100),
    UpdatedBy NVARCHAR(100),
    IsDeleted BIT NOT NULL DEFAULT 0
);

-- =============================================
-- INDEXES FOR PERFORMANCE
-- =============================================

-- Users Indexes
CREATE INDEX IX_Users_Email ON Users(Email) WHERE IsDeleted = 0;
CREATE INDEX IX_Users_Role ON Users(Role) WHERE IsDeleted = 0;

-- Customers Indexes
CREATE INDEX IX_Customers_Phone ON Customers(Phone) WHERE IsDeleted = 0;
CREATE INDEX IX_Customers_Email ON Customers(Email) WHERE IsDeleted = 0;

-- Vehicles Indexes
CREATE INDEX IX_Vehicles_Status ON Vehicles(Status) WHERE IsDeleted = 0;
CREATE INDEX IX_Vehicles_RegistrationNumber ON Vehicles(RegistrationNumber) WHERE IsDeleted = 0;
CREATE INDEX IX_Vehicles_FuelType ON Vehicles(FuelType) WHERE IsDeleted = 0;

-- Drivers Indexes
CREATE INDEX IX_Drivers_Status ON Drivers(Status) WHERE IsDeleted = 0;
CREATE INDEX IX_Drivers_Phone ON Drivers(Phone) WHERE IsDeleted = 0;

-- Routes Indexes
CREATE INDEX IX_Routes_Source ON Routes(Source) WHERE IsDeleted = 0;
CREATE INDEX IX_Routes_Destination ON Routes(Destination) WHERE IsDeleted = 0;
CREATE INDEX IX_Routes_IsActive ON Routes(IsActive) WHERE IsDeleted = 0;

-- Bookings Indexes
CREATE INDEX IX_Bookings_CustomerId ON Bookings(CustomerId) WHERE IsDeleted = 0;
CREATE INDEX IX_Bookings_VehicleId ON Bookings(VehicleId) WHERE IsDeleted = 0;
CREATE INDEX IX_Bookings_DriverId ON Bookings(DriverId) WHERE IsDeleted = 0;
CREATE INDEX IX_Bookings_RouteId ON Bookings(RouteId) WHERE IsDeleted = 0;
CREATE INDEX IX_Bookings_Status ON Bookings(Status) WHERE IsDeleted = 0;
CREATE INDEX IX_Bookings_PickupTime ON Bookings(PickupTime) WHERE IsDeleted = 0;
CREATE INDEX IX_Bookings_BookingNumber ON Bookings(BookingNumber) WHERE IsDeleted = 0;

-- Payments Indexes
CREATE INDEX IX_Payments_BookingId ON Payments(BookingId) WHERE IsDeleted = 0;
CREATE INDEX IX_Payments_Status ON Payments(Status) WHERE IsDeleted = 0;
CREATE INDEX IX_Payments_PaymentDate ON Payments(PaymentDate) WHERE IsDeleted = 0;

-- FuelLogs Indexes
CREATE INDEX IX_FuelLogs_VehicleId ON FuelLogs(VehicleId) WHERE IsDeleted = 0;
CREATE INDEX IX_FuelLogs_DriverId ON FuelLogs(DriverId) WHERE IsDeleted = 0;
CREATE INDEX IX_FuelLogs_FuelDate ON FuelLogs(FuelDate) WHERE IsDeleted = 0;

-- Maintenance Indexes
CREATE INDEX IX_Maintenance_VehicleId ON Maintenance(VehicleId) WHERE IsDeleted = 0;
CREATE INDEX IX_Maintenance_Status ON Maintenance(Status) WHERE IsDeleted = 0;
CREATE INDEX IX_Maintenance_MaintenanceDate ON Maintenance(MaintenanceDate) WHERE IsDeleted = 0;

-- VehicleTracking Indexes
CREATE INDEX IX_VehicleTracking_VehicleId ON VehicleTracking(VehicleId, [Timestamp]) WHERE IsDeleted = 0;
CREATE INDEX IX_VehicleTracking_DriverId ON VehicleTracking(DriverId) WHERE IsDeleted = 0;
CREATE INDEX IX_VehicleTracking_BookingId ON VehicleTracking(BookingId) WHERE IsDeleted = 0;

-- Notifications Indexes
CREATE INDEX IX_Notifications_UserId ON Notifications(UserId, IsRead) WHERE IsDeleted = 0;
CREATE INDEX IX_Notifications_Type ON Notifications(Type) WHERE IsDeleted = 0;
CREATE INDEX IX_Notifications_CreatedAt ON Notifications(CreatedAt) WHERE IsDeleted = 0;

-- AuditLogs Indexes
CREATE INDEX IX_AuditLogs_EntityName ON AuditLogs(EntityName, EntityId);
CREATE INDEX IX_AuditLogs_UserId ON AuditLogs(UserId);
CREATE INDEX IX_AuditLogs_CreatedAt ON AuditLogs(CreatedAt);

-- DriverAllowanceRules Indexes
CREATE INDEX IX_DriverAllowanceRules_Priority ON DriverAllowanceRules(Priority, IsActive) WHERE IsDeleted = 0;
CREATE INDEX IX_DriverAllowanceRules_IsActive ON DriverAllowanceRules(IsActive) WHERE IsDeleted = 0;

-- =============================================
-- SEED DATA
-- =============================================

-- Default Admin User
-- Email: admin@sheikhtravel.com
-- Password: Admin@123
-- PasswordHash: BCrypt hash with work factor 12
INSERT INTO Users (FullName, Email, PasswordHash, Phone, Role, IsActive, CreatedAt)
VALUES (
    'System Admin',
    'admin@sheikhtravel.com',
    '$2a$12$rEQCd6VbrekNPFaxT5f8pO5lZ78BlfKdcFrkhYN1sltX2Tk4iHCX2',
    '03001234567',
    1,  -- Admin
    1,
    GETUTCDATE()
);

-- Default Driver Allowance Rules
INSERT INTO DriverAllowanceRules (Name, CalculationType, [Value], Priority, MaxDistanceKm, IsActive, Notes, CreatedAt)
VALUES 
    ('Short Trip Fixed Allowance', 1, 500.00, 1, 50.00, 1, 'Fixed PKR 500 for trips under 50 km', GETUTCDATE()),
    ('Medium Trip Per-KM', 2, 15.00, 2, 200.00, 1, 'PKR 15 per km for trips 50-200 km', GETUTCDATE()),
    ('Long Trip Per-Day', 3, 2000.00, 3, NULL, 1, 'PKR 2000 per day for trips over 200 km', GETUTCDATE()),
    ('Default Profit Share', 4, 10.00, 999, NULL, 1, '10% profit share as fallback', GETUTCDATE());

-- Update Priority for Medium Trip to include MinDistanceKm
UPDATE DriverAllowanceRules 
SET MinDistanceKm = 50.00 
WHERE Name = 'Medium Trip Per-KM';

UPDATE DriverAllowanceRules 
SET MinDistanceKm = 200.00 
WHERE Name = 'Long Trip Per-Day';

-- =============================================
-- SUMMARY
-- =============================================
-- Database: SheikhTravelSystemDB
-- 
-- Tables Created (13):
--   1. Users              - System users (Admin, Dispatcher, Driver, Accountant)
--   2. Customers          - Customer records
--   3. Vehicles           - Fleet management
--   4. Drivers            - Driver records
--   5. Routes             - Route definitions
--   6. Bookings           - Trip bookings
--   7. Payments           - Payment tracking
--   8. FuelLogs           - Fuel consumption
--   9. Maintenance        - Vehicle maintenance
--  10. VehicleTracking    - GPS tracking data
--  11. Notifications      - User notifications
--  12. AuditLogs          - Audit trail
--  13. DriverAllowanceRules - Driver compensation rules
--
-- Features:
--   ✓ Soft delete support (IsDeleted)
--   ✓ Audit fields (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
--   ✓ Foreign key relationships
--   ✓ Performance indexes
--   ✓ Default admin user seeded
--   ✓ Driver allowance rules seeded
--
-- Default Admin Credentials:
--   Email: admin@sheikhtravel.com
--   Password: Admin@123
-- =============================================
