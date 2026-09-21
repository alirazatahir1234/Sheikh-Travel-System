using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class PlatformRepository
{
    public async Task<CompanyLicenseDto?> LoadCompanyLicenseAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition("""
            SELECT
                t.Id AS TenantId,
                t.Name AS CompanyName,
                COALESCE(s.SubscriptionCode, sp.SubscriptionCode) AS SubscriptionCode,
                COALESCE(s.PlanName, t.SubscriptionPlan, sp.DisplayName) AS PlanName,
                COALESCE(sp.DisplayName, s.PlanName, t.SubscriptionPlan) AS PlanDisplayName,
                COALESCE(s.Status, N'Active') AS Status,
                s.SubscriptionStartDate AS StartDate,
                s.SubscriptionEndDate AS EndDate,
                COALESCE(s.AutoRenew, 1) AS AutoRenew,
                COALESCE(s.MaxUsers, sp.MaxUsers) AS MaxUsers,
                COALESCE(s.MaxDrivers, sp.MaxDrivers) AS MaxDrivers,
                COALESCE(s.MaxVehicles, sp.MaxVehicles) AS MaxVehicles,
                COALESCE(s.MaxBranches, sp.MaxBranches) AS MaxBranches,
                COALESCE(s.MaxGpsDevices, sp.MaxGpsDevices) AS MaxGpsDevices,
                COALESCE(s.StorageQuotaGb, sp.StorageQuotaGb) AS StorageQuotaGb,
                COALESCE(s.AICredits, sp.AICredits) AS AICredits,
                COALESCE(s.GPSEnabled, sp.GPSEnabled, 1) AS GPSEnabled,
                sp.DefaultModuleCodesJson,
                (SELECT COUNT(*) FROM Users u WHERE u.TenantId = t.Id AND u.IsDeleted = 0 AND u.IsActive = 1) AS UsedUsers,
                (SELECT COUNT(*) FROM Drivers d WHERE d.TenantId = t.Id AND d.IsDeleted = 0) AS UsedDrivers,
                (SELECT COUNT(*) FROM Vehicles v WHERE v.TenantId = t.Id AND v.IsDeleted = 0) AS UsedVehicles,
                (SELECT COUNT(*) FROM Branches b WHERE b.TenantId = t.Id) AS UsedBranches,
                (SELECT COUNT(*) FROM GpsDevices g
                 INNER JOIN Vehicles v ON v.Id = g.VehicleId AND v.TenantId = t.Id AND v.IsDeleted = 0
                 WHERE g.IsDeleted = 0) AS UsedGpsDevices
            FROM Tenants t
            LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
            LEFT JOIN SubscriptionPlans sp ON sp.SubscriptionCode = COALESCE(
                s.SubscriptionCode,
                CASE
                    WHEN UPPER(COALESCE(s.PlanName, t.SubscriptionPlan, N'')) LIKE N'%ENTERPRISE%' THEN N'ENTERPRISE'
                    WHEN UPPER(COALESCE(s.PlanName, t.SubscriptionPlan, N'')) LIKE N'%PRO%' THEN N'PRO'
                    WHEN UPPER(COALESCE(s.PlanName, t.SubscriptionPlan, N'')) LIKE N'%STARTER%' THEN N'STARTER'
                    ELSE N'ENTERPRISE'
                END)
            WHERE t.Id = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row is null) return null;

        var planName = (string?)row.PlanName;
        var subscriptionCode = (string?)row.SubscriptionCode;
        var licensedFromDb = SubscriptionPlanCatalog.ParseModuleCodes((string?)row.DefaultModuleCodesJson);
        var licensed = licensedFromDb.Count > 0
            ? licensedFromDb
            : SubscriptionPlanCatalog.LicensedModuleCodes(subscriptionCode ?? planName);

        var installed = (await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT m.ModuleCode
            FROM TenantModules tm
            INNER JOIN Modules m ON m.Id = tm.ModuleId
            WHERE tm.TenantId = @TenantId
            ORDER BY m.ModuleCode
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

        return new CompanyLicenseDto(
            CompanyId: (int)row.TenantId,
            TenantId: (int)row.TenantId,
            CompanyName: (string)row.CompanyName,
            SubscriptionCode: subscriptionCode,
            PlanName: planName,
            PlanDisplayName: (string?)row.PlanDisplayName,
            Status: (string)row.Status,
            StartDate: (DateTime?)row.StartDate,
            EndDate: (DateTime?)row.EndDate,
            AutoRenew: (bool)row.AutoRenew,
            LicensedModules: licensed.ToList(),
            InstalledModules: installed,
            MaxUsers: (int?)row.MaxUsers,
            MaxDrivers: (int?)row.MaxDrivers,
            MaxVehicles: (int?)row.MaxVehicles,
            MaxBranches: (int?)row.MaxBranches,
            MaxGpsDevices: (int?)row.MaxGpsDevices,
            StorageQuotaGb: (int?)row.StorageQuotaGb,
            AICredits: (int?)row.AICredits,
            GPSEnabled: (bool)row.GPSEnabled,
            UsedUsers: (int)row.UsedUsers,
            UsedDrivers: (int)row.UsedDrivers,
            UsedVehicles: (int)row.UsedVehicles,
            UsedBranches: (int)row.UsedBranches,
            UsedGpsDevices: (int)row.UsedGpsDevices);
    }

    public async Task<string?> ResolvePlanNameAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            SELECT COALESCE(s.PlanName, t.SubscriptionPlan, s.SubscriptionCode)
            FROM Tenants t
            LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
            WHERE t.Id = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> LoadSubscriptionPlansFromDbAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = dbFactory.CreateConnection();
            var rows = (await connection.QueryAsync<(
                string SubscriptionCode, string DisplayName, string? Description, string PlanType, string Status,
                int SortOrder, int? DurationMonths, bool IsDefault, bool Visible, string? DocumentationUrl,
                string? DefaultModuleCodesJson, int? MaxUsers, int? MaxVehicles, int? MaxDrivers,
                int? MaxBranches, int? MaxGpsDevices, int? StorageQuotaGb, int? AICredits, bool GPSEnabled)>(
                new CommandDefinition("""
                        SELECT SubscriptionCode, DisplayName, Description, PlanType, Status, SortOrder,
                               DurationMonths, IsDefault, Visible, DocumentationUrl, DefaultModuleCodesJson,
                               MaxUsers, MaxVehicles, MaxDrivers, MaxBranches, MaxGpsDevices,
                               StorageQuotaGb, AICredits, GPSEnabled
                        FROM SubscriptionPlans
                        WHERE Visible = 1
                        ORDER BY SortOrder, DisplayName
                        """, cancellationToken: cancellationToken))).ToList();

            return rows.Select(r => new SubscriptionPlanDto(
                r.SubscriptionCode,
                r.DisplayName,
                r.Description,
                r.PlanType,
                r.Status,
                r.SortOrder,
                r.DurationMonths,
                r.IsDefault,
                r.Visible,
                r.DocumentationUrl,
                SubscriptionPlanCatalog.ParseModuleCodes(r.DefaultModuleCodesJson),
                r.MaxUsers,
                r.MaxVehicles,
                r.MaxDrivers,
                r.MaxBranches,
                r.MaxGpsDevices,
                r.StorageQuotaGb,
                r.AICredits,
                r.GPSEnabled)).ToList();
        }
        catch
        {
            // Table may not exist yet.
            return Array.Empty<SubscriptionPlanDto>();
        }
    }

    public async Task<SubscriptionDetailDto?> GetSubscriptionDetailAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleOrDefaultAsync<SubscriptionDetailDto>(new CommandDefinition("""
                SELECT
                    t.Id AS TenantId,
                    t.Name AS TenantName,
                    COALESCE(s.PlanName, t.SubscriptionPlan) AS PlanName,
                    s.SubscriptionCode,
                    COALESCE(s.Status, N'Active') AS Status,
                    COALESCE(s.BillingCycle, N'Monthly') AS BillingCycle,
                    s.MonthlyAmount,
                    COALESCE(s.CurrencyCode, N'PKR') AS CurrencyCode,
                    COALESCE(s.AutoRenew, 1) AS AutoRenew,
                    s.SubscriptionStartDate,
                    s.SubscriptionEndDate,
                    s.TrialEndDate,
                    s.MaxUsers, s.MaxVehicles, s.MaxDrivers, s.MaxBranches, s.MaxGpsDevices,
                    s.StorageQuotaGb, s.AICredits, COALESCE(s.GPSEnabled, 1) AS GPSEnabled
                FROM Tenants t
                LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
                WHERE t.Id = @TenantId
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        }
        catch
        {
            return await connection.QuerySingleOrDefaultAsync<SubscriptionDetailDto>(new CommandDefinition("""
                SELECT
                    t.Id AS TenantId,
                    t.Name AS TenantName,
                    COALESCE(s.PlanName, t.SubscriptionPlan) AS PlanName,
                    COALESCE(s.Status, N'Active') AS Status,
                    COALESCE(s.BillingCycle, N'Monthly') AS BillingCycle,
                    s.MonthlyAmount,
                    COALESCE(s.CurrencyCode, N'PKR') AS CurrencyCode,
                    COALESCE(s.AutoRenew, 1) AS AutoRenew,
                    s.SubscriptionStartDate,
                    s.SubscriptionEndDate,
                    s.TrialEndDate,
                    s.MaxUsers, s.MaxVehicles, s.MaxDrivers, s.MaxBranches, s.MaxGpsDevices
                FROM Tenants t
                LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
                WHERE t.Id = @TenantId
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        }
    }

    public async Task<IReadOnlyList<InvoiceDto>> GetTenantInvoicesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var invoices = (await connection.QueryAsync<InvoiceDto>(new CommandDefinition("""
            SELECT Id, InvoiceNumber, PlanName, Amount, CurrencyCode, Status, IssuedDate, DueDate, PaidDate
            FROM TenantInvoices
            WHERE TenantId = @TenantId
            ORDER BY IssuedDate DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
        return invoices;
    }

    public async Task<IReadOnlyList<PaymentDto>> GetTenantBillingHistoryAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var payments = (await connection.QueryAsync<PaymentDto>(new CommandDefinition("""
            SELECT Id, InvoiceId, Amount, CurrencyCode, PaymentMethod, Status, Reference, PaidAt
            FROM TenantBillingHistory
            WHERE TenantId = @TenantId
            ORDER BY PaidAt DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
        return payments;
    }

    public async Task ApplySubscriptionActionAsync(
        int tenantId, SubscriptionAction action, string? planName, decimal? monthlyAmount, string? billingCycle, bool? autoRenew,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await EnsureSubscriptionRowAsync(connection, tenantId, cancellationToken);

        switch (action)
        {
            case SubscriptionAction.Upgrade:
            {
                var plan = SubscriptionPlanCatalog.Resolve(planName);
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE TenantSubscriptions
                    SET PlanName = COALESCE(@PlanName, PlanName),
                        SubscriptionCode = @SubscriptionCode,
                        MonthlyAmount = COALESCE(@MonthlyAmount, MonthlyAmount),
                        BillingCycle = COALESCE(@BillingCycle, BillingCycle),
                        MaxUsers = @MaxUsers,
                        MaxVehicles = @MaxVehicles,
                        MaxDrivers = @MaxDrivers,
                        MaxBranches = @MaxBranches,
                        MaxGpsDevices = @MaxGpsDevices,
                        StorageQuotaGb = @StorageQuotaGb,
                        AICredits = @AICredits,
                        GPSEnabled = @GPSEnabled,
                        Status = N'Active',
                        UpdatedAt = GETUTCDATE()
                    WHERE TenantId = @TenantId;
                    """, new
                {
                    TenantId = tenantId,
                    PlanName = planName ?? plan.DisplayName,
                    SubscriptionCode = plan.SubscriptionCode,
                    MonthlyAmount = monthlyAmount,
                    BillingCycle = billingCycle,
                    plan.MaxUsers,
                    plan.MaxVehicles,
                    plan.MaxDrivers,
                    plan.MaxBranches,
                    plan.MaxGpsDevices,
                    plan.StorageQuotaGb,
                    plan.AICredits,
                    plan.GPSEnabled
                }, cancellationToken: cancellationToken));
                await SyncTenantPlanAsync(connection, tenantId, planName ?? plan.DisplayName, cancellationToken);
                break;
            }

            case SubscriptionAction.Renew:
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE TenantSubscriptions
                    SET SubscriptionEndDate = DATEADD(year, 1,
                            CASE WHEN SubscriptionEndDate IS NULL OR SubscriptionEndDate < GETUTCDATE()
                                 THEN GETUTCDATE() ELSE SubscriptionEndDate END),
                        Status = N'Active',
                        UpdatedAt = GETUTCDATE()
                    WHERE TenantId = @TenantId;
                    """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
                await GenerateRenewalInvoiceAsync(connection, tenantId, cancellationToken);
                break;

            case SubscriptionAction.Suspend:
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE TenantSubscriptions SET Status = N'Suspended', UpdatedAt = GETUTCDATE() WHERE TenantId = @TenantId",
                    new { TenantId = tenantId }, cancellationToken: cancellationToken));
                break;

            case SubscriptionAction.Cancel:
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE TenantSubscriptions SET Status = N'Cancelled', AutoRenew = 0, UpdatedAt = GETUTCDATE() WHERE TenantId = @TenantId",
                    new { TenantId = tenantId }, cancellationToken: cancellationToken));
                break;

            case SubscriptionAction.Reactivate:
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE TenantSubscriptions SET Status = N'Active', UpdatedAt = GETUTCDATE() WHERE TenantId = @TenantId",
                    new { TenantId = tenantId }, cancellationToken: cancellationToken));
                break;
        }

        if (autoRenew.HasValue)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE TenantSubscriptions SET AutoRenew = @AutoRenew, UpdatedAt = GETUTCDATE() WHERE TenantId = @TenantId",
                new { TenantId = tenantId, AutoRenew = autoRenew.Value }, cancellationToken: cancellationToken));
        }
    }

    private static async Task EnsureSubscriptionRowAsync(System.Data.IDbConnection connection, int tenantId, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM TenantSubscriptions WHERE TenantId = @TenantId)
            INSERT INTO TenantSubscriptions (TenantId, PlanName, Status, SubscriptionStartDate, SubscriptionEndDate, AutoRenew, BillingCycle, CurrencyCode, MonthlyAmount)
            SELECT @TenantId, COALESCE(t.SubscriptionPlan, N'Enterprise'), N'Active', GETUTCDATE(), DATEADD(year, 1, GETUTCDATE()), 1, N'Monthly', N'PKR', 5000
            FROM Tenants t WHERE t.Id = @TenantId;
            """, new { TenantId = tenantId }, cancellationToken: ct));
    }

    private static async Task SyncTenantPlanAsync(System.Data.IDbConnection connection, int tenantId, string? planName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(planName)) return;
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Tenants SET SubscriptionPlan = @PlanName, UpdatedAt = GETUTCDATE() WHERE Id = @TenantId",
            new { TenantId = tenantId, PlanName = planName }, cancellationToken: ct));
    }

    private static async Task GenerateRenewalInvoiceAsync(System.Data.IDbConnection connection, int tenantId, CancellationToken ct)
    {
        var sub = await connection.QuerySingleOrDefaultAsync<(string? Plan, decimal? Amount, string? Currency)>(
            new CommandDefinition(
                "SELECT PlanName, MonthlyAmount AS Amount, CurrencyCode AS Currency FROM TenantSubscriptions WHERE TenantId = @TenantId",
                new { TenantId = tenantId }, cancellationToken: ct));

        var amount = sub.Amount ?? 5000m;
        var currency = string.IsNullOrWhiteSpace(sub.Currency) ? "PKR" : sub.Currency;
        var invoiceNumber = $"INV-{tenantId:D4}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantInvoices (TenantId, InvoiceNumber, PlanName, Amount, CurrencyCode, Status, IssuedDate, DueDate, CreatedAt)
            VALUES (@TenantId, @InvoiceNumber, @Plan, @Amount, @Currency, N'Pending', GETUTCDATE(), DATEADD(day, 14, GETUTCDATE()), GETUTCDATE());
            """, new { TenantId = tenantId, InvoiceNumber = invoiceNumber, Plan = sub.Plan, Amount = amount, Currency = currency },
            cancellationToken: ct));
    }
}
