using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// TravelNexus Phase 6A: customer finance — invoices (lines bound once to their source, installments), receipts with
/// cross-currency allocations, credit notes, refunds and finance history; numbering prefixes and payment terms; Phase 6A
/// permissions and the Invoices &amp; Payments menu row. Additive and idempotent. This is the travel ledger; fleet
/// <c>CustomerInvoices</c>/<c>Payments</c> are not touched.
/// </summary>
public static class TravelNexusFinanceMigration
{
    private static readonly (string Code, string Desc)[] PermissionRows =
    [
        (TravelPermissions.FinanceView, "View travel invoices, payments, balances and statements"),
        (TravelPermissions.InvoiceManage, "Create, edit and issue travel invoices"),
        (TravelPermissions.PaymentManage, "Record and allocate customer payments; clear or fail pending payments"),
        (TravelPermissions.FinanceAdjust, "Void invoices, issue credit notes and record refunds"),
        (TravelPermissions.FinanceReconcile, "Reconcile customer payments against the bank"),
    ];

    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // ── Settings and invoices ────────────────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH(N'dbo.TravelSettings', N'ReceiptPrefix') IS NULL
                ALTER TABLE dbo.TravelSettings ADD ReceiptPrefix NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelSettings_ReceiptPrefix DEFAULT (N'TRCP');
            IF COL_LENGTH(N'dbo.TravelSettings', N'CreditNotePrefix') IS NULL
                ALTER TABLE dbo.TravelSettings ADD CreditNotePrefix NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelSettings_CreditNotePrefix DEFAULT (N'TCN');
            IF COL_LENGTH(N'dbo.TravelSettings', N'RefundPrefix') IS NULL
                ALTER TABLE dbo.TravelSettings ADD RefundPrefix NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelSettings_RefundPrefix DEFAULT (N'TRF');
            IF COL_LENGTH(N'dbo.TravelSettings', N'DefaultPaymentTermsDays') IS NULL
                ALTER TABLE dbo.TravelSettings ADD DefaultPaymentTermsDays INT NOT NULL CONSTRAINT DF_TravelSettings_PaymentTerms DEFAULT (7);

