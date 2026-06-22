using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Fleet maintenance module schema: requests, work orders, workshops, parts, schedules, alerts.
/// Idempotent — safe to re-run.
/// </summary>
public static class MaintenanceModuleMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await ExtendMaintenanceAsync(connection, cancellationToken);
        await CreateServiceTypesAsync(connection, cancellationToken);
        await CreateWorkshopsAsync(connection, cancellationToken);
        await CreateTechniciansAsync(connection, cancellationToken);
        await CreateMaintenanceRequestsAsync(connection, cancellationToken);
        await CreateWorkOrdersAsync(connection, cancellationToken);
        await CreateWorkOrderItemsAsync(connection, cancellationToken);
        await CreateMaintenanceAlertsAsync(connection, cancellationToken);
        await CreateVehicleMaintenanceSchedulesAsync(connection, cancellationToken);
        await CreatePartsAsync(connection, cancellationToken);
        await CreatePartInventoryAsync(connection, cancellationToken);
        await CreatePartUsageAsync(connection, cancellationToken);
        await CreateVehicleBreakdownsAsync(connection, cancellationToken);
        await CreateMaintenanceCostsAsync(connection, cancellationToken);
        await SeedServiceTypesAsync(connection, cancellationToken);
        await SeedWorkOrderStatusesAsync(connection, cancellationToken);
        await ExtendMaintenanceRequestsAsync(connection, cancellationToken);
        await ExtendSchedulerSchemaAsync(connection, cancellationToken);
        await ExtendWorkshopVendorSchemaAsync(connection, cancellationToken);
        await ExtendPartsInventorySchemaAsync(connection, cancellationToken);
        await ExtendMaintenanceReportsSchemaAsync(connection, cancellationToken);
        await ExtendWorkOrdersSchemaAsync(connection, cancellationToken);
        await SeedMaintenanceRequestStatusesAsync(connection, cancellationToken);
        await EnsureIndexesAsync(connection, cancellationToken);

        logger.LogInformation("Maintenance module migration completed.");
    }

    private static async Task ExtendMaintenanceAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'TenantId')
                ALTER TABLE Maintenance ADD TenantId INT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'WorkOrderId')
                ALTER TABLE Maintenance ADD WorkOrderId INT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'RequestId')
                ALTER TABLE Maintenance ADD RequestId INT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'Category')
                ALTER TABLE Maintenance ADD Category NVARCHAR(60) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'Priority')
                ALTER TABLE Maintenance ADD Priority NVARCHAR(20) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'LaborCost')
                ALTER TABLE Maintenance ADD LaborCost DECIMAL(18,2) NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'PartsCost')
                ALTER TABLE Maintenance ADD PartsCost DECIMAL(18,2) NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'IsPreventive')
                ALTER TABLE Maintenance ADD IsPreventive BIT NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'TechnicianNotes')
                ALTER TABLE Maintenance ADD TechnicianNotes NVARCHAR(MAX) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'CompletedAt')
                ALTER TABLE Maintenance ADD CompletedAt DATETIME2 NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Maintenance' AND COLUMN_NAME = 'ServiceTypeId')
                ALTER TABLE Maintenance ADD ServiceTypeId INT NULL;
            """, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE m SET m.TenantId = v.TenantId
            FROM Maintenance m
            INNER JOIN Vehicles v ON m.VehicleId = v.Id
            WHERE m.TenantId IS NULL;
            """, cancellationToken: ct));
    }

    private static async Task CreateServiceTypesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ServiceTypes')
            CREATE TABLE ServiceTypes (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                Code NVARCHAR(60) NOT NULL,
                Name NVARCHAR(150) NOT NULL,
                IsPreventive BIT NOT NULL DEFAULT 1,
                SortOrder INT NOT NULL DEFAULT 0,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateWorkshopsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Workshops')
            CREATE TABLE Workshops (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                Name NVARCHAR(200) NOT NULL,
                WorkshopType NVARCHAR(20) NOT NULL DEFAULT N'Internal',
                Location NVARCHAR(300) NULL,
                ContactPerson NVARCHAR(150) NULL,
                ContactPhone NVARCHAR(40) NULL,
                ContactEmail NVARCHAR(150) NULL,
                Capacity INT NULL,
                VendorType NVARCHAR(60) NULL,
                ContractDetails NVARCHAR(500) NULL,
                SLA NVARCHAR(200) NULL,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CreatedBy NVARCHAR(100) NULL,
                UpdatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateTechniciansAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Technicians')
            CREATE TABLE Technicians (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                WorkshopId INT NULL,
                FullName NVARCHAR(150) NOT NULL,
                Phone NVARCHAR(40) NULL,
                Email NVARCHAR(150) NULL,
                UserId INT NULL,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_Technicians_Workshops FOREIGN KEY (WorkshopId) REFERENCES Workshops(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateMaintenanceRequestsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MaintenanceRequests')
            CREATE TABLE MaintenanceRequests (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                RequestNumber NVARCHAR(30) NOT NULL,
                VehicleId INT NOT NULL,
                DriverId INT NULL,
                RequestDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                RequestType NVARCHAR(40) NOT NULL DEFAULT N'Corrective',
                Priority NVARCHAR(20) NOT NULL DEFAULT N'Medium',
                IssueCategory NVARCHAR(60) NOT NULL,
                Description NVARCHAR(2000) NOT NULL,
                BreakdownLocation NVARCHAR(300) NULL,
                DriverRemarks NVARCHAR(1000) NULL,
                PhotosJson NVARCHAR(MAX) NULL,
                DocumentsJson NVARCHAR(MAX) NULL,
                Status NVARCHAR(30) NOT NULL DEFAULT N'Open',
                WorkOrderId INT NULL,
                BranchId INT NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CreatedBy NVARCHAR(100) NULL,
                UpdatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_MaintenanceRequests_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateWorkOrdersAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorkOrders')
            CREATE TABLE WorkOrders (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                WorkOrderNumber NVARCHAR(30) NOT NULL,
                RequestId INT NULL,
                VehicleId INT NOT NULL,
                WorkshopId INT NULL,
                TechnicianId INT NULL,
                ServiceTypeId INT NULL,
                ServiceTypeName NVARCHAR(150) NULL,
                StartDate DATETIME2 NULL,
                EstimatedCompletionDate DATETIME2 NULL,
                CompletedAt DATETIME2 NULL,
                LaborCost DECIMAL(18,2) NOT NULL DEFAULT 0,
                PartsCost DECIMAL(18,2) NOT NULL DEFAULT 0,
                TotalCost AS (LaborCost + PartsCost) PERSISTED,
                Status NVARCHAR(30) NOT NULL DEFAULT N'Draft',
                Priority NVARCHAR(20) NULL,
                Notes NVARCHAR(2000) NULL,
                TechnicianNotes NVARCHAR(MAX) NULL,
                BranchId INT NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CreatedBy NVARCHAR(100) NULL,
                UpdatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_WorkOrders_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id),
                CONSTRAINT FK_WorkOrders_Workshops FOREIGN KEY (WorkshopId) REFERENCES Workshops(Id),
                CONSTRAINT FK_WorkOrders_Requests FOREIGN KEY (RequestId) REFERENCES MaintenanceRequests(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateWorkOrderItemsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorkOrderItems')
            CREATE TABLE WorkOrderItems (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                WorkOrderId INT NOT NULL,
                ItemType NVARCHAR(20) NOT NULL DEFAULT N'Labor',
                Description NVARCHAR(500) NOT NULL,
                Quantity DECIMAL(10,2) NOT NULL DEFAULT 1,
                UnitCost DECIMAL(18,2) NOT NULL DEFAULT 0,
                TotalCost AS (Quantity * UnitCost) PERSISTED,
                PartId INT NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_WorkOrderItems_WorkOrders FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateMaintenanceAlertsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MaintenanceAlerts')
            CREATE TABLE MaintenanceAlerts (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                VehicleId INT NULL,
                AlertType NVARCHAR(60) NOT NULL,
                Severity NVARCHAR(20) NOT NULL DEFAULT N'Warning',
                Title NVARCHAR(200) NOT NULL,
                Message NVARCHAR(1000) NOT NULL,
                ReferenceType NVARCHAR(40) NULL,
                ReferenceId INT NULL,
                IsRead BIT NOT NULL DEFAULT 0,
                IsDismissed BIT NOT NULL DEFAULT 0,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT FK_MaintenanceAlerts_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateVehicleMaintenanceSchedulesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'VehicleMaintenanceSchedules')
            CREATE TABLE VehicleMaintenanceSchedules (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                VehicleId INT NOT NULL,
                ServiceTypeId INT NULL,
                ServiceTypeName NVARCHAR(150) NOT NULL,
                IntervalType NVARCHAR(20) NOT NULL,
                IntervalValue INT NOT NULL,
                LastServiceDate DATETIME2 NULL,
                LastServiceMileage DECIMAL(12,2) NULL,
                NextDueDate DATETIME2 NULL,
                NextDueMileage DECIMAL(12,2) NULL,
                Priority NVARCHAR(20) NOT NULL DEFAULT N'Medium',
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_VehicleMaintenanceSchedules_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreatePartsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Parts')
            CREATE TABLE Parts (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                PartNumber NVARCHAR(60) NOT NULL,
                PartName NVARCHAR(200) NOT NULL,
                Category NVARCHAR(60) NULL,
                Brand NVARCHAR(100) NULL,
                Supplier NVARCHAR(200) NULL,
                UnitCost DECIMAL(18,2) NOT NULL DEFAULT 0,
                MinStockLevel INT NOT NULL DEFAULT 0,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0
            );
            """, cancellationToken: ct));
    }

    private static async Task CreatePartInventoryAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PartInventory')
            CREATE TABLE PartInventory (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                PartId INT NOT NULL,
                StockQuantity INT NOT NULL DEFAULT 0,
                Location NVARCHAR(100) NULL,
                UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT FK_PartInventory_Parts FOREIGN KEY (PartId) REFERENCES Parts(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreatePartUsageAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PartUsage')
            CREATE TABLE PartUsage (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                VehicleId INT NOT NULL,
                WorkOrderId INT NULL,
                PartId INT NOT NULL,
                Quantity INT NOT NULL DEFAULT 1,
                UnitCost DECIMAL(18,2) NOT NULL DEFAULT 0,
                TotalCost AS (Quantity * UnitCost) PERSISTED,
                UsedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CreatedBy NVARCHAR(100) NULL,
                CONSTRAINT FK_PartUsage_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id),
                CONSTRAINT FK_PartUsage_WorkOrders FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id),
                CONSTRAINT FK_PartUsage_Parts FOREIGN KEY (PartId) REFERENCES Parts(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateVehicleBreakdownsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'VehicleBreakdowns')
            CREATE TABLE VehicleBreakdowns (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                VehicleId INT NOT NULL,
                DriverId INT NULL,
                RequestId INT NULL,
                WorkOrderId INT NULL,
                BreakdownLocation NVARCHAR(300) NULL,
                FaultReport NVARCHAR(2000) NOT NULL,
                DriverRemarks NVARCHAR(1000) NULL,
                TechnicianNotes NVARCHAR(MAX) NULL,
                RepairDetails NVARCHAR(2000) NULL,
                RepairCost DECIMAL(18,2) NOT NULL DEFAULT 0,
                Status NVARCHAR(30) NOT NULL DEFAULT N'Reported',
                ReportedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                ResolvedAt DATETIME2 NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_VehicleBreakdowns_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateMaintenanceCostsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MaintenanceCosts')
            CREATE TABLE MaintenanceCosts (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                VehicleId INT NOT NULL,
                WorkOrderId INT NULL,
                CostCategory NVARCHAR(40) NOT NULL,
                Amount DECIMAL(18,2) NOT NULL,
                CostDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                Notes NVARCHAR(500) NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_MaintenanceCosts_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task SeedServiceTypesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var types = new (string Code, string Name, int Sort)[]
        {
            ("OIL_CHANGE", "Oil Change", 1),
            ("FILTER_REPLACEMENT", "Filter Replacement", 2),
            ("TIRE_ROTATION", "Tire Rotation", 3),
            ("BRAKE_INSPECTION", "Brake Inspection", 4),
            ("BATTERY_CHECK", "Battery Check", 5),
            ("AC_SERVICE", "AC Service", 6),
            ("FULL_SERVICE", "Full Service", 7),
            ("ANNUAL_INSPECTION", "Annual Inspection", 8)
        };

        foreach (var t in types)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM ServiceTypes WHERE TenantId IS NULL AND Code = @Code AND IsDeleted = 0)
                INSERT INTO ServiceTypes (TenantId, Code, Name, SortOrder) VALUES (NULL, @Code, @Name, @Sort);
                """, new { t.Code, t.Name, t.Sort }, cancellationToken: ct));
        }
    }

    private static async Task SeedWorkOrderStatusesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var statuses = new (string Code, string Label, string Color, int Sort)[]
        {
            ("Draft", "Draft", "muted", 1),
            ("Open", "Open", "primary", 2),
            ("Assigned", "Assigned", "primary", 3),
            ("InProgress", "In Progress", "warning", 4),
            ("WaitingParts", "Waiting Parts", "warning", 5),
            ("Completed", "Completed", "success", 6),
            ("Closed", "Closed", "success", 7),
            ("Cancelled", "Cancelled", "muted", 8)
        };

        foreach (var s in statuses)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM FleetStatusCodes WHERE Category = N'WorkOrder' AND Code = @Code)
                INSERT INTO FleetStatusCodes (Category, Code, Label, ColorToken, SortOrder)
                VALUES (N'WorkOrder', @Code, @Label, @Color, @Sort);
                """, new { s.Code, s.Label, s.Color, s.Sort }, cancellationToken: ct));
        }
    }

    private static async Task ExtendSchedulerSchemaAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vehicles' AND COLUMN_NAME = 'CurrentEngineHours')
                ALTER TABLE Vehicles ADD CurrentEngineHours DECIMAL(12,2) NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleMaintenanceSchedules' AND COLUMN_NAME = 'LastServiceEngineHours')
                ALTER TABLE VehicleMaintenanceSchedules ADD LastServiceEngineHours DECIMAL(12,2) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleMaintenanceSchedules' AND COLUMN_NAME = 'NextDueEngineHours')
                ALTER TABLE VehicleMaintenanceSchedules ADD NextDueEngineHours DECIMAL(12,2) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'WorkOrders' AND COLUMN_NAME = 'ScheduleId')
                ALTER TABLE WorkOrders ADD ScheduleId INT NULL;
            """, cancellationToken: ct));
    }

    private static async Task ExtendWorkshopVendorSchemaAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Workshops' AND COLUMN_NAME = 'Rating')
                ALTER TABLE Workshops ADD Rating DECIMAL(3,2) NULL;

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Vendors')
            CREATE TABLE Vendors (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                Name NVARCHAR(200) NOT NULL,
                Category NVARCHAR(80) NOT NULL DEFAULT N'Parts',
                ContactPerson NVARCHAR(150) NULL,
                ContactPhone NVARCHAR(40) NULL,
                ContactEmail NVARCHAR(150) NULL,
                ProductsJson NVARCHAR(MAX) NULL,
                Rating DECIMAL(3,2) NULL,
                IsPreferred BIT NOT NULL DEFAULT 0,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CreatedBy NVARCHAR(100) NULL,
                UpdatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0
            );
            """, cancellationToken: ct));
    }

    private static async Task ExtendPartsInventorySchemaAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Parts' AND COLUMN_NAME = 'VehicleCompatibilityJson')
                ALTER TABLE Parts ADD VehicleCompatibilityJson NVARCHAR(MAX) NULL;

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PartStockMovements')
            CREATE TABLE PartStockMovements (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                PartId INT NOT NULL,
                MovementType NVARCHAR(20) NOT NULL,
                Quantity INT NOT NULL DEFAULT 1,
                FromLocation NVARCHAR(100) NULL,
                ToLocation NVARCHAR(100) NULL,
                VehicleId INT NULL,
                WorkOrderId INT NULL,
                Notes NVARCHAR(500) NULL,
                CreatedBy NVARCHAR(100) NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT FK_PartStockMovements_Parts FOREIGN KEY (PartId) REFERENCES Parts(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task ExtendMaintenanceReportsSchemaAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MaintenanceReportSchedules')
            CREATE TABLE MaintenanceReportSchedules (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                ReportType NVARCHAR(60) NOT NULL,
                FiltersJson NVARCHAR(MAX) NULL,
                Frequency NVARCHAR(20) NOT NULL DEFAULT N'Weekly',
                Recipients NVARCHAR(1000) NOT NULL,
                NextRunAt DATETIME2 NULL,
                LastRunAt DATETIME2 NULL,
                LastRunStatus NVARCHAR(30) NULL DEFAULT N'Pending',
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CreatedBy NVARCHAR(100) NULL,
                UpdatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0
            );
            """, cancellationToken: ct));
    }

    private static async Task ExtendWorkOrdersSchemaAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'WorkOrders' AND COLUMN_NAME = 'MaintenanceType')
                ALTER TABLE WorkOrders ADD MaintenanceType NVARCHAR(30) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'WorkOrders' AND COLUMN_NAME = 'EstimatedLaborCost')
                ALTER TABLE WorkOrders ADD EstimatedLaborCost DECIMAL(18,2) NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'WorkOrders' AND COLUMN_NAME = 'EstimatedPartsCost')
                ALTER TABLE WorkOrders ADD EstimatedPartsCost DECIMAL(18,2) NOT NULL DEFAULT 0;
            """, cancellationToken: ct));
    }

    private static async Task ExtendMaintenanceRequestsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MaintenanceRequests' AND COLUMN_NAME = 'DepartmentId')
                ALTER TABLE MaintenanceRequests ADD DepartmentId INT NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MaintenanceRequests' AND COLUMN_NAME = 'ApprovedBy')
                ALTER TABLE MaintenanceRequests ADD ApprovedBy NVARCHAR(100) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MaintenanceRequests' AND COLUMN_NAME = 'ApprovedAt')
                ALTER TABLE MaintenanceRequests ADD ApprovedAt DATETIME2 NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MaintenanceRequests' AND COLUMN_NAME = 'RejectedBy')
                ALTER TABLE MaintenanceRequests ADD RejectedBy NVARCHAR(100) NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MaintenanceRequests' AND COLUMN_NAME = 'RejectedAt')
                ALTER TABLE MaintenanceRequests ADD RejectedAt DATETIME2 NULL;
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MaintenanceRequests' AND COLUMN_NAME = 'RejectionReason')
                ALTER TABLE MaintenanceRequests ADD RejectionReason NVARCHAR(500) NULL;
            """, cancellationToken: ct));
    }

    private static async Task SeedMaintenanceRequestStatusesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var statuses = new (string Code, string Label, string Color, int Sort)[]
        {
            ("Open", "Open", "primary", 1),
            ("PendingApproval", "Pending Approval", "warning", 2),
            ("Approved", "Approved", "success", 3),
            ("Rejected", "Rejected", "danger", 4),
            ("Converted", "Converted", "muted", 5),
            ("Cancelled", "Cancelled", "muted", 6),
            ("InProgress", "In Progress", "warning", 7)
        };

        foreach (var s in statuses)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM FleetStatusCodes WHERE Category = N'MaintenanceRequest' AND Code = @Code)
                INSERT INTO FleetStatusCodes (Category, Code, Label, ColorToken, SortOrder)
                VALUES (N'MaintenanceRequest', @Code, @Label, @Color, @Sort);
                """, new { s.Code, s.Label, s.Color, s.Sort }, cancellationToken: ct));
        }
    }

    private static async Task EnsureIndexesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MaintenanceRequests_Tenant_Status')
                CREATE INDEX IX_MaintenanceRequests_Tenant_Status ON MaintenanceRequests (TenantId, Status, CreatedAt DESC) WHERE IsDeleted = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WorkOrders_Tenant_Status')
                CREATE INDEX IX_WorkOrders_Tenant_Status ON WorkOrders (TenantId, Status, CreatedAt DESC) WHERE IsDeleted = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WorkOrders_VehicleId')
                CREATE INDEX IX_WorkOrders_VehicleId ON WorkOrders (VehicleId, Status) WHERE IsDeleted = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_MaintenanceAlerts_Tenant')
                CREATE INDEX IX_MaintenanceAlerts_Tenant ON MaintenanceAlerts (TenantId, IsDismissed, CreatedAt DESC);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VehicleMaintenanceSchedules_Vehicle')
                CREATE INDEX IX_VehicleMaintenanceSchedules_Vehicle ON VehicleMaintenanceSchedules (VehicleId) WHERE IsDeleted = 0 AND IsActive = 1;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Parts_Tenant')
                CREATE INDEX IX_Parts_Tenant ON Parts (TenantId, PartNumber) WHERE IsDeleted = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Vendors_Tenant')
                CREATE INDEX IX_Vendors_Tenant ON Vendors (TenantId, Name) WHERE IsDeleted = 0;
            """, cancellationToken: ct));
    }
}
