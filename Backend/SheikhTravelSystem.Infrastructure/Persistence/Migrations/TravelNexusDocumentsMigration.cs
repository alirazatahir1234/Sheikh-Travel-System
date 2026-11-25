using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// TravelNexus Phase 3: versioned document checklists, per-applicant document requirements, private document file
/// metadata (files live in ITravelDocumentStorage, never in /uploads), case checklist reference, widened history
/// event types, Phase 3 permissions and the Document Checklists menu row. Additive and idempotent.
/// </summary>
public static class TravelNexusDocumentsMigration
{
    private static readonly (string Code, string Desc)[] PermissionRows =
    [
        (TravelPermissions.DocumentManage, "Upload applicant documents and request additional documents"),
        (TravelPermissions.DocumentVerify, "Verify, reject or waive applicant documents"),
        (TravelPermissions.DocumentDownload, "Open applicant document files (audited)"),
    ];

    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelChecklistTemplates', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelChecklistTemplates (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelChecklistTemplates PRIMARY KEY,
                    TenantId INT NOT NULL,
                    CountryCode CHAR(2) NOT NULL,
                    VisaTypeId INT NULL,
                    Name NVARCHAR(150) NOT NULL,
                    Version INT NOT NULL CONSTRAINT DF_TravelChecklistTemplates_Version DEFAULT (1),
                    Status NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelChecklistTemplates_Status DEFAULT (N'Draft'),
                    ActivatedAt DATETIME2 NULL,
                    Notes NVARCHAR(1000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelChecklistTemplates_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelChecklistTemplates_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelChecklistTemplates_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelChecklistTemplates_Country FOREIGN KEY (CountryCode) REFERENCES dbo.TravelCountries(Code),
                    CONSTRAINT FK_TravelChecklistTemplates_VisaType FOREIGN KEY (VisaTypeId) REFERENCES dbo.TravelVisaTypes(Id),
                    CONSTRAINT CK_TravelChecklistTemplates_Status CHECK (Status IN (N'Draft', N'Active', N'Retired')),
                    CONSTRAINT CK_TravelChecklistTemplates_Version CHECK (Version >= 1),
                    CONSTRAINT CK_TravelChecklistTemplates_Activated CHECK (Status = N'Draft' OR ActivatedAt IS NOT NULL)
                );
                -- One Active checklist per tenant + country + visa type (NULL visa type = country-wide).
                CREATE UNIQUE INDEX UX_TravelChecklistTemplates_Active_Specific
                    ON dbo.TravelChecklistTemplates (TenantId, CountryCode, VisaTypeId)
                    WHERE Status = N'Active' AND IsDeleted = 0 AND VisaTypeId IS NOT NULL;
                CREATE UNIQUE INDEX UX_TravelChecklistTemplates_Active_Country
                    ON dbo.TravelChecklistTemplates (TenantId, CountryCode)
                    WHERE Status = N'Active' AND IsDeleted = 0 AND VisaTypeId IS NULL;
                CREATE INDEX IX_TravelChecklistTemplates_Tenant_Country
                    ON dbo.TravelChecklistTemplates (TenantId, CountryCode, VisaTypeId) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelChecklistItems', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelChecklistItems (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelChecklistItems PRIMARY KEY,
                    TenantId INT NOT NULL,
                    TemplateId INT NOT NULL,
                    DocumentTypeId INT NOT NULL,
                    IsMandatory BIT NOT NULL CONSTRAINT DF_TravelChecklistItems_Mandatory DEFAULT (1),
                    ApplicantCondition NVARCHAR(20) NOT NULL CONSTRAINT DF_TravelChecklistItems_Condition DEFAULT (N'All'),
                    MinValidityDays INT NULL,
                    Instructions NVARCHAR(500) NULL,
                    SortOrder INT NOT NULL CONSTRAINT DF_TravelChecklistItems_Sort DEFAULT (0),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelChecklistItems_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT FK_TravelChecklistItems_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelChecklistItems_Template FOREIGN KEY (TemplateId) REFERENCES dbo.TravelChecklistTemplates(Id),
                    CONSTRAINT FK_TravelChecklistItems_DocType FOREIGN KEY (DocumentTypeId) REFERENCES dbo.TravelDocumentTypes(Id),
                    CONSTRAINT CK_TravelChecklistItems_Condition CHECK (ApplicantCondition IN
                        (N'All', N'Adult', N'Minor', N'Male', N'Female', N'FemaleWithMahram')),
                    CONSTRAINT CK_TravelChecklistItems_MinValidity CHECK (MinValidityDays IS NULL OR MinValidityDays BETWEEN 1 AND 3650)
                );
                CREATE UNIQUE INDEX UX_TravelChecklistItems_Template_Doc_Condition
                    ON dbo.TravelChecklistItems (TenantId, TemplateId, DocumentTypeId, ApplicantCondition);
            END

            IF OBJECT_ID(N'dbo.TravelApplicantDocuments', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelApplicantDocuments (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelApplicantDocuments PRIMARY KEY,
                    TenantId INT NOT NULL,
                    VisaCaseId INT NOT NULL,
                    ApplicantId INT NOT NULL,
                    DocumentTypeId INT NOT NULL,
                    ChecklistItemId INT NULL,
                    IsMandatory BIT NOT NULL,
                    MinValidityDays INT NULL,
                    Instructions NVARCHAR(500) NULL,
                    Status NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelApplicantDocuments_Status DEFAULT (N'Missing'),
                    CurrentFileId INT NULL,
                    ExpiryDate DATE NULL,
                    ReviewNote NVARCHAR(500) NULL,
                    ReviewedBy INT NULL,
                    ReviewedAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelApplicantDocuments_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelApplicantDocuments_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelApplicantDocuments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelApplicantDocuments_Case FOREIGN KEY (VisaCaseId) REFERENCES dbo.TravelVisaCases(Id),
                    CONSTRAINT FK_TravelApplicantDocuments_Applicant FOREIGN KEY (ApplicantId) REFERENCES dbo.TravelVisaApplicants(Id),
                    CONSTRAINT FK_TravelApplicantDocuments_DocType FOREIGN KEY (DocumentTypeId) REFERENCES dbo.TravelDocumentTypes(Id),
                    CONSTRAINT FK_TravelApplicantDocuments_Item FOREIGN KEY (ChecklistItemId) REFERENCES dbo.TravelChecklistItems(Id),
                    CONSTRAINT CK_TravelApplicantDocuments_Status CHECK (Status IN
                        (N'Missing', N'Uploaded', N'Verified', N'Rejected', N'Waived')),
                    CONSTRAINT CK_TravelApplicantDocuments_Review CHECK
                        (Status NOT IN (N'Rejected', N'Waived') OR ReviewNote IS NOT NULL),
                    CONSTRAINT CK_TravelApplicantDocuments_File CHECK
                        (Status IN (N'Missing', N'Waived') OR CurrentFileId IS NOT NULL)
                );
                CREATE UNIQUE INDEX UX_TravelApplicantDocuments_Applicant_Doc
                    ON dbo.TravelApplicantDocuments (TenantId, ApplicantId, DocumentTypeId) WHERE IsDeleted = 0;
                CREATE INDEX IX_TravelApplicantDocuments_Tenant_Case
                    ON dbo.TravelApplicantDocuments (TenantId, VisaCaseId, Status) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelDocumentFiles', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelDocumentFiles (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelDocumentFiles PRIMARY KEY,
                    TenantId INT NOT NULL,
                    ApplicantDocumentId INT NOT NULL,
                    StorageKey NVARCHAR(200) NOT NULL,
                    OriginalFileName NVARCHAR(200) NOT NULL,
                    ContentType NVARCHAR(50) NOT NULL,
                    SizeBytes BIGINT NOT NULL,
                    Sha256 CHAR(64) NOT NULL,
                    UploadedBy INT NULL,
                    UploadedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelDocumentFiles_UploadedAt DEFAULT (SYSUTCDATETIME()),
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelDocumentFiles_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelDocumentFiles_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelDocumentFiles_Requirement FOREIGN KEY (ApplicantDocumentId) REFERENCES dbo.TravelApplicantDocuments(Id),
                    CONSTRAINT CK_TravelDocumentFiles_ContentType CHECK (ContentType IN (N'application/pdf', N'image/jpeg', N'image/png')),
                    CONSTRAINT CK_TravelDocumentFiles_Size CHECK (SizeBytes > 0)
                );
                CREATE UNIQUE INDEX UX_TravelDocumentFiles_StorageKey ON dbo.TravelDocumentFiles (StorageKey);
                CREATE INDEX IX_TravelDocumentFiles_Tenant_Requirement
                    ON dbo.TravelDocumentFiles (TenantId, ApplicantDocumentId) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.FK_TravelApplicantDocuments_CurrentFile', N'F') IS NULL
                ALTER TABLE dbo.TravelApplicantDocuments
                    ADD CONSTRAINT FK_TravelApplicantDocuments_CurrentFile FOREIGN KEY (CurrentFileId) REFERENCES dbo.TravelDocumentFiles(Id);

            IF COL_LENGTH(N'dbo.TravelVisaCases', N'ChecklistTemplateId') IS NULL
                ALTER TABLE dbo.TravelVisaCases ADD ChecklistTemplateId INT NULL
                    CONSTRAINT FK_TravelVisaCases_Checklist FOREIGN KEY REFERENCES dbo.TravelChecklistTemplates(Id);
            IF COL_LENGTH(N'dbo.TravelVisaCases', N'ChecklistVersion') IS NULL
                ALTER TABLE dbo.TravelVisaCases ADD ChecklistVersion INT NULL;
            """, cancellationToken: cancellationToken));

        // Widen history event types (drop + recreate the CHECK; existing rows all satisfy the wider list).
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.CK_TravelVisaStatusHistory_Event', N'C') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.check_constraints
                               WHERE name = N'CK_TravelVisaStatusHistory_Event' AND definition LIKE N'%DocumentUploaded%')
            BEGIN
                ALTER TABLE dbo.TravelVisaStatusHistory DROP CONSTRAINT CK_TravelVisaStatusHistory_Event;
                ALTER TABLE dbo.TravelVisaStatusHistory ADD CONSTRAINT CK_TravelVisaStatusHistory_Event CHECK (EventType IN
                    (N'CaseStatus', N'ApplicantAdded', N'ApplicantRemoved', N'ApplicantDecision',
                     N'DocumentUploaded', N'DocumentVerified', N'DocumentRejected', N'DocumentWaived',
                     N'DocumentRequested', N'DocumentRemoved'));
            END
            """, cancellationToken: cancellationToken));

        foreach (var (code, desc) in PermissionRows)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions')
                AND NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                VALUES (N'Travel', @Code, @Desc);
                """, new { Code = code, Desc = desc }, cancellationToken: cancellationToken));
        }

        foreach (var role in new[] { "SUPER_ADMIN", "TENANT_ADMIN", "OWNER" })
        {
            await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
                connection, role, TravelPermissions.Phase3, cancellationToken);
        }

        var hasMenuTables = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN OBJECT_ID(N'dbo.PlatformMenus', N'U') IS NOT NULL
                         AND OBJECT_ID(N'dbo.PlatformModules', N'U') IS NOT NULL THEN 1 ELSE 0 END
            """, cancellationToken: cancellationToken)) == 1;
        if (hasMenuTables)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE pm SET
                    pm.ModuleId = mod.Id, pm.ParentId = NULL, pm.Name = @Name, pm.Icon = @Icon,
                    pm.PermissionCode = @Perm, pm.SortOrder = @Sort, pm.IsActive = 1, pm.Visible = 1,
                    pm.DisplayName = @Name, pm.Category = N'Travel', pm.ModuleKey = @ModuleCode,
                    pm.FeatureKey = @FeatureKey, pm.UpdatedAt = SYSUTCDATETIME()
                FROM PlatformMenus pm
                CROSS JOIN PlatformModules mod
                WHERE mod.ModuleKey = @PlatformModule AND pm.Route = @Route;

                IF @@ROWCOUNT = 0
                INSERT INTO PlatformMenus (
                    ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                    DisplayName, Description, Category, Visible, FeatureKey, ModuleKey, IsMobileSupported, UpdatedAt)
                SELECT mod.Id, NULL, @Name, @Route, @Icon, @Perm, @Sort, 1,
                       @Name, NULL, N'Travel', 1, @FeatureKey, @ModuleCode, 0, SYSUTCDATETIME()
                FROM PlatformModules mod
                WHERE mod.ModuleKey = @PlatformModule
                  AND NOT EXISTS (SELECT 1 FROM PlatformMenus x WHERE x.Route = @Route);
                """,
                new
                {
                    PlatformModule = "travel-nexus",
                    Name = "Document Checklists",
                    Route = "/travel/checklists",
                    Icon = "checklist",
                    Perm = TravelPermissions.Settings,
                    Sort = 50,
                    ModuleCode = TenantModuleCatalog.TravelAgencyModuleCode,
                    FeatureKey = TenantModuleCatalog.TravelAgencyLegacyKey
                }, cancellationToken: cancellationToken));
        }

        logger.LogInformation("TravelNexusDocumentsMigration applied (checklists, applicant documents, document files, permissions).");
    }
}