            IF OBJECT_ID(N'dbo.TravelInvoices', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelInvoices (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelInvoices PRIMARY KEY,
                    TenantId INT NOT NULL,
                    InvoiceNumber NVARCHAR(30) NULL,
                    CustomerId INT NOT NULL,
                    BookingId INT NULL,
                    VisaCaseId INT NULL,
                    GroupId INT NULL,
                    CurrencyCode CHAR(3) NOT NULL,
                    IssueDate DATE NULL,
                    DueDate DATE NULL,
                    Subtotal DECIMAL(18,2) NOT NULL CONSTRAINT DF_TravelInvoices_Subtotal DEFAULT (0),
                    DiscountAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_TravelInvoices_Discount DEFAULT (0),
                    Total DECIMAL(18,2) NOT NULL CONSTRAINT DF_TravelInvoices_Total DEFAULT (0),
                    Status NVARCHAR(10) NOT NULL CONSTRAINT DF_TravelInvoices_Status DEFAULT (N'Draft'),
                    IssuedAt DATETIME2 NULL,
                    VoidedAt DATETIME2 NULL,
                    VoidReason NVARCHAR(500) NULL,
                    BranchId INT NULL,
                    Notes NVARCHAR(2000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelInvoices_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelInvoices_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelInvoices_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelInvoices_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
                    CONSTRAINT FK_TravelInvoices_Booking FOREIGN KEY (BookingId) REFERENCES dbo.TravelBookings(Id),
                    CONSTRAINT FK_TravelInvoices_VisaCase FOREIGN KEY (VisaCaseId) REFERENCES dbo.TravelVisaCases(Id),
                    CONSTRAINT FK_TravelInvoices_Group FOREIGN KEY (GroupId) REFERENCES dbo.TravelGroups(Id),
                    CONSTRAINT FK_TravelInvoices_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branches(Id),
                    CONSTRAINT CK_TravelInvoices_Status CHECK (Status IN (N'Draft', N'Issued', N'Void')),
                    CONSTRAINT CK_TravelInvoices_Totals CHECK (Subtotal >= 0 AND DiscountAmount >= 0 AND Total = Subtotal - DiscountAmount AND Total >= 0),
                    CONSTRAINT CK_TravelInvoices_Issued CHECK (Status = N'Draft' OR (InvoiceNumber IS NOT NULL AND IssueDate IS NOT NULL AND DueDate IS NOT NULL)),
                    CONSTRAINT CK_TravelInvoices_Due CHECK (IssueDate IS NULL OR DueDate IS NULL OR DueDate >= IssueDate),
                    CONSTRAINT CK_TravelInvoices_Void CHECK (Status <> N'Void' OR VoidReason IS NOT NULL)
                );
                CREATE UNIQUE INDEX UX_TravelInvoices_Tenant_Number ON dbo.TravelInvoices (TenantId, InvoiceNumber) WHERE InvoiceNumber IS NOT NULL;
                CREATE INDEX IX_TravelInvoices_Tenant_Customer ON dbo.TravelInvoices (TenantId, CustomerId) WHERE IsDeleted = 0;
                CREATE INDEX IX_TravelInvoices_Tenant_Status_Due ON dbo.TravelInvoices (TenantId, Status, DueDate) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelInvoiceLines', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelInvoiceLines (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelInvoiceLines PRIMARY KEY,
                    TenantId INT NOT NULL,
                    InvoiceId INT NOT NULL,
                    Sequence INT NOT NULL,
                    Description NVARCHAR(300) NOT NULL,
                    Quantity DECIMAL(10,2) NOT NULL CONSTRAINT DF_TravelInvoiceLines_Qty DEFAULT (1),
                    UnitPrice DECIMAL(18,2) NOT NULL,
                    LineTotal DECIMAL(18,2) NOT NULL,
                    SourceType NVARCHAR(20) NULL,
                    SourceId INT NULL,
                    IsVoided BIT NOT NULL CONSTRAINT DF_TravelInvoiceLines_IsVoided DEFAULT (0),
                    CONSTRAINT FK_TravelInvoiceLines_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelInvoiceLines_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.TravelInvoices(Id),
                    CONSTRAINT CK_TravelInvoiceLines_Amounts CHECK (Quantity > 0 AND UnitPrice >= 0 AND LineTotal >= 0),
                    CONSTRAINT CK_TravelInvoiceLines_Source CHECK ((SourceType IS NULL AND SourceId IS NULL) OR (SourceType IS NOT NULL AND SourceId IS NOT NULL)),
                    CONSTRAINT CK_TravelInvoiceLines_SourceType CHECK (SourceType IS NULL OR SourceType IN
                        (N'Flight', N'Hotel', N'VisaServiceFee', N'VisaEmbassyFee', N'GroupMember'))
                );
                CREATE INDEX IX_TravelInvoiceLines_Tenant_Invoice ON dbo.TravelInvoiceLines (TenantId, InvoiceId);
                -- A sold item can sit on one live invoice only (no double billing).
                CREATE UNIQUE INDEX UX_TravelInvoiceLines_Tenant_Source ON dbo.TravelInvoiceLines (TenantId, SourceType, SourceId)
                    WHERE SourceType IS NOT NULL AND IsVoided = 0;
            END

            IF OBJECT_ID(N'dbo.TravelInvoiceInstallments', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelInvoiceInstallments (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelInvoiceInstallments PRIMARY KEY,
                    TenantId INT NOT NULL,
                    InvoiceId INT NOT NULL,
                    Sequence INT NOT NULL,
                    DueDate DATE NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    CONSTRAINT FK_TravelInvoiceInstallments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelInvoiceInstallments_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.TravelInvoices(Id),
                    CONSTRAINT CK_TravelInvoiceInstallments_Amount CHECK (Amount > 0)
                );
                CREATE UNIQUE INDEX UX_TravelInvoiceInstallments_Invoice_Seq ON dbo.TravelInvoiceInstallments (TenantId, InvoiceId, Sequence);
            END
            """, cancellationToken: cancellationToken));

        // ── Payments, allocations, credit notes, refunds, history ───────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TravelPayments', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelPayments (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelPayments PRIMARY KEY,
                    TenantId INT NOT NULL,
                    ReceiptNumber NVARCHAR(30) NOT NULL,
                    CustomerId INT NOT NULL,
                    ReceivedOn DATE NOT NULL,
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
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelPayments_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_TravelPayments_IsDeleted DEFAULT (0),
                    CONSTRAINT FK_TravelPayments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelPayments_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
                    CONSTRAINT FK_TravelPayments_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branches(Id),
                    CONSTRAINT CK_TravelPayments_Amount CHECK (Amount > 0),
                    CONSTRAINT CK_TravelPayments_Method CHECK (Method IN (N'Cash', N'BankTransfer', N'Card', N'Cheque', N'Online', N'Other')),
                    CONSTRAINT CK_TravelPayments_Status CHECK (Status IN (N'Pending', N'Completed', N'Failed', N'Refunded')),
                    CONSTRAINT CK_TravelPayments_Reconciled CHECK (ReconciledAt IS NULL OR Status IN (N'Completed', N'Refunded'))
                );
                CREATE UNIQUE INDEX UX_TravelPayments_Tenant_Receipt ON dbo.TravelPayments (TenantId, ReceiptNumber);
                -- A resubmitted receipt (double click, retry) maps to the original row.
                CREATE UNIQUE INDEX UX_TravelPayments_Tenant_Idempotency ON dbo.TravelPayments (TenantId, IdempotencyKey);
                CREATE INDEX IX_TravelPayments_Tenant_Customer ON dbo.TravelPayments (TenantId, CustomerId) WHERE IsDeleted = 0;
                CREATE INDEX IX_TravelPayments_Tenant_Received ON dbo.TravelPayments (TenantId, ReceivedOn) WHERE IsDeleted = 0;
            END

            IF OBJECT_ID(N'dbo.TravelPaymentAllocations', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelPaymentAllocations (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelPaymentAllocations PRIMARY KEY,
                    TenantId INT NOT NULL,
                    PaymentId INT NOT NULL,
                    InvoiceId INT NOT NULL,
                    AmountFromPayment DECIMAL(18,2) NOT NULL,
                    ExchangeRate DECIMAL(18,6) NOT NULL CONSTRAINT DF_TravelPaymentAllocations_Rate DEFAULT (1),
                    AmountApplied DECIMAL(18,2) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelPaymentAllocations_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    CONSTRAINT FK_TravelPaymentAllocations_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelPaymentAllocations_Payment FOREIGN KEY (PaymentId) REFERENCES dbo.TravelPayments(Id),
                    CONSTRAINT FK_TravelPaymentAllocations_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.TravelInvoices(Id),
                    CONSTRAINT CK_TravelPaymentAllocations_Amounts CHECK (AmountFromPayment > 0 AND ExchangeRate > 0 AND AmountApplied > 0)
                );
                CREATE UNIQUE INDEX UX_TravelPaymentAllocations_Payment_Invoice ON dbo.TravelPaymentAllocations (TenantId, PaymentId, InvoiceId);
                CREATE INDEX IX_TravelPaymentAllocations_Tenant_Invoice ON dbo.TravelPaymentAllocations (TenantId, InvoiceId);
            END

            IF OBJECT_ID(N'dbo.TravelCreditNotes', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelCreditNotes (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelCreditNotes PRIMARY KEY,
                    TenantId INT NOT NULL,
                    CreditNoteNumber NVARCHAR(30) NOT NULL,
                    InvoiceId INT NOT NULL,
                    CustomerId INT NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    CurrencyCode CHAR(3) NOT NULL,
                    Reason NVARCHAR(500) NOT NULL,
                    CancellationId INT NULL,
                    Status NVARCHAR(8) NOT NULL CONSTRAINT DF_TravelCreditNotes_Status DEFAULT (N'Issued'),
                    IssuedOn DATE NOT NULL,
                    VoidReason NVARCHAR(500) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelCreditNotes_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    CONSTRAINT FK_TravelCreditNotes_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelCreditNotes_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.TravelInvoices(Id),
                    CONSTRAINT FK_TravelCreditNotes_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
                    CONSTRAINT FK_TravelCreditNotes_Cancellation FOREIGN KEY (CancellationId) REFERENCES dbo.TravelBookingCancellations(Id),
                    CONSTRAINT CK_TravelCreditNotes_Amount CHECK (Amount > 0),
                    CONSTRAINT CK_TravelCreditNotes_Status CHECK (Status IN (N'Issued', N'Void')),
                    CONSTRAINT CK_TravelCreditNotes_Void CHECK (Status <> N'Void' OR VoidReason IS NOT NULL)
                );
                CREATE UNIQUE INDEX UX_TravelCreditNotes_Tenant_Number ON dbo.TravelCreditNotes (TenantId, CreditNoteNumber);
                CREATE INDEX IX_TravelCreditNotes_Tenant_Invoice ON dbo.TravelCreditNotes (TenantId, InvoiceId);
            END

            IF OBJECT_ID(N'dbo.TravelRefunds', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelRefunds (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelRefunds PRIMARY KEY,
                    TenantId INT NOT NULL,
                    RefundNumber NVARCHAR(30) NOT NULL,
                    CustomerId INT NOT NULL,
                    InvoiceId INT NULL,
                    PaymentId INT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    CurrencyCode CHAR(3) NOT NULL,
                    Method NVARCHAR(16) NOT NULL,
                    Reference NVARCHAR(100) NULL,
                    PaidOn DATE NOT NULL,
                    CancellationId INT NULL,
                    Notes NVARCHAR(1000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelRefunds_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    CONSTRAINT FK_TravelRefunds_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelRefunds_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
                    CONSTRAINT FK_TravelRefunds_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.TravelInvoices(Id),
                    CONSTRAINT FK_TravelRefunds_Payment FOREIGN KEY (PaymentId) REFERENCES dbo.TravelPayments(Id),
                    CONSTRAINT FK_TravelRefunds_Cancellation FOREIGN KEY (CancellationId) REFERENCES dbo.TravelBookingCancellations(Id),
                    CONSTRAINT CK_TravelRefunds_Target CHECK ((InvoiceId IS NULL AND PaymentId IS NOT NULL) OR (InvoiceId IS NOT NULL AND PaymentId IS NULL)),
                    CONSTRAINT CK_TravelRefunds_Amount CHECK (Amount > 0),
                    CONSTRAINT CK_TravelRefunds_Method CHECK (Method IN (N'Cash', N'BankTransfer', N'Card', N'Cheque', N'Online', N'Other'))
                );
                CREATE UNIQUE INDEX UX_TravelRefunds_Tenant_Number ON dbo.TravelRefunds (TenantId, RefundNumber);
                CREATE INDEX IX_TravelRefunds_Tenant_Invoice ON dbo.TravelRefunds (TenantId, InvoiceId) WHERE InvoiceId IS NOT NULL;
                CREATE INDEX IX_TravelRefunds_Tenant_Payment ON dbo.TravelRefunds (TenantId, PaymentId) WHERE PaymentId IS NOT NULL;
            END

            IF OBJECT_ID(N'dbo.TravelFinanceHistory', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelFinanceHistory (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelFinanceHistory PRIMARY KEY,
                    TenantId INT NOT NULL,
                    InvoiceId INT NULL,
                    PaymentId INT NULL,
                    CreditNoteId INT NULL,
                    RefundId INT NULL,
                    EventType NVARCHAR(20) NOT NULL,
                    FromValue NVARCHAR(100) NULL,
                    ToValue NVARCHAR(100) NULL,
                    Amount DECIMAL(18,2) NULL,
                    CurrencyCode CHAR(3) NULL,
                    Note NVARCHAR(1000) NULL,
                    ChangedBy INT NULL,
                    ChangedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelFinanceHistory_At DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT FK_TravelFinanceHistory_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT FK_TravelFinanceHistory_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.TravelInvoices(Id),
                    CONSTRAINT FK_TravelFinanceHistory_Payment FOREIGN KEY (PaymentId) REFERENCES dbo.TravelPayments(Id),
                    CONSTRAINT FK_TravelFinanceHistory_CreditNote FOREIGN KEY (CreditNoteId) REFERENCES dbo.TravelCreditNotes(Id),
                    CONSTRAINT FK_TravelFinanceHistory_Refund FOREIGN KEY (RefundId) REFERENCES dbo.TravelRefunds(Id),
                    CONSTRAINT CK_TravelFinanceHistory_Event CHECK (EventType IN (N'InvoiceCreated', N'InvoiceUpdated', N'InvoiceIssued', N'InvoiceVoided',
                        N'PaymentRecorded', N'PaymentStatus', N'Allocated', N'Unallocated', N'CreditNoteIssued', N'CreditNoteVoided', N'RefundPaid', N'Reconciled'))
                );
                CREATE INDEX IX_TravelFinanceHistory_Tenant_Invoice ON dbo.TravelFinanceHistory (TenantId, InvoiceId, ChangedAt) WHERE InvoiceId IS NOT NULL;
                CREATE INDEX IX_TravelFinanceHistory_Tenant_Payment ON dbo.TravelFinanceHistory (TenantId, PaymentId, ChangedAt) WHERE PaymentId IS NOT NULL;
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
                connection, role, TravelPermissions.Phase6, cancellationToken);
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
                    PlatformModule = "travel-nexus", Name = "Invoices & Payments", Route = "/travel/finance", Icon = "receipt_long",
                    Perm = TravelPermissions.FinanceView, Sort = 100,
                    ModuleCode = TenantModuleCatalog.TravelAgencyModuleCode,
                    FeatureKey = TenantModuleCatalog.TravelAgencyLegacyKey
                }, cancellationToken: cancellationToken));
        }

        logger.LogInformation("TravelNexusFinanceMigration applied (invoices, installments, payments, allocations, credit notes, refunds, permissions).");
    }
}
