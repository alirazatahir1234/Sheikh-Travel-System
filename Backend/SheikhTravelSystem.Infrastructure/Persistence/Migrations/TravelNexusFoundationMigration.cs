using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// TravelNexus Phase 1: reference countries, tenant configuration (visa types, document types,
/// lead sources, settings), travel customer profiles and travelers; registers module
/// <c>TRAVEL_AGENCY</c>, its permissions and ERP menu rows.
/// <para>
/// The module is NOT auto-enabled for any tenant (TenantType was back-filled to "Travel Agency"
/// for fleet tenants too). Platform admins enable it per company via module management.
/// </para>
/// Idempotent: every DDL statement is guarded.
/// </summary>
public static class TravelNexusFoundationMigration
{
    private static readonly (string Code, string Desc)[] PermissionRows =
    [
        (TravelPermissions.View, "Open TravelNexus"),
        (TravelPermissions.Settings, "Configure TravelNexus visa types, document types, lead sources and numbering"),
        (TravelPermissions.CustomerView, "View travel customers and travelers"),
        (TravelPermissions.CustomerManage, "Create and edit travel customers and travelers"),
        (TravelPermissions.TravelerViewPii, "Reveal full passport and CNIC numbers of travelers"),
    ];

    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // ── Platform-scope reference data (no TenantId) ──────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelCountries', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelCountries (
                    Code CHAR(2) NOT NULL CONSTRAINT PK_TravelCountries PRIMARY KEY,
                    Name NVARCHAR(100) NOT NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_TravelCountries_IsActive DEFAULT (1)
                );
            END
            """, cancellationToken: cancellationToken));

        foreach (var (code, name) in TravelCountryCatalog.All)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM dbo.TravelCountries WHERE Code = @Code)
                    INSERT INTO dbo.TravelCountries (Code, Name) VALUES (@Code, @Name);
                """, new { Code = code, Name = name }, cancellationToken: cancellationToken));
        }

        // ── Tenant configuration ─────────────────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelSettings', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelSettings (
                    TenantId INT NOT NULL CONSTRAINT PK_TravelSettings PRIMARY KEY,
                    DefaultCurrencyCode CHAR(3) NULL,
                    EnquiryPrefix NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelSettings_EnqPrefix DEFAULT (N'ENQ'),
                    VisaCasePrefix NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelSettings_VisaPrefix DEFAULT (N'VC'),
                    InvoicePrefix NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelSettings_InvPrefix DEFAULT (N'TINV'),
                    PassportExpiryWarningDays INT NOT NULL CONSTRAINT DF_TravelSettings_PpWarn DEFAULT (180),
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy INT NULL,
                    CONSTRAINT FK_TravelSettings_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT CK_TravelSettings_PpWarn CHECK (PassportExpiryWarningDays BETWEEN 0 AND 730)
                );
            END

            IF OBJECT_ID(N'dbo.TravelVisaTypes', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelVisaTypes (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelVisaTypes PRIMARY KEY,
                    TenantId INT NOT NULL,
                    CountryCode CHAR(2) NOT NULL,
                    Name NVARCHAR(150) NOT NULL,
                    Category NVARCHAR(20) NOT NULL,
                    EntryType NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelVisaTypes_Entry DEFAULT (N'Single'),
                    ValidityDays INT NULL,
                    StayDays INT NULL,
                    ProcessingDays INT NULL,
                    DefaultFee DECIMAL(18,2) NULL,
                    CurrencyCode CHAR(3) NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_TravelVisaTypes_IsActive DEFAULT (1),
                    Notes NVARCHAR(1000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelVisaTypes_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelVisaTypes_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelVisaTypes_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelVisaTypes_Countries FOREIGN KEY (CountryCode) REFERENCES dbo.TravelCountries(Code),
                    CONSTRAINT CK_TravelVisaTypes_Category CHECK (Category IN
                        (N'Tourist', N'Business', N'Umrah', N'Hajj', N'Work', N'Student', N'Transit', N'Family', N'Other')),
                    CONSTRAINT CK_TravelVisaTypes_Entry CHECK (EntryType IN (N'Single', N'Double', N'Multiple')),
                    CONSTRAINT CK_TravelVisaTypes_FeeCurrency CHECK (DefaultFee IS NULL OR CurrencyCode IS NOT NULL),
                    CONSTRAINT CK_TravelVisaTypes_FeeNonNegative CHECK (DefaultFee IS NULL OR DefaultFee >= 0)
                );
                CREATE UNIQUE INDEX UX_TravelVisaTypes_Tenant_Country_Name
                    ON dbo.TravelVisaTypes (TenantId, CountryCode, Name) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelDocumentTypes', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelDocumentTypes (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelDocumentTypes PRIMARY KEY,
                    TenantId INT NOT NULL,
                    Code NVARCHAR(40) NOT NULL,
                    Name NVARCHAR(150) NOT NULL,
                    Description NVARCHAR(500) NULL,
                    RequiresExpiryDate BIT NOT NULL CONSTRAINT DF_TravelDocumentTypes_ReqExp DEFAULT (0),
                    IsActive BIT NOT NULL CONSTRAINT DF_TravelDocumentTypes_IsActive DEFAULT (1),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelDocumentTypes_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelDocumentTypes_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelDocumentTypes_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
                );
                CREATE UNIQUE INDEX UX_TravelDocumentTypes_Tenant_Code
                    ON dbo.TravelDocumentTypes (TenantId, Code) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelLeadSources', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelLeadSources (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelLeadSources PRIMARY KEY,
                    TenantId INT NOT NULL,
                    Name NVARCHAR(100) NOT NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_TravelLeadSources_IsActive DEFAULT (1),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelLeadSources_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelLeadSources_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelLeadSources_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
                );
                CREATE UNIQUE INDEX UX_TravelLeadSources_Tenant_Name
                    ON dbo.TravelLeadSources (TenantId, Name) WHERE IsDeleted = 0;
            END
            """, cancellationToken: cancellationToken));

        // ── CRM: customer profile (1:1 with dbo.Customers) and travelers ─────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelCustomerProfiles', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelCustomerProfiles (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelCustomerProfiles PRIMARY KEY,
                    TenantId INT NOT NULL,
                    CustomerId INT NOT NULL,
                    CustomerKind NVARCHAR(20) NOT NULL,
                    CompanyName NVARCHAR(200) NULL,
                    TaxNumber NVARCHAR(50) NULL,
                    ContactPerson NVARCHAR(150) NULL,
                    AlternatePhone NVARCHAR(30) NULL,
                    City NVARCHAR(100) NULL,
                    AssignedAgentUserId INT NULL,
                    BranchId INT NULL,
                    LeadSourceId INT NULL,
                    Notes NVARCHAR(2000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelCustomerProfiles_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelCustomerProfiles_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelCustomerProfiles_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelCustomerProfiles_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
                    CONSTRAINT FK_TravelCustomerProfiles_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches(Id),
                    CONSTRAINT FK_TravelCustomerProfiles_Agent FOREIGN KEY (AssignedAgentUserId) REFERENCES dbo.Users(Id),
                    CONSTRAINT FK_TravelCustomerProfiles_LeadSource FOREIGN KEY (LeadSourceId) REFERENCES dbo.TravelLeadSources(Id),
                    CONSTRAINT CK_TravelCustomerProfiles_Kind CHECK (CustomerKind IN (N'Individual', N'Corporate')),
                    CONSTRAINT CK_TravelCustomerProfiles_Company CHECK (CustomerKind = N'Individual' OR CompanyName IS NOT NULL)
                );
                CREATE UNIQUE INDEX UX_TravelCustomerProfiles_Tenant_Customer
                    ON dbo.TravelCustomerProfiles (TenantId, CustomerId);
                CREATE INDEX IX_TravelCustomerProfiles_Tenant_Agent
                    ON dbo.TravelCustomerProfiles (TenantId, AssignedAgentUserId) WHERE IsDeleted = 0;
                CREATE INDEX IX_TravelCustomerProfiles_Tenant_Branch
                    ON dbo.TravelCustomerProfiles (TenantId, BranchId) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelTravelers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelTravelers (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelTravelers PRIMARY KEY,
                    TenantId INT NOT NULL,
                    CustomerId INT NOT NULL,
                    Title NVARCHAR(10) NULL,
                    GivenNames NVARCHAR(100) NOT NULL,
                    Surname NVARCHAR(100) NOT NULL,
                    Gender NVARCHAR(10) NOT NULL,
                    DateOfBirth DATE NULL,
                    PlaceOfBirth NVARCHAR(100) NULL,
                    NationalityCode CHAR(2) NULL,
                    PassportNumber NVARCHAR(30) NULL,
                    PassportIssuingCountryCode CHAR(2) NULL,
                    PassportIssueDate DATE NULL,
                    PassportExpiryDate DATE NULL,
                    Cnic NVARCHAR(20) NULL,
                    Phone NVARCHAR(30) NULL,
                    Email NVARCHAR(200) NULL,
                    MahramTravelerId INT NULL,
                    MahramRelation NVARCHAR(30) NULL,
                    Notes NVARCHAR(1000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelTravelers_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelTravelers_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelTravelers_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelTravelers_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
                    CONSTRAINT FK_TravelTravelers_Nationality FOREIGN KEY (NationalityCode) REFERENCES dbo.TravelCountries(Code),
                    CONSTRAINT FK_TravelTravelers_PassportCountry FOREIGN KEY (PassportIssuingCountryCode) REFERENCES dbo.TravelCountries(Code),
                    CONSTRAINT FK_TravelTravelers_Mahram FOREIGN KEY (MahramTravelerId) REFERENCES dbo.TravelTravelers(Id),
                    CONSTRAINT CK_TravelTravelers_Gender CHECK (Gender IN (N'Male', N'Female')),
                    CONSTRAINT CK_TravelTravelers_PassportDates CHECK
                        (PassportIssueDate IS NULL OR PassportExpiryDate IS NULL OR PassportExpiryDate > PassportIssueDate),
                    CONSTRAINT CK_TravelTravelers_NotOwnMahram CHECK (MahramTravelerId IS NULL OR MahramTravelerId <> Id)
                );
                CREATE INDEX IX_TravelTravelers_Tenant_Customer
                    ON dbo.TravelTravelers (TenantId, CustomerId) WHERE IsDeleted = 0;
                CREATE INDEX IX_TravelTravelers_Tenant_PassportExpiry
                    ON dbo.TravelTravelers (TenantId, PassportExpiryDate) WHERE IsDeleted = 0 AND PassportExpiryDate IS NOT NULL;
                CREATE UNIQUE INDEX UX_TravelTravelers_Tenant_Passport
                    ON dbo.TravelTravelers (TenantId, PassportIssuingCountryCode, PassportNumber)
                    WHERE IsDeleted = 0 AND PassportNumber IS NOT NULL;
            END
            """, cancellationToken: cancellationToken));

        // ── Module registry row (opt-in, toggleable; no tenant backfill) ─────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.Modules', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleCode = @Code)
                    INSERT INTO Modules (ModuleCode, ModuleName, DisplayName, Description, Category, Icon, Route, SortOrder,
                        DependenciesJson, Visible, IsMobileSupported, IsAISupported, IsGPSSupported, Status, LegacyKeysJson)
                    VALUES (@Code, N'TravelNexus', N'TravelNexus',
                        N'Travel agency CRM, visa processing, Umrah groups, bookings and travel finance',
                        N'Travel', N'flight_takeoff', N'/travel', 55,
                        N'["DASHBOARD"]', 1, 0, 0, 0, N'Active', @LegacyJson);
                ELSE
                    UPDATE Modules SET
                        ModuleName = N'TravelNexus', DisplayName = N'TravelNexus',
                        Route = N'/travel', Status = N'Active', LegacyKeysJson = @LegacyJson
                    WHERE ModuleCode = @Code;
            END

            IF OBJECT_ID(N'dbo.FeatureDefinitions', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM FeatureDefinitions WHERE FeatureKey = @LegacyKey)
                    INSERT INTO FeatureDefinitions (
                        FeatureKey, ModuleKey, Name, Description, SortOrder, IsActive,
                        DisplayName, Category, Icon, Route, Visible, Status)
                    VALUES (
                        @LegacyKey, @Code, N'TravelNexus', N'Travel agency, visa and Umrah management',
                        55, 1, N'TravelNexus', N'Travel', N'flight_takeoff', N'/travel', 1, N'ModuleLegacy');
            END
            """,
            new
            {
                Code = TenantModuleCatalog.TravelAgencyModuleCode,
                LegacyKey = TenantModuleCatalog.TravelAgencyLegacyKey,
                LegacyJson = $"[\"{TenantModuleCatalog.TravelAgencyLegacyKey}\"]"
            },
            cancellationToken: cancellationToken));

        // ── Permissions + role grants ────────────────────────────────────────────
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
                connection, role, TravelPermissions.Phase1, cancellationToken);
        }

        // ── ERP menu (only screens that exist; later phases add their own rows) ──
        var hasMenuTables = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN OBJECT_ID(N'dbo.PlatformMenus', N'U') IS NOT NULL
                         AND OBJECT_ID(N'dbo.PlatformModules', N'U') IS NOT NULL THEN 1 ELSE 0 END
            """, cancellationToken: cancellationToken)) == 1;

        if (hasMenuTables)
        {
            const string platformModuleKey = "travel-nexus";
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = @ModuleKey)
                    INSERT INTO PlatformModules (Name, ModuleKey, Icon, SortOrder, IsCollapsible,
                        DisplayName, Description, Visible)
                    VALUES (N'TravelNexus', @ModuleKey, N'flight_takeoff', 36, 1,
                        N'TravelNexus', N'Travel agency, visa and Umrah management', 1);
                """, new { ModuleKey = platformModuleKey }, cancellationToken: cancellationToken));

            var menus = new (string Name, string Route, string Icon, string Perm, int Sort)[]
            {
                ("Customers", "/travel/customers", "groups", TravelPermissions.CustomerView, 30),
                ("Settings", "/travel/settings", "tune", TravelPermissions.Settings, 150),
            };

            foreach (var m in menus)
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
                        DisplayName, Description, Category, Visible, FeatureKey, ModuleKey,
                        IsMobileSupported, UpdatedAt)
                    SELECT mod.Id, NULL, @Name, @Route, @Icon, @Perm, @Sort, 1,
                           @Name, NULL, N'Travel', 1, @FeatureKey, @ModuleCode, 0, SYSUTCDATETIME()
                    FROM PlatformModules mod
                    WHERE mod.ModuleKey = @PlatformModule
                      AND NOT EXISTS (SELECT 1 FROM PlatformMenus x WHERE x.Route = @Route);
                    """,
                    new
                    {
                        PlatformModule = platformModuleKey,
                        m.Name,
                        m.Route,
                        m.Icon,
                        m.Perm,
                        m.Sort,
                        ModuleCode = TenantModuleCatalog.TravelAgencyModuleCode,
                        FeatureKey = TenantModuleCatalog.TravelAgencyLegacyKey
                    },
                    cancellationToken: cancellationToken));
            }
        }

        logger.LogInformation(
            "TravelNexusFoundationMigration applied (countries={CountryCount}, module {ModuleCode} registered, not auto-enabled).",
            TravelCountryCatalog.All.Count, TenantModuleCatalog.TravelAgencyModuleCode);
    }
}
