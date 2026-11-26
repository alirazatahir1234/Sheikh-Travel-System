using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// TravelNexus Phase 6B: exchange rates to a reporting currency, supplier rate cards and commission rules, supplier bills (lines
/// bound once to their booking service), supplier payments and allocations, supplier credits and refunds received, sub-agents,
/// agent commission rules and payouts, invoice commission attribution; prefixes; Phase 6B permissions and menu rows.
/// Additive and idempotent.
/// </summary>
public static class TravelNexusSupplierFinanceMigration
{
    private static readonly (string Code, string Desc)[] PermissionRows =
    [
        (TravelPermissions.SupplierBillManage, "Draft and post supplier bills"),
        (TravelPermissions.SupplierPaymentManage, "Record and allocate supplier payments; record supplier refunds"),
        (TravelPermissions.CommissionView, "View agent commissions and balances"),
        (TravelPermissions.CommissionManage, "Manage commission rules, sub-agents, payouts and attribution"),
        (TravelPermissions.ProfitabilityView, "View margins and profitability"),
    ];

    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // ── Settings, attribution columns, exchange rates, sub-agents ───────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH(N'dbo.TravelSettings', N'ReportingCurrencyCode') IS NULL
                ALTER TABLE dbo.TravelSettings ADD ReportingCurrencyCode CHAR(3) NULL;
            IF COL_LENGTH(N'dbo.TravelSettings', N'SupplierBillPrefix') IS NULL
                ALTER TABLE dbo.TravelSettings ADD SupplierBillPrefix NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelSettings_SupplierBillPrefix DEFAULT (N'TSB');
            IF COL_LENGTH(N'dbo.TravelSettings', N'SupplierPaymentPrefix') IS NULL
                ALTER TABLE dbo.TravelSettings ADD SupplierPaymentPrefix NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelSettings_SupplierPaymentPrefix DEFAULT (N'TSP');
            IF COL_LENGTH(N'dbo.TravelSettings', N'SupplierCreditPrefix') IS NULL
                ALTER TABLE dbo.TravelSettings ADD SupplierCreditPrefix NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelSettings_SupplierCreditPrefix DEFAULT (N'TSC');
            IF COL_LENGTH(N'dbo.TravelSettings', N'SupplierRefundPrefix') IS NULL
                ALTER TABLE dbo.TravelSettings ADD SupplierRefundPrefix NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelSettings_SupplierRefundPrefix DEFAULT (N'TSR');
            IF COL_LENGTH(N'dbo.TravelSettings', N'PayoutPrefix') IS NULL
                ALTER TABLE dbo.TravelSettings ADD PayoutPrefix NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelSettings_PayoutPrefix DEFAULT (N'TCP');

