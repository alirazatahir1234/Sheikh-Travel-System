-- Phase 8 CI fixture DB — tenants 9001 / 9002 with known vehicle counts.
-- Idempotent. Never logs credentials; connection string comes from env only.
IF DB_ID(N'SheikhGo_Phase8_CI') IS NULL
    CREATE DATABASE SheikhGo_Phase8_CI;
GO

USE SheikhGo_Phase8_CI;
GO

IF OBJECT_ID(N'dbo.Tenants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tenants (
        Id INT NOT NULL PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Slug NVARCHAR(100) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Tenants_IsActive DEFAULT (1),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Tenants_CreatedAt DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'dbo.Vehicles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Vehicles (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        RegistrationNumber NVARCHAR(50) NOT NULL,
        SeatingCapacity INT NOT NULL CONSTRAINT DF_Vehicles_Seats DEFAULT (0),
        FuelAverage DECIMAL(10,2) NOT NULL CONSTRAINT DF_Vehicles_FuelAvg DEFAULT (0),
        FuelType INT NOT NULL CONSTRAINT DF_Vehicles_FuelType DEFAULT (1),
        CurrentMileage DECIMAL(12,2) NOT NULL CONSTRAINT DF_Vehicles_Mileage DEFAULT (0),
        CurrentEngineHours DECIMAL(12,2) NOT NULL CONSTRAINT DF_Vehicles_EngineHours DEFAULT (0),
        Status INT NOT NULL CONSTRAINT DF_Vehicles_Status DEFAULT (1),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Vehicles_CreatedAt DEFAULT (SYSUTCDATETIME()),
        IsDeleted BIT NOT NULL CONSTRAINT DF_Vehicles_IsDeleted DEFAULT (0),
        TenantId INT NOT NULL,
        CONSTRAINT UQ_Vehicles_Reg UNIQUE (RegistrationNumber)
    );
    CREATE INDEX IX_Vehicles_TenantId ON dbo.Vehicles (TenantId) WHERE IsDeleted = 0;
END
GO

MERGE dbo.Tenants AS t
USING (VALUES
    (9001, N'Phase8 Fixture A', N'phase8-a', 1),
    (9002, N'Phase8 Fixture B', N'phase8-b', 1)
) AS s (Id, Name, Slug, IsActive)
ON t.Id = s.Id
WHEN MATCHED THEN UPDATE SET Name = s.Name, Slug = s.Slug, IsActive = s.IsActive
WHEN NOT MATCHED THEN INSERT (Id, Name, Slug, IsActive) VALUES (s.Id, s.Name, s.Slug, s.IsActive);
GO

-- Reset fixture plates only (never touch non-fixture rows).
DELETE FROM dbo.Vehicles WHERE RegistrationNumber LIKE N'P8-%';
GO

-- Tenant 9001 → 3 active inventory vehicles (Status 1/2/3; exclude Draft/Retired)
INSERT INTO dbo.Vehicles (Name, RegistrationNumber, Status, TenantId)
VALUES
    (N'P8 A1', N'P8-9001-01', 1, 9001),
    (N'P8 A2', N'P8-9001-02', 2, 9001),
    (N'P8 A3', N'P8-9001-03', 3, 9001);

-- Tenant 9002 → 5 active inventory vehicles
INSERT INTO dbo.Vehicles (Name, RegistrationNumber, Status, TenantId)
VALUES
    (N'P8 B1', N'P8-9002-01', 1, 9002),
    (N'P8 B2', N'P8-9002-02', 1, 9002),
    (N'P8 B3', N'P8-9002-03', 2, 9002),
    (N'P8 B4', N'P8-9002-04', 2, 9002),
    (N'P8 B5', N'P8-9002-05', 3, 9002);
GO
