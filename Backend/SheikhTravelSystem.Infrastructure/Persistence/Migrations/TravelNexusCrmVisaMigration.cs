using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// TravelNexus Phase 2: per-tenant number sequences, lost reasons, enquiries + activities,
/// visa cases + applicants + status history; Phase 2 permissions and menu rows.
/// Additive only. Idempotent: every DDL statement is guarded.
/// </summary>
public static class TravelNexusCrmVisaMigration
{
    private static readonly (string Code, string Desc)[] PermissionRows =
    [
        (TravelPermissions.EnquiryView, "View enquiries, follow-ups and communication history"),
        (TravelPermissions.EnquiryManage, "Create, update, convert and close enquiries"),
        (TravelPermissions.VisaView, "View visa cases, applicants and history"),
        (TravelPermissions.VisaManage, "Create visa cases, manage applicants and move cases through the pipeline"),
        (TravelPermissions.VisaDecide, "Record embassy decisions (approve or reject applicants)"),
    ];

    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelNumberSequences', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelNumberSequences (
                    TenantId INT NOT NULL,
                    SequenceKey NVARCHAR(30) NOT NULL,
                    NextValue INT NOT NULL CONSTRAINT DF_TravelNumberSequences_Next DEFAULT (1),
                    CONSTRAINT PK_TravelNumberSequences PRIMARY KEY (TenantId, SequenceKey),
                    CONSTRAINT FK_TravelNumberSequences_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
                );
            END

            IF OBJECT_ID(N'dbo.TravelLostReasons', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelLostReasons (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelLostReasons PRIMARY KEY,
                    TenantId INT NOT NULL,
                    Name NVARCHAR(100) NOT NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_TravelLostReasons_IsActive DEFAULT (1),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelLostReasons_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelLostReasons_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelLostReasons_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
                );
                CREATE UNIQUE INDEX UX_TravelLostReasons_Tenant_Name
                    ON dbo.TravelLostReasons (TenantId, Name) WHERE IsDeleted = 0;
            END
            """, cancellationToken: cancellationToken));

        // ── Enquiries ───────────────────────────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelEnquiries', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelEnquiries (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelEnquiries PRIMARY KEY,
                    TenantId INT NOT NULL,
                    EnquiryNumber NVARCHAR(30) NOT NULL,
                    CustomerId INT NULL,
                    LeadName NVARCHAR(100) NULL,
                    LeadPhone NVARCHAR(30) NULL,
                    LeadEmail NVARCHAR(200) NULL,
                    ServiceType NVARCHAR(20) NOT NULL,
                    DestinationCountryCode CHAR(2) NULL,
                    VisaTypeId INT NULL,
                    TravelDateFrom DATE NULL,
                    TravelDateTo DATE NULL,
                    Adults INT NOT NULL CONSTRAINT DF_TravelEnquiries_Adults DEFAULT (1),
                    Children INT NOT NULL CONSTRAINT DF_TravelEnquiries_Children DEFAULT (0),
                    Infants INT NOT NULL CONSTRAINT DF_TravelEnquiries_Infants DEFAULT (0),
                    Budget DECIMAL(18,2) NULL,
                    CurrencyCode CHAR(3) NULL,
                    LeadSourceId INT NULL,
                    AssignedAgentUserId INT NULL,
                    BranchId INT NULL,
                    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_TravelEnquiries_Status DEFAULT (N'New'),
                    Priority NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelEnquiries_Priority DEFAULT (N'Normal'),
                    NextFollowUpAt DATETIME2 NULL,
                    LostReasonId INT NULL,
                    LostNotes NVARCHAR(1000) NULL,
                    ConvertedVisaCaseId INT NULL,
                    ClosedAt DATETIME2 NULL,
                    Notes NVARCHAR(2000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelEnquiries_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelEnquiries_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelEnquiries_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelEnquiries_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
                    CONSTRAINT FK_TravelEnquiries_Country FOREIGN KEY (DestinationCountryCode) REFERENCES dbo.TravelCountries(Code),
                    CONSTRAINT FK_TravelEnquiries_VisaType FOREIGN KEY (VisaTypeId) REFERENCES dbo.TravelVisaTypes(Id),
                    CONSTRAINT FK_TravelEnquiries_LeadSource FOREIGN KEY (LeadSourceId) REFERENCES dbo.TravelLeadSources(Id),
                    CONSTRAINT FK_TravelEnquiries_Agent FOREIGN KEY (AssignedAgentUserId) REFERENCES dbo.Users(Id),
                    CONSTRAINT FK_TravelEnquiries_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branches(Id),
                    CONSTRAINT FK_TravelEnquiries_LostReason FOREIGN KEY (LostReasonId) REFERENCES dbo.TravelLostReasons(Id),
                    CONSTRAINT CK_TravelEnquiries_Service CHECK (ServiceType IN
                        (N'Visa', N'Umrah', N'Hajj', N'Flight', N'Hotel', N'Package', N'Other')),
                    CONSTRAINT CK_TravelEnquiries_Status CHECK (Status IN
                        (N'New', N'Contacted', N'Qualified', N'Quoted', N'Won', N'Lost')),
                    CONSTRAINT CK_TravelEnquiries_Priority CHECK (Priority IN (N'Low', N'Normal', N'High')),
                    CONSTRAINT CK_TravelEnquiries_Lead CHECK
                        (CustomerId IS NOT NULL OR (LeadName IS NOT NULL AND LeadPhone IS NOT NULL)),
                    CONSTRAINT CK_TravelEnquiries_Lost CHECK (Status <> N'Lost' OR LostReasonId IS NOT NULL),
                    CONSTRAINT CK_TravelEnquiries_Won CHECK (Status <> N'Won' OR CustomerId IS NOT NULL),
                    CONSTRAINT CK_TravelEnquiries_BudgetCurrency CHECK (Budget IS NULL OR CurrencyCode IS NOT NULL),
                    CONSTRAINT CK_TravelEnquiries_Pax CHECK (Adults >= 0 AND Children >= 0 AND Infants >= 0 AND Adults + Children + Infants >= 1),
                    CONSTRAINT CK_TravelEnquiries_Dates CHECK (TravelDateFrom IS NULL OR TravelDateTo IS NULL OR TravelDateTo >= TravelDateFrom)
                );
                CREATE UNIQUE INDEX UX_TravelEnquiries_Tenant_Number ON dbo.TravelEnquiries (TenantId, EnquiryNumber);
                CREATE INDEX IX_TravelEnquiries_Tenant_Status ON dbo.TravelEnquiries (TenantId, Status) WHERE IsDeleted = 0;
                CREATE INDEX IX_TravelEnquiries_Tenant_FollowUp ON dbo.TravelEnquiries (TenantId, NextFollowUpAt)
                    WHERE IsDeleted = 0 AND NextFollowUpAt IS NOT NULL;
                CREATE INDEX IX_TravelEnquiries_Tenant_Customer ON dbo.TravelEnquiries (TenantId, CustomerId) WHERE IsDeleted = 0;
                CREATE INDEX IX_TravelEnquiries_Tenant_Created ON dbo.TravelEnquiries (TenantId, CreatedAt) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelEnquiryActivities', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelEnquiryActivities (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelEnquiryActivities PRIMARY KEY,
                    TenantId INT NOT NULL,
                    EnquiryId INT NOT NULL,
                    ActivityType NVARCHAR(20) NOT NULL,
                    Direction NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelEnquiryActivities_Dir DEFAULT (N'Internal'),
                    Summary NVARCHAR(1000) NOT NULL,
                    Outcome NVARCHAR(500) NULL,
                    OccurredAt DATETIME2 NOT NULL,
                    NextFollowUpAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelEnquiryActivities_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    CONSTRAINT FK_TravelEnquiryActivities_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelEnquiryActivities_Enquiry FOREIGN KEY (EnquiryId) REFERENCES dbo.TravelEnquiries(Id),
                    CONSTRAINT CK_TravelEnquiryActivities_Type CHECK (ActivityType IN
                        (N'Call', N'WhatsApp', N'Sms', N'Email', N'Meeting', N'Note', N'StatusChange')),
                    CONSTRAINT CK_TravelEnquiryActivities_Dir CHECK (Direction IN (N'Inbound', N'Outbound', N'Internal'))
                );
                CREATE INDEX IX_TravelEnquiryActivities_Tenant_Enquiry
                    ON dbo.TravelEnquiryActivities (TenantId, EnquiryId, OccurredAt);
            END
            """, cancellationToken: cancellationToken));

        // ── Visa cases ──────────────────────────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelVisaCases', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelVisaCases (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelVisaCases PRIMARY KEY,
                    TenantId INT NOT NULL,
                    CaseNumber NVARCHAR(30) NOT NULL,
                    CustomerId INT NOT NULL,
                    EnquiryId INT NULL,
                    CountryCode CHAR(2) NOT NULL,
                    VisaTypeId INT NOT NULL,
                    IntendedTravelDate DATE NULL,
                    AppointmentAt DATETIME2 NULL,
                    SubmittedAt DATETIME2 NULL,
                    SubmissionReference NVARCHAR(100) NULL,
                    ExpectedDecisionDate DATE NULL,
                    DecidedAt DATETIME2 NULL,
                    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_TravelVisaCases_Status DEFAULT (N'Draft'),
                    OnHoldFromStatus NVARCHAR(20) NULL,
                    Priority NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelVisaCases_Priority DEFAULT (N'Normal'),
                    AssignedAgentUserId INT NULL,
                    BranchId INT NULL,
                    QuotedServiceFee DECIMAL(18,2) NULL,
                    QuotedEmbassyFee DECIMAL(18,2) NULL,
                    CurrencyCode CHAR(3) NULL,
                    CancelReason NVARCHAR(500) NULL,
                    Notes NVARCHAR(2000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelVisaCases_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelVisaCases_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelVisaCases_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelVisaCases_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
                    CONSTRAINT FK_TravelVisaCases_Enquiry FOREIGN KEY (EnquiryId) REFERENCES dbo.TravelEnquiries(Id),
                    CONSTRAINT FK_TravelVisaCases_Country FOREIGN KEY (CountryCode) REFERENCES dbo.TravelCountries(Code),
                    CONSTRAINT FK_TravelVisaCases_VisaType FOREIGN KEY (VisaTypeId) REFERENCES dbo.TravelVisaTypes(Id),
                    CONSTRAINT FK_TravelVisaCases_Agent FOREIGN KEY (AssignedAgentUserId) REFERENCES dbo.Users(Id),
                    CONSTRAINT FK_TravelVisaCases_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branches(Id),
                    CONSTRAINT CK_TravelVisaCases_Status CHECK (Status IN
                        (N'Draft', N'DocumentsPending', N'DocumentsVerified', N'Submitted', N'InProcessing',
                         N'Approved', N'PartiallyApproved', N'Rejected', N'OnHold', N'Cancelled')),
                    CONSTRAINT CK_TravelVisaCases_OnHold CHECK
                        ((Status = N'OnHold' AND OnHoldFromStatus IS NOT NULL) OR (Status <> N'OnHold' AND OnHoldFromStatus IS NULL)),
                    CONSTRAINT CK_TravelVisaCases_Priority CHECK (Priority IN (N'Low', N'Normal', N'High')),
                    CONSTRAINT CK_TravelVisaCases_FeeCurrency CHECK
                        ((QuotedServiceFee IS NULL AND QuotedEmbassyFee IS NULL) OR CurrencyCode IS NOT NULL),
                    CONSTRAINT CK_TravelVisaCases_FeesNonNegative CHECK
                        ((QuotedServiceFee IS NULL OR QuotedServiceFee >= 0) AND (QuotedEmbassyFee IS NULL OR QuotedEmbassyFee >= 0)),
                    CONSTRAINT CK_TravelVisaCases_Cancel CHECK (Status <> N'Cancelled' OR CancelReason IS NOT NULL),
                    CONSTRAINT CK_TravelVisaCases_Submitted CHECK
                        (Status IN (N'Draft', N'DocumentsPending', N'DocumentsVerified', N'Cancelled')
                         OR (Status = N'OnHold' AND OnHoldFromStatus IN (N'Draft', N'DocumentsPending', N'DocumentsVerified'))
                         OR SubmittedAt IS NOT NULL)
                );
                CREATE UNIQUE INDEX UX_TravelVisaCases_Tenant_Number ON dbo.TravelVisaCases (TenantId, CaseNumber);
                CREATE INDEX IX_TravelVisaCases_Tenant_Status ON dbo.TravelVisaCases (TenantId, Status) WHERE IsDeleted = 0;
                CREATE INDEX IX_TravelVisaCases_Tenant_Customer ON dbo.TravelVisaCases (TenantId, CustomerId) WHERE IsDeleted = 0;
                CREATE INDEX IX_TravelVisaCases_Tenant_Created ON dbo.TravelVisaCases (TenantId, CreatedAt) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelVisaApplicants', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelVisaApplicants (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelVisaApplicants PRIMARY KEY,
                    TenantId INT NOT NULL,
                    VisaCaseId INT NOT NULL,
                    TravelerId INT NOT NULL,
                    Decision NVARCHAR(20) NOT NULL CONSTRAINT DF_TravelVisaApplicants_Decision DEFAULT (N'Pending'),
                    VisaNumber NVARCHAR(50) NULL,
                    VisaIssueDate DATE NULL,
                    VisaValidFrom DATE NULL,
                    VisaExpiryDate DATE NULL,
                    RejectionReason NVARCHAR(500) NULL,
                    DecidedAt DATETIME2 NULL,
                    Notes NVARCHAR(1000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelVisaApplicants_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelVisaApplicants_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelVisaApplicants_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelVisaApplicants_Case FOREIGN KEY (VisaCaseId) REFERENCES dbo.TravelVisaCases(Id),
                    CONSTRAINT FK_TravelVisaApplicants_Traveler FOREIGN KEY (TravelerId) REFERENCES dbo.TravelTravelers(Id),
                    CONSTRAINT CK_TravelVisaApplicants_Decision CHECK (Decision IN (N'Pending', N'Approved', N'Rejected', N'Withdrawn')),
                    CONSTRAINT CK_TravelVisaApplicants_Rejected CHECK (Decision <> N'Rejected' OR RejectionReason IS NOT NULL),
                    CONSTRAINT CK_TravelVisaApplicants_VisaDates CHECK
                        (VisaValidFrom IS NULL OR VisaExpiryDate IS NULL OR VisaExpiryDate > VisaValidFrom)
                );
                CREATE UNIQUE INDEX UX_TravelVisaApplicants_Case_Traveler
                    ON dbo.TravelVisaApplicants (TenantId, VisaCaseId, TravelerId) WHERE IsDeleted = 0;
                CREATE INDEX IX_TravelVisaApplicants_Tenant_Traveler
                    ON dbo.TravelVisaApplicants (TenantId, TravelerId) WHERE IsDeleted = 0;
                CREATE INDEX IX_TravelVisaApplicants_Tenant_VisaExpiry
                    ON dbo.TravelVisaApplicants (TenantId, VisaExpiryDate) WHERE IsDeleted = 0 AND VisaExpiryDate IS NOT NULL;
            END

            IF OBJECT_ID(N'dbo.TravelVisaStatusHistory', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelVisaStatusHistory (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelVisaStatusHistory PRIMARY KEY,
                    TenantId INT NOT NULL,
                    VisaCaseId INT NOT NULL,
                    ApplicantId INT NULL,
                    EventType NVARCHAR(20) NOT NULL,
                    FromValue NVARCHAR(30) NULL,
                    ToValue NVARCHAR(30) NULL,
                    Note NVARCHAR(1000) NULL,
                    ChangedBy INT NULL,
                    ChangedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelVisaStatusHistory_At DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT FK_TravelVisaStatusHistory_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelVisaStatusHistory_Case FOREIGN KEY (VisaCaseId) REFERENCES dbo.TravelVisaCases(Id),
                    CONSTRAINT FK_TravelVisaStatusHistory_Applicant FOREIGN KEY (ApplicantId) REFERENCES dbo.TravelVisaApplicants(Id),
                    CONSTRAINT CK_TravelVisaStatusHistory_Event CHECK (EventType IN
                        (N'CaseStatus', N'ApplicantAdded', N'ApplicantRemoved', N'ApplicantDecision'))
                );
                CREATE INDEX IX_TravelVisaStatusHistory_Tenant_Case
                    ON dbo.TravelVisaStatusHistory (TenantId, VisaCaseId, ChangedAt);
            END

            IF OBJECT_ID(N'dbo.FK_TravelEnquiries_VisaCase', N'F') IS NULL
                ALTER TABLE dbo.TravelEnquiries
                    ADD CONSTRAINT FK_TravelEnquiries_VisaCase FOREIGN KEY (ConvertedVisaCaseId) REFERENCES dbo.TravelVisaCases(Id);
            """, cancellationToken: cancellationToken));

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
                connection, role, TravelPermissions.Phase2, cancellationToken);
        }

        // ── ERP menu rows ────────────────────────────────────────────────────────
        var hasMenuTables = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN OBJECT_ID(N'dbo.PlatformMenus', N'U') IS NOT NULL
                         AND OBJECT_ID(N'dbo.PlatformModules', N'U') IS NOT NULL THEN 1 ELSE 0 END
            """, cancellationToken: cancellationToken)) == 1;

        if (hasMenuTables)
        {
            var menus = new (string Name, string Route, string Icon, string Perm, int Sort)[]
            {
                ("CRM & Enquiries", "/travel/enquiries", "contact_phone", TravelPermissions.EnquiryView, 20),
                ("Visa Management", "/travel/visa-cases", "assignment_ind", TravelPermissions.VisaView, 40),
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
                        PlatformModule = "travel-nexus",
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

        logger.LogInformation("TravelNexusCrmVisaMigration applied (enquiries, visa cases, sequences, permissions, menu).");
    }
}