            IF OBJECT_ID(N'dbo.TravelExchangeRates', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelExchangeRates (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelExchangeRates PRIMARY KEY,
                    TenantId INT NOT NULL,
                    CurrencyCode CHAR(3) NOT NULL,
                    RateToReporting DECIMAL(18,6) NOT NULL,
                    EffectiveFrom DATE NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelExchangeRates_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    CONSTRAINT FK_TravelExchangeRates_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT CK_TravelExchangeRates_Rate CHECK (RateToReporting > 0)
                );
                CREATE UNIQUE INDEX UX_TravelExchangeRates_Tenant_Currency_Date ON dbo.TravelExchangeRates (TenantId, CurrencyCode, EffectiveFrom);
            END

            IF OBJECT_ID(N'dbo.TravelSubAgents', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelSubAgents (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelSubAgents PRIMARY KEY,
                    TenantId INT NOT NULL,
                    Name NVARCHAR(150) NOT NULL,
                    Phone NVARCHAR(30) NULL,
                    Email NVARCHAR(200) NULL,
                    Notes NVARCHAR(1000) NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_TravelSubAgents_IsActive DEFAULT (1),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelSubAgents_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelSubAgents_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelSubAgents_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
                );
                CREATE UNIQUE INDEX UX_TravelSubAgents_Tenant_Name ON dbo.TravelSubAgents (TenantId, Name) WHERE IsDeleted = 0;
            END

            IF COL_LENGTH(N'dbo.TravelInvoices', N'AgentUserId') IS NULL
                ALTER TABLE dbo.TravelInvoices ADD AgentUserId INT NULL CONSTRAINT FK_TravelInvoices_Agent REFERENCES dbo.Users(Id);
            IF COL_LENGTH(N'dbo.TravelInvoices', N'SubAgentId') IS NULL
                ALTER TABLE dbo.TravelInvoices ADD SubAgentId INT NULL CONSTRAINT FK_TravelInvoices_SubAgent REFERENCES dbo.TravelSubAgents(Id);
            IF COL_LENGTH(N'dbo.TravelCustomerProfiles', N'SubAgentId') IS NULL
                ALTER TABLE dbo.TravelCustomerProfiles ADD SubAgentId INT NULL CONSTRAINT FK_TravelCustomerProfiles_SubAgent REFERENCES dbo.TravelSubAgents(Id);
            """, cancellationToken: cancellationToken));

        // ── Supplier rate cards and commission rules ─────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelSupplierRates', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelSupplierRates (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelSupplierRates PRIMARY KEY,
                    TenantId INT NOT NULL,
                    SupplierId INT NOT NULL,
                    RateType NVARCHAR(16) NOT NULL,
                    HotelId INT NULL,
                    RoomType NVARCHAR(10) NULL,
                    Description NVARCHAR(200) NULL,
                    ValidFrom DATE NOT NULL,
                    ValidTo DATE NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    CurrencyCode CHAR(3) NOT NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_TravelSupplierRates_IsActive DEFAULT (1),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelSupplierRates_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelSupplierRates_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelSupplierRates_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelSupplierRates_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.TravelSuppliers(Id),
                    CONSTRAINT FK_TravelSupplierRates_Hotel FOREIGN KEY (HotelId) REFERENCES dbo.TravelHotels(Id),
                    CONSTRAINT CK_TravelSupplierRates_Type CHECK (RateType IN (N'HotelRoomNight', N'Transfer', N'Service')),
                    CONSTRAINT CK_TravelSupplierRates_Hotel CHECK (RateType <> N'HotelRoomNight' OR (HotelId IS NOT NULL AND RoomType IS NOT NULL)),
                    CONSTRAINT CK_TravelSupplierRates_Room CHECK (RoomType IS NULL OR RoomType IN (N'Quad', N'Triple', N'Double', N'Single')),
                    CONSTRAINT CK_TravelSupplierRates_Dates CHECK (ValidTo >= ValidFrom),
                    CONSTRAINT CK_TravelSupplierRates_Amount CHECK (Amount >= 0)
                );
                CREATE INDEX IX_TravelSupplierRates_Tenant_Supplier ON dbo.TravelSupplierRates (TenantId, SupplierId) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelSupplierCommissionRules', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelSupplierCommissionRules (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelSupplierCommissionRules PRIMARY KEY,
                    TenantId INT NOT NULL,
                    SupplierId INT NOT NULL,
                    ServiceType NVARCHAR(8) NOT NULL,
                    Basis NVARCHAR(16) NOT NULL,
                    Rate DECIMAL(18,4) NOT NULL,
                    CurrencyCode CHAR(3) NULL,
                    ValidFrom DATE NOT NULL,
                    ValidTo DATE NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_TravelSupplierCommissionRules_IsActive DEFAULT (1),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelSupplierCommissionRules_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelSupplierCommissionRules_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelSupplierCommissionRules_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelSupplierCommissionRules_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.TravelSuppliers(Id),
                    CONSTRAINT CK_TravelSupplierCommissionRules_Service CHECK (ServiceType IN (N'Flight', N'Hotel', N'Other')),
                    CONSTRAINT CK_TravelSupplierCommissionRules_Basis CHECK (Basis IN (N'PercentOfCost', N'FixedPerUnit')),
                    CONSTRAINT CK_TravelSupplierCommissionRules_Rate CHECK (Rate >= 0 AND (Basis <> N'PercentOfCost' OR Rate <= 100)),
                    CONSTRAINT CK_TravelSupplierCommissionRules_Currency CHECK (Basis <> N'FixedPerUnit' OR CurrencyCode IS NOT NULL),
                    CONSTRAINT CK_TravelSupplierCommissionRules_Dates CHECK (ValidTo IS NULL OR ValidTo >= ValidFrom)
                );
                CREATE INDEX IX_TravelSupplierCommissionRules_Tenant_Supplier ON dbo.TravelSupplierCommissionRules (TenantId, SupplierId) WHERE IsDeleted = 0;
            END
            """, cancellationToken: cancellationToken));

