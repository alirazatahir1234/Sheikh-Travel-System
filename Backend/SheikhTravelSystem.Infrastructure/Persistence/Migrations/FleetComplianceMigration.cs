using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Phase 4 compliance and inspection schema. Adds dedicated tables for tracking
/// vehicle/driver legal documents (insurance, registration, licenses) and for
/// recording checklist-based vehicle inspections with photo evidence. File
/// contents are never stored in SQL; only blob URLs are persisted.
/// </summary>
public static class FleetComplianceMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await CreateComplianceDocumentsAsync(connection, cancellationToken);
        await CreateInspectionTemplatesAsync(connection, cancellationToken);
        await CreateInspectionsAsync(connection, cancellationToken);
        await SeedDefaultTemplateAsync(connection, cancellationToken);

        logger.LogInformation("Fleet compliance migration completed.");
    }

    private static async Task CreateComplianceDocumentsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ComplianceDocuments')
            CREATE TABLE ComplianceDocuments (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                EntityType NVARCHAR(20) NOT NULL,        -- Vehicle | Driver
                EntityId INT NOT NULL,
                DocumentType NVARCHAR(60) NOT NULL,      -- Insurance | Registration | License | Permit
                DocumentNumber NVARCHAR(100) NULL,
                IssuedDate DATETIME2 NULL,
                ExpiryDate DATETIME2 NULL,
                Status NVARCHAR(30) NOT NULL DEFAULT N'Valid',  -- Valid | Expiring | Expired
                FileUrl NVARCHAR(500) NULL,
                Notes NVARCHAR(500) NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CreatedBy NVARCHAR(100) NULL,
                UpdatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0
            );
            """, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ComplianceDocuments_Entity')
                CREATE INDEX IX_ComplianceDocuments_Entity ON ComplianceDocuments (EntityType, EntityId) WHERE IsDeleted = 0;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ComplianceDocuments_Expiry')
                CREATE INDEX IX_ComplianceDocuments_Expiry ON ComplianceDocuments (ExpiryDate) WHERE IsDeleted = 0;
            """, cancellationToken: ct));
    }

    private static async Task CreateInspectionTemplatesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'InspectionTemplates')
            CREATE TABLE InspectionTemplates (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                Name NVARCHAR(150) NOT NULL,
                Description NVARCHAR(500) NULL,
                ChecklistJson NVARCHAR(MAX) NOT NULL,   -- array of {key,label,required}
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                IsDeleted BIT NOT NULL DEFAULT 0
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateInspectionsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Inspections')
            CREATE TABLE Inspections (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NULL,
                VehicleId INT NOT NULL,
                TemplateId INT NULL,
                DriverId INT NULL,
                InspectedBy NVARCHAR(150) NULL,
                InspectionDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                OdometerReading DECIMAL(12,2) NULL,
                Result NVARCHAR(20) NOT NULL DEFAULT N'Pass',  -- Pass | Warning | Fail
                ResultsJson NVARCHAR(MAX) NULL,                 -- array of {key,status,comment}
                PhotosJson NVARCHAR(MAX) NULL,                  -- array of blob URLs
                Comments NVARCHAR(1000) NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CreatedBy NVARCHAR(100) NULL,
                IsDeleted BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_Inspections_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles(Id),
                CONSTRAINT FK_Inspections_Templates FOREIGN KEY (TemplateId) REFERENCES InspectionTemplates(Id)
            );
            """, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Inspections_Vehicle_Date')
                CREATE INDEX IX_Inspections_Vehicle_Date ON Inspections (VehicleId, InspectionDate DESC) WHERE IsDeleted = 0;
            """, cancellationToken: ct));
    }

    private static async Task SeedDefaultTemplateAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var existing = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM InspectionTemplates WHERE IsDeleted = 0", cancellationToken: ct));
        if (existing > 0)
        {
            return;
        }

        const string checklist = """
            [
              {"key":"tyres","label":"Tyres & Wheels","required":true},
              {"key":"brakes","label":"Brakes","required":true},
              {"key":"lights","label":"Lights & Indicators","required":true},
              {"key":"engine","label":"Engine & Oil","required":true},
              {"key":"body","label":"Body & Paint","required":false},
              {"key":"interior","label":"Interior & Seats","required":false},
              {"key":"documents","label":"Documents Present","required":true},
              {"key":"firstaid","label":"First Aid & Safety Kit","required":true}
            ]
            """;

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO InspectionTemplates (Name, Description, ChecklistJson, IsActive)
            VALUES (N'Standard Vehicle Inspection', N'Default pre-trip vehicle safety checklist', @Checklist, 1);
            """, new { Checklist = checklist }, cancellationToken: ct));
    }
}