        // ── Supplier bills, payments, credits, refunds ───────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelSupplierBills', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelSupplierBills (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelSupplierBills PRIMARY KEY,
                    TenantId INT NOT NULL,
                    BillNumber NVARCHAR(30) NULL,
                    SupplierId INT NOT NULL,
                    SupplierReference NVARCHAR(60) NULL,
                    CurrencyCode CHAR(3) NOT NULL,
                    BillDate DATE NULL,
                    DueDate DATE NULL,
                    Gross DECIMAL(18,2) NOT NULL CONSTRAINT DF_TravelSupplierBills_Gross DEFAULT (0),
                    CommissionTotal DECIMAL(18,2) NOT NULL CONSTRAINT DF_TravelSupplierBills_Commission DEFAULT (0),
                    Net DECIMAL(18,2) NOT NULL CONSTRAINT DF_TravelSupplierBills_Net DEFAULT (0),
                    Status NVARCHAR(8) NOT NULL CONSTRAINT DF_TravelSupplierBills_Status DEFAULT (N'Draft'),
                    PostedAt DATETIME2 NULL,
                    VoidedAt DATETIME2 NULL,
                    VoidReason NVARCHAR(500) NULL,
                    BranchId INT NULL,
                    Notes NVARCHAR(2000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelSupplierBills_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelSupplierBills_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelSupplierBills_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelSupplierBills_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.TravelSuppliers(Id),
                    CONSTRAINT FK_TravelSupplierBills_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branches(Id),
                    CONSTRAINT CK_TravelSupplierBills_Status CHECK (Status IN (N'Draft', N'Posted', N'Void')),
                    CONSTRAINT CK_TravelSupplierBills_Totals CHECK (Gross >= 0 AND CommissionTotal >= 0 AND CommissionTotal <= Gross AND Net = Gross - CommissionTotal),
                    CONSTRAINT CK_TravelSupplierBills_Posted CHECK (Status = N'Draft' OR (BillNumber IS NOT NULL AND BillDate IS NOT NULL AND DueDate IS NOT NULL)),
                    CONSTRAINT CK_TravelSupplierBills_Due CHECK (BillDate IS NULL OR DueDate IS NULL OR DueDate >= BillDate),
                    CONSTRAINT CK_TravelSupplierBills_Void CHECK (Status <> N'Void' OR VoidReason IS NOT NULL)
                );
                CREATE UNIQUE INDEX UX_TravelSupplierBills_Tenant_Number ON dbo.TravelSupplierBills (TenantId, BillNumber) WHERE BillNumber IS NOT NULL;
                -- The same supplier invoice cannot be entered twice.
                CREATE UNIQUE INDEX UX_TravelSupplierBills_Supplier_Reference ON dbo.TravelSupplierBills (TenantId, SupplierId, SupplierReference)
                    WHERE SupplierReference IS NOT NULL AND IsDeleted = 0 AND Status <> N'Void';
                CREATE INDEX IX_TravelSupplierBills_Tenant_Supplier ON dbo.TravelSupplierBills (TenantId, SupplierId) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelSupplierBillLines', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelSupplierBillLines (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelSupplierBillLines PRIMARY KEY,
                    TenantId INT NOT NULL,
                    BillId INT NOT NULL,
                    Sequence INT NOT NULL,
                    Description NVARCHAR(300) NOT NULL,
                    SourceType NVARCHAR(8) NULL,
                    SourceId INT NULL,
                    ContextType NVARCHAR(10) NULL,
                    ContextId INT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    CommissionAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_TravelSupplierBillLines_Commission DEFAULT (0),
                    NetAmount DECIMAL(18,2) NOT NULL,
                    IsVoided BIT NOT NULL CONSTRAINT DF_TravelSupplierBillLines_IsVoided DEFAULT (0),
                    CONSTRAINT FK_TravelSupplierBillLines_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelSupplierBillLines_Bill FOREIGN KEY (BillId) REFERENCES dbo.TravelSupplierBills(Id),
                    CONSTRAINT CK_TravelSupplierBillLines_Amounts CHECK (Amount >= 0 AND CommissionAmount >= 0 AND CommissionAmount <= Amount AND NetAmount = Amount - CommissionAmount),
                    CONSTRAINT CK_TravelSupplierBillLines_Source CHECK ((SourceType IS NULL AND SourceId IS NULL) OR (SourceType IN (N'Flight', N'Hotel') AND SourceId IS NOT NULL)),
                    CONSTRAINT CK_TravelSupplierBillLines_Context CHECK ((ContextType IS NULL AND ContextId IS NULL)
                        OR (ContextType IN (N'Booking', N'VisaCase', N'Group') AND ContextId IS NOT NULL))
                );
                CREATE INDEX IX_TravelSupplierBillLines_Tenant_Bill ON dbo.TravelSupplierBillLines (TenantId, BillId);
                -- A service's cost sits on one live bill only.
                CREATE UNIQUE INDEX UX_TravelSupplierBillLines_Tenant_Source ON dbo.TravelSupplierBillLines (TenantId, SourceType, SourceId)
                    WHERE SourceType IS NOT NULL AND IsVoided = 0;
            END

            IF OBJECT_ID(N'dbo.TravelSupplierPayments', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelSupplierPayments (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelSupplierPayments PRIMARY KEY,
                    TenantId INT NOT NULL,
                    PaymentNumber NVARCHAR(30) NOT NULL,
                    SupplierId INT NOT NULL,
                    PaidOn DATE NOT NULL,
                    Method NVARCHAR(16) NOT NULL,
                    Reference NVARCHAR(100) NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    CurrencyCode CHAR(3) NOT NULL,
                    Status NVARCHAR(12) NOT NULL,
                    IdempotencyKey UNIQUEIDENTIFIER NOT NULL,
                    ClearedAt DATETIME2 NULL,
                    ReconciledAt DATETIME2 NULL,
                    ReconciledBy INT NULL,
                    BankReference NVARCHAR(100) NULL,
                    BranchId INT NULL,
                    Notes NVARCHAR(1000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelSupplierPayments_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelSupplierPayments_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelSupplierPayments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelSupplierPayments_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.TravelSuppliers(Id),
                    CONSTRAINT FK_TravelSupplierPayments_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branches(Id),
                    CONSTRAINT CK_TravelSupplierPayments_Amount CHECK (Amount > 0),
                    CONSTRAINT CK_TravelSupplierPayments_Method CHECK (Method IN (N'Cash', N'BankTransfer', N'Card', N'Cheque', N'Online', N'Other')),
                    CONSTRAINT CK_TravelSupplierPayments_Status CHECK (Status IN (N'Pending', N'Completed', N'Failed', N'Refunded')),
                    CONSTRAINT CK_TravelSupplierPayments_Reconciled CHECK (ReconciledAt IS NULL OR Status IN (N'Completed', N'Refunded'))
                );
                CREATE UNIQUE INDEX UX_TravelSupplierPayments_Tenant_Number ON dbo.TravelSupplierPayments (TenantId, PaymentNumber);
                CREATE UNIQUE INDEX UX_TravelSupplierPayments_Tenant_Idempotency ON dbo.TravelSupplierPayments (TenantId, IdempotencyKey);
                CREATE INDEX IX_TravelSupplierPayments_Tenant_Supplier ON dbo.TravelSupplierPayments (TenantId, SupplierId) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelSupplierPaymentAllocations', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelSupplierPaymentAllocations (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelSupplierPaymentAllocations PRIMARY KEY,
                    TenantId INT NOT NULL,
                    PaymentId INT NOT NULL,
                    BillId INT NOT NULL,
                    AmountFromPayment DECIMAL(18,2) NOT NULL,
                    ExchangeRate DECIMAL(18,6) NOT NULL CONSTRAINT DF_TravelSupplierPaymentAllocations_Rate DEFAULT (1),
                    AmountApplied DECIMAL(18,2) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelSupplierPaymentAllocations_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    CONSTRAINT FK_TravelSupplierPaymentAllocations_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelSupplierPaymentAllocations_Payment FOREIGN KEY (PaymentId) REFERENCES dbo.TravelSupplierPayments(Id),
                    CONSTRAINT FK_TravelSupplierPaymentAllocations_Bill FOREIGN KEY (BillId) REFERENCES dbo.TravelSupplierBills(Id),
                    CONSTRAINT CK_TravelSupplierPaymentAllocations_Amounts CHECK (AmountFromPayment > 0 AND ExchangeRate > 0 AND AmountApplied > 0)
                );
                CREATE UNIQUE INDEX UX_TravelSupplierPaymentAllocations_Payment_Bill ON dbo.TravelSupplierPaymentAllocations (TenantId, PaymentId, BillId);
                CREATE INDEX IX_TravelSupplierPaymentAllocations_Tenant_Bill ON dbo.TravelSupplierPaymentAllocations (TenantId, BillId);
            END

            IF OBJECT_ID(N'dbo.TravelSupplierCredits', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelSupplierCredits (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelSupplierCredits PRIMARY KEY,
                    TenantId INT NOT NULL,
                    CreditNumber NVARCHAR(30) NOT NULL,
                    BillId INT NOT NULL,
                    SupplierId INT NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    CurrencyCode CHAR(3) NOT NULL,
                    Reason NVARCHAR(500) NOT NULL,
                    CancellationId INT NULL,
                    Status NVARCHAR(8) NOT NULL CONSTRAINT DF_TravelSupplierCredits_Status DEFAULT (N'Issued'),
                    IssuedOn DATE NOT NULL,
                    VoidReason NVARCHAR(500) NULL,
                    IdempotencyKey UNIQUEIDENTIFIER NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelSupplierCredits_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    CONSTRAINT FK_TravelSupplierCredits_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelSupplierCredits_Bill FOREIGN KEY (BillId) REFERENCES dbo.TravelSupplierBills(Id),
                    CONSTRAINT FK_TravelSupplierCredits_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.TravelSuppliers(Id),
                    CONSTRAINT FK_TravelSupplierCredits_Cancellation FOREIGN KEY (CancellationId) REFERENCES dbo.TravelBookingCancellations(Id),
                    CONSTRAINT CK_TravelSupplierCredits_Amount CHECK (Amount > 0),
                    CONSTRAINT CK_TravelSupplierCredits_Status CHECK (Status IN (N'Issued', N'Void')),
                    CONSTRAINT CK_TravelSupplierCredits_Void CHECK (Status <> N'Void' OR VoidReason IS NOT NULL)
                );
                CREATE UNIQUE INDEX UX_TravelSupplierCredits_Tenant_Number ON dbo.TravelSupplierCredits (TenantId, CreditNumber);
                CREATE UNIQUE INDEX UX_TravelSupplierCredits_Tenant_Idempotency ON dbo.TravelSupplierCredits (TenantId, IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
                CREATE INDEX IX_TravelSupplierCredits_Tenant_Bill ON dbo.TravelSupplierCredits (TenantId, BillId);
            END

            IF OBJECT_ID(N'dbo.TravelSupplierRefunds', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelSupplierRefunds (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelSupplierRefunds PRIMARY KEY,
                    TenantId INT NOT NULL,
                    RefundNumber NVARCHAR(30) NOT NULL,
                    SupplierId INT NOT NULL,
                    BillId INT NULL,
                    PaymentId INT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    CurrencyCode CHAR(3) NOT NULL,
                    Method NVARCHAR(16) NOT NULL,
                    Reference NVARCHAR(100) NULL,
                    ReceivedOn DATE NOT NULL,
                    CancellationId INT NULL,
                    Notes NVARCHAR(1000) NULL,
                    IdempotencyKey UNIQUEIDENTIFIER NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelSupplierRefunds_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    CONSTRAINT FK_TravelSupplierRefunds_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelSupplierRefunds_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.TravelSuppliers(Id),
                    CONSTRAINT FK_TravelSupplierRefunds_Bill FOREIGN KEY (BillId) REFERENCES dbo.TravelSupplierBills(Id),
                    CONSTRAINT FK_TravelSupplierRefunds_Payment FOREIGN KEY (PaymentId) REFERENCES dbo.TravelSupplierPayments(Id),
                    CONSTRAINT FK_TravelSupplierRefunds_Cancellation FOREIGN KEY (CancellationId) REFERENCES dbo.TravelBookingCancellations(Id),
                    CONSTRAINT CK_TravelSupplierRefunds_Target CHECK ((BillId IS NULL AND PaymentId IS NOT NULL) OR (BillId IS NOT NULL AND PaymentId IS NULL)),
                    CONSTRAINT CK_TravelSupplierRefunds_Amount CHECK (Amount > 0),
                    CONSTRAINT CK_TravelSupplierRefunds_Method CHECK (Method IN (N'Cash', N'BankTransfer', N'Card', N'Cheque', N'Online', N'Other'))
                );
                CREATE UNIQUE INDEX UX_TravelSupplierRefunds_Tenant_Number ON dbo.TravelSupplierRefunds (TenantId, RefundNumber);
                CREATE UNIQUE INDEX UX_TravelSupplierRefunds_Tenant_Idempotency ON dbo.TravelSupplierRefunds (TenantId, IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
            END
            """, cancellationToken: cancellationToken));

        // ── Agent commissions and history ────────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelCommissionRules', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelCommissionRules (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelCommissionRules PRIMARY KEY,
                    TenantId INT NOT NULL,
                    BeneficiaryType NVARCHAR(8) NOT NULL,
                    UserId INT NULL,
                    SubAgentId INT NULL,
                    Scope NVARCHAR(8) NOT NULL CONSTRAINT DF_TravelCommissionRules_Scope DEFAULT (N'All'),
                    Basis NVARCHAR(16) NOT NULL,
                    Rate DECIMAL(9,4) NOT NULL,
                    ValidFrom DATE NOT NULL,
                    ValidTo DATE NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_TravelCommissionRules_IsActive DEFAULT (1),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelCommissionRules_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelCommissionRules_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelCommissionRules_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelCommissionRules_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
                    CONSTRAINT FK_TravelCommissionRules_SubAgent FOREIGN KEY (SubAgentId) REFERENCES dbo.TravelSubAgents(Id),
                    CONSTRAINT CK_TravelCommissionRules_Type CHECK (BeneficiaryType IN (N'Staff', N'SubAgent')),
                    CONSTRAINT CK_TravelCommissionRules_Who CHECK ((BeneficiaryType = N'Staff' AND SubAgentId IS NULL) OR (BeneficiaryType = N'SubAgent' AND UserId IS NULL)),
                    CONSTRAINT CK_TravelCommissionRules_Scope CHECK (Scope IN (N'All', N'Flight', N'Hotel', N'Visa', N'Umrah')),
                    CONSTRAINT CK_TravelCommissionRules_Basis CHECK (Basis IN (N'PercentOfMargin', N'PercentOfSell')),
                    CONSTRAINT CK_TravelCommissionRules_Rate CHECK (Rate >= 0 AND Rate <= 100),
                    CONSTRAINT CK_TravelCommissionRules_Dates CHECK (ValidTo IS NULL OR ValidTo >= ValidFrom)
                );
                CREATE INDEX IX_TravelCommissionRules_Tenant ON dbo.TravelCommissionRules (TenantId, BeneficiaryType) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelCommissionPayouts', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelCommissionPayouts (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelCommissionPayouts PRIMARY KEY,
                    TenantId INT NOT NULL,
                    PayoutNumber NVARCHAR(30) NOT NULL,
                    BeneficiaryType NVARCHAR(8) NOT NULL,
                    UserId INT NULL,
                    SubAgentId INT NULL,
                    PaidOn DATE NOT NULL,
                    Method NVARCHAR(16) NOT NULL,
                    Reference NVARCHAR(100) NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    CurrencyCode CHAR(3) NOT NULL,
                    IdempotencyKey UNIQUEIDENTIFIER NOT NULL,
                    Notes NVARCHAR(1000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelCommissionPayouts_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    CONSTRAINT FK_TravelCommissionPayouts_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelCommissionPayouts_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
                    CONSTRAINT FK_TravelCommissionPayouts_SubAgent FOREIGN KEY (SubAgentId) REFERENCES dbo.TravelSubAgents(Id),
                    CONSTRAINT CK_TravelCommissionPayouts_Who CHECK ((BeneficiaryType = N'Staff' AND UserId IS NOT NULL AND SubAgentId IS NULL)
                        OR (BeneficiaryType = N'SubAgent' AND SubAgentId IS NOT NULL AND UserId IS NULL)),
                    CONSTRAINT CK_TravelCommissionPayouts_Amount CHECK (Amount > 0),
                    CONSTRAINT CK_TravelCommissionPayouts_Method CHECK (Method IN (N'Cash', N'BankTransfer', N'Card', N'Cheque', N'Online', N'Other'))
                );
                CREATE UNIQUE INDEX UX_TravelCommissionPayouts_Tenant_Number ON dbo.TravelCommissionPayouts (TenantId, PayoutNumber);
                CREATE UNIQUE INDEX UX_TravelCommissionPayouts_Tenant_Idempotency ON dbo.TravelCommissionPayouts (TenantId, IdempotencyKey);
            END

            IF OBJECT_ID(N'dbo.TravelSupplierFinanceHistory', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelSupplierFinanceHistory (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelSupplierFinanceHistory PRIMARY KEY,
                    TenantId INT NOT NULL,
                    BillId INT NULL,
                    PaymentId INT NULL,
                    CreditId INT NULL,
                    RefundId INT NULL,
                    PayoutId INT NULL,
                    InvoiceId INT NULL,
                    EventType NVARCHAR(20) NOT NULL,
                    FromValue NVARCHAR(100) NULL,
                    ToValue NVARCHAR(100) NULL,
                    Amount DECIMAL(18,2) NULL,
                    CurrencyCode CHAR(3) NULL,
                    Note NVARCHAR(1000) NULL,
                    ChangedBy INT NULL,
                    ChangedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelSupplierFinanceHistory_At DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT FK_TravelSupplierFinanceHistory_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelSupplierFinanceHistory_Bill FOREIGN KEY (BillId) REFERENCES dbo.TravelSupplierBills(Id),
                    CONSTRAINT FK_TravelSupplierFinanceHistory_Payment FOREIGN KEY (PaymentId) REFERENCES dbo.TravelSupplierPayments(Id),
                    CONSTRAINT FK_TravelSupplierFinanceHistory_Credit FOREIGN KEY (CreditId) REFERENCES dbo.TravelSupplierCredits(Id),
                    CONSTRAINT FK_TravelSupplierFinanceHistory_Refund FOREIGN KEY (RefundId) REFERENCES dbo.TravelSupplierRefunds(Id),
                    CONSTRAINT FK_TravelSupplierFinanceHistory_Payout FOREIGN KEY (PayoutId) REFERENCES dbo.TravelCommissionPayouts(Id),
                    CONSTRAINT FK_TravelSupplierFinanceHistory_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.TravelInvoices(Id),
                    CONSTRAINT CK_TravelSupplierFinanceHistory_Event CHECK (EventType IN (N'BillCreated', N'BillUpdated', N'BillPosted', N'BillVoided',
                        N'PaymentRecorded', N'PaymentStatus', N'Allocated', N'Unallocated', N'CreditIssued', N'CreditVoided', N'RefundReceived',
                        N'Reconciled', N'PayoutRecorded', N'AttributionChanged'))
                );
                CREATE INDEX IX_TravelSupplierFinanceHistory_Tenant_Bill ON dbo.TravelSupplierFinanceHistory (TenantId, BillId, ChangedAt) WHERE BillId IS NOT NULL;
                CREATE INDEX IX_TravelSupplierFinanceHistory_Tenant_Payment ON dbo.TravelSupplierFinanceHistory (TenantId, PaymentId, ChangedAt) WHERE PaymentId IS NOT NULL;
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
                connection, role, TravelPermissions.Phase6B, cancellationToken);
        }

        var hasMenuTables = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN OBJECT_ID(N'dbo.PlatformMenus', N'U') IS NOT NULL
                         AND OBJECT_ID(N'dbo.PlatformModules', N'U') IS NOT NULL THEN 1 ELSE 0 END
            """, cancellationToken: cancellationToken)) == 1;
        if (hasMenuTables)
        {
            var menus = new (string Name, string Route, string Icon, string Perm, int Sort)[]
            {
                ("Commissions", "/travel/commissions", "percent", TravelPermissions.CommissionView, 110),
                ("Profitability", "/travel/profitability", "trending_up", TravelPermissions.ProfitabilityView, 120),
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
                        DisplayName, Description, Category, Visible, FeatureKey, ModuleKey, IsMobileSupported, UpdatedAt)
                    SELECT mod.Id, NULL, @Name, @Route, @Icon, @Perm, @Sort, 1,
                           @Name, NULL, N'Travel', 1, @FeatureKey, @ModuleCode, 0, SYSUTCDATETIME()
                    FROM PlatformModules mod
                    WHERE mod.ModuleKey = @PlatformModule
                      AND NOT EXISTS (SELECT 1 FROM PlatformMenus x WHERE x.Route = @Route);
                    """,
                    new
                    {
                        PlatformModule = "travel-nexus", m.Name, m.Route, m.Icon, m.Perm, m.Sort,
                        ModuleCode = TenantModuleCatalog.TravelAgencyModuleCode,
                        FeatureKey = TenantModuleCatalog.TravelAgencyLegacyKey
                    }, cancellationToken: cancellationToken));
            }
        }

        logger.LogInformation("TravelNexusSupplierFinanceMigration applied (rates, supplier bills and payments, commissions, exchange rates, permissions).");
    }
}
