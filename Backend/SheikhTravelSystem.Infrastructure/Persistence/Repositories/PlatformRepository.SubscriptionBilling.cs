using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class PlatformRepository
{
    public async Task<SubscriptionRevenueDashboardDto> LoadSubscriptionRevenueDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var summary = await connection.QuerySingleAsync<(int Active, int AutoRenew, int OverdueTenants, decimal Mrr, decimal Outstanding, decimal Collected30)>(
            new CommandDefinition("""
                SELECT
                    (SELECT COUNT(*) FROM TenantSubscriptions s
                     INNER JOIN Tenants t ON t.Id = s.TenantId AND t.IsActive = 1
                     WHERE s.Status = N'Active') AS Active,
                    (SELECT COUNT(*) FROM TenantSubscriptions s
                     INNER JOIN Tenants t ON t.Id = s.TenantId AND t.IsActive = 1
                     WHERE s.Status = N'Active' AND s.AutoRenew = 1) AS AutoRenew,
                    (SELECT COUNT(DISTINCT i.TenantId) FROM TenantInvoices i
                     WHERE i.Status = N'Overdue') AS OverdueTenants,
                    (SELECT COALESCE(SUM(s.MonthlyAmount), 0) FROM TenantSubscriptions s
                     INNER JOIN Tenants t ON t.Id = s.TenantId AND t.IsActive = 1
                     WHERE s.Status = N'Active') AS Mrr,
                    (SELECT COALESCE(SUM(i.Amount), 0) FROM TenantInvoices i
                     WHERE i.Status IN (N'Pending', N'Overdue')) AS Outstanding,
                    (SELECT COALESCE(SUM(h.Amount), 0) FROM TenantBillingHistory h
                     WHERE h.PaidAt >= DATEADD(day, -30, GETUTCDATE())) AS Collected30
                """, cancellationToken: cancellationToken));

        var renewalRows = await connection.QueryAsync<(
            int TenantId, string TenantName, string? PlanName, string? PlanKey, DateTime? NextBillingDate,
            decimal? MonthlyAmount, string CurrencyCode, string? BillingCycle, int? Devices)>(new CommandDefinition("""
            SELECT TOP 20
                s.TenantId,
                t.Name AS TenantName,
                COALESCE(s.PlanName, t.SubscriptionPlan) AS PlanName,
                COALESCE(s.SubscriptionCode, s.PlanName, t.SubscriptionPlan) AS PlanKey,
                s.NextBillingDate,
                s.MonthlyAmount,
                COALESCE(s.CurrencyCode, N'PKR') AS CurrencyCode,
                s.BillingCycle,
                s.MaxGpsDevices AS Devices
            FROM TenantSubscriptions s
            INNER JOIN Tenants t ON t.Id = s.TenantId AND t.IsActive = 1
            WHERE s.Status = N'Active'
              AND s.AutoRenew = 1
              AND s.NextBillingDate IS NOT NULL
              AND s.NextBillingDate <= DATEADD(day, 14, GETUTCDATE())
            ORDER BY s.NextBillingDate
            """, cancellationToken: cancellationToken));

        var renewals = renewalRows.Select(r => new SubscriptionRenewalRowDto(
                r.TenantId,
                r.TenantName,
                r.PlanName,
                r.NextBillingDate,
                r.MonthlyAmount,
                r.CurrencyCode,
                SubscriptionBillingCycle.Normalize(r.BillingCycle),
                SubscriptionPlanPricing.ResolveCycleAmount(r.PlanKey, r.Devices, r.BillingCycle)))
            .ToList();

        var overdue = (await connection.QueryAsync<SubscriptionOverdueAccountDto>(new CommandDefinition("""
            SELECT
                t.Id AS TenantId,
                t.Name AS TenantName,
                COALESCE(s.PlanName, t.SubscriptionPlan) AS PlanName,
                COALESCE(SUM(i.Amount), 0) AS OverdueAmount,
                MAX(i.DueDate) AS OldestDueDate,
                COUNT(*) AS OverdueInvoiceCount,
                COALESCE(MAX(CAST(s.FeaturesLocked AS INT)), 0) AS FeaturesLocked
            FROM TenantInvoices i
            INNER JOIN Tenants t ON t.Id = i.TenantId AND t.IsActive = 1
            LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
            WHERE i.Status = N'Overdue'
            GROUP BY t.Id, t.Name, COALESCE(s.PlanName, t.SubscriptionPlan)
            ORDER BY OverdueAmount DESC
            """, cancellationToken: cancellationToken))).ToList();

        return new SubscriptionRevenueDashboardDto(
            summary.Active,
            summary.AutoRenew,
            summary.OverdueTenants,
            summary.Mrr,
            summary.Outstanding,
            summary.Collected30,
            renewals,
            overdue);
    }

    public async Task SyncTenantSubscriptionInvoicesAsync(int tenantId, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<(int Id, string Status, DateTime? DueDate, DateTime? PaidDate)>(
            new CommandDefinition(
                "SELECT Id, Status, DueDate, PaidDate FROM TenantInvoices WHERE TenantId = @TenantId",
                new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

        foreach (var row in rows)
        {
            var normalized = SubscriptionBillingLifecycle.NormalizeInvoiceStatus(
                row.Status, 0, row.DueDate, row.PaidDate, asOfUtc);
            if (!string.Equals(normalized, row.Status, StringComparison.OrdinalIgnoreCase))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE TenantInvoices SET Status = @Status WHERE Id = @Id",
                    new { Id = row.Id, Status = normalized }, cancellationToken: cancellationToken));
            }
        }
    }

    public async Task<decimal> SumOverdueSubscriptionInvoicesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT COALESCE(SUM(Amount), 0) FROM TenantInvoices WHERE TenantId = @TenantId AND Status = N'Overdue'",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task RefreshSubscriptionFeatureLockAsync(int tenantId, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sub = await connection.QuerySingleOrDefaultAsync<(string? Status, bool FeaturesLocked)>(
            new CommandDefinition(
                "SELECT COALESCE(Status, N'Active') AS Status, COALESCE(FeaturesLocked, 0) AS FeaturesLocked FROM TenantSubscriptions WHERE TenantId = @TenantId",
                new { TenantId = tenantId }, cancellationToken: cancellationToken));

        var suspended = string.Equals(sub.Status, "Suspended", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(sub.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);
        var overdue = await SumOverdueSubscriptionInvoicesAsync(tenantId, cancellationToken) > 0;
        var locked = SubscriptionBillingLifecycle.ShouldLockFeatures(suspended, overdue, asOfUtc);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TenantSubscriptions SET FeaturesLocked = @Locked, UpdatedAt = GETUTCDATE() WHERE TenantId = @TenantId",
            new { TenantId = tenantId, Locked = locked }, cancellationToken: cancellationToken));
    }

    public async Task<bool> AreSubscriptionFeaturesLockedAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT COALESCE(FeaturesLocked, 0) FROM TenantSubscriptions WHERE TenantId = @TenantId",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<int>> ListActiveTenantIdsForBillingAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var ids = await connection.QueryAsync<int>(new CommandDefinition("""
            SELECT t.Id
            FROM Tenants t
            INNER JOIN TenantSubscriptions s ON s.TenantId = t.Id
            WHERE t.IsActive = 1
            """, cancellationToken: cancellationToken));
        return ids.ToList();
    }

    public async Task<int?> CreateSubscriptionRenewalInvoiceAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sub = await connection.QuerySingleOrDefaultAsync<(
            string? Plan, string? Code, int? Devices, string? Currency, DateTime? NextBilling, bool AutoRenew, string Status, string? Cycle)>(
            new CommandDefinition("""
                SELECT PlanName AS [Plan], SubscriptionCode AS Code, MaxGpsDevices AS Devices, CurrencyCode AS Currency,
                       NextBillingDate AS NextBilling, COALESCE(AutoRenew, 1) AS AutoRenew, COALESCE(Status, N'Active') AS Status,
                       BillingCycle AS Cycle
                FROM TenantSubscriptions WHERE TenantId = @TenantId
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (sub.Status is not "Active" || !sub.AutoRenew)
            return null;

        var asOf = DateTime.UtcNow;
        if (sub.NextBilling.HasValue && sub.NextBilling.Value.Date > asOf.Date)
            return null;

        var pendingExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM TenantInvoices
            WHERE TenantId = @TenantId AND Status IN (N'Pending', N'Overdue')
              AND IssuedDate >= DATEADD(day, -45, GETUTCDATE())
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        if (pendingExists > 0)
            return null;

        // Priced per licensed GPS device at invoice time, so device increases flow into the next invoice.
        var planKey = sub.Code ?? sub.Plan;
        var monthlyAmount = SubscriptionPlanPricing.ResolveMonthlyAmount(planKey, sub.Devices);
        var amount = SubscriptionPlanPricing.ResolveCycleAmount(planKey, sub.Devices, sub.Cycle);
        var currency = string.IsNullOrWhiteSpace(sub.Currency) ? "PKR" : sub.Currency!;
        var invoiceNumber = $"SUB-{tenantId:D4}-{asOf:yyyyMMddHHmmss}";
        var periodStart = sub.NextBilling ?? asOf;
        var periodEnd = SubscriptionBillingCycle.PeriodEnd(periodStart, sub.Cycle);

        var invoiceId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TenantInvoices (
                TenantId, InvoiceNumber, PlanName, Amount, CurrencyCode, Status,
                IssuedDate, DueDate, CreatedAt, BillingPeriodStart, BillingPeriodEnd)
            VALUES (
                @TenantId, @InvoiceNumber, @Plan, @Amount, @Currency, N'Pending',
                GETUTCDATE(), DATEADD(day, @DueDays, GETUTCDATE()), GETUTCDATE(), @PeriodStart, @PeriodEnd);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            InvoiceNumber = invoiceNumber,
            Plan = sub.Plan,
            Amount = amount,
            Currency = currency,
            DueDays = SubscriptionPlanPricing.InvoiceDueDays,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd
        }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TenantSubscriptions
            SET NextBillingDate = @NextBilling,
                SubscriptionEndDate = @PeriodEnd,
                MonthlyAmount = @MonthlyAmount,
                UpdatedAt = GETUTCDATE()
            WHERE TenantId = @TenantId
            """, new
        {
            TenantId = tenantId,
            NextBilling = periodEnd,
            PeriodEnd = periodEnd,
            MonthlyAmount = monthlyAmount
        }, cancellationToken: cancellationToken));

        return invoiceId;
    }

    /// <summary>
    /// Re-derives <c>MonthlyAmount</c> (monthly equivalent of the yearly per-device package) from the stored plan
    /// and licensed device count. Call after the plan or <c>MaxGpsDevices</c> changes.
    /// </summary>
    private static async Task RefreshSubscriptionPriceAsync(
        System.Data.IDbConnection connection, int tenantId, CancellationToken ct)
    {
        var sub = await connection.QuerySingleOrDefaultAsync<(int TenantId, string? PlanKey, int? Devices)>(new CommandDefinition("""
            SELECT s.TenantId, COALESCE(s.SubscriptionCode, s.PlanName, t.SubscriptionPlan) AS PlanKey, s.MaxGpsDevices AS Devices
            FROM TenantSubscriptions s
            INNER JOIN Tenants t ON t.Id = s.TenantId
            WHERE s.TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: ct));
        if (sub.TenantId == 0) return;

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TenantSubscriptions SET MonthlyAmount = @Amount, UpdatedAt = GETUTCDATE() WHERE TenantId = @TenantId",
            new { TenantId = tenantId, Amount = SubscriptionPlanPricing.ResolveMonthlyAmount(sub.PlanKey, sub.Devices) },
            cancellationToken: ct));
    }

    public async Task<bool> MarkSubscriptionInvoicePaidAsync(
        int tenantId, int invoiceId, string? paymentMethod, string? reference, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var invoice = await GetSubscriptionInvoiceAsync(tenantId, invoiceId, cancellationToken);
        if (invoice is null) return false;

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TenantInvoices
            SET Status = N'Paid', PaidDate = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = invoiceId, TenantId = tenantId }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantBillingHistory (TenantId, InvoiceId, Amount, CurrencyCode, PaymentMethod, Status, Reference, PaidAt)
            VALUES (@TenantId, @InvoiceId, @Amount, @Currency, @Method, N'Completed', @Reference, GETUTCDATE())
            """, new
        {
            TenantId = tenantId,
            InvoiceId = invoiceId,
            Amount = invoice.Value.Amount,
            Currency = invoice.Value.CurrencyCode,
            Method = paymentMethod ?? "Manual",
            Reference = reference ?? $"PAY-{invoiceId:D6}"
        }, cancellationToken: cancellationToken));

        await RefreshSubscriptionFeatureLockAsync(tenantId, DateTime.UtcNow, cancellationToken);
        return true;
    }

    public async Task<(int Id, string InvoiceNumber, decimal Amount, string CurrencyCode, string? PlanName, DateTime? DueDate)?> GetSubscriptionInvoiceAsync(
        int tenantId, int invoiceId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(int Id, string InvoiceNumber, decimal Amount, string CurrencyCode, string? PlanName, DateTime? DueDate)>(
            new CommandDefinition(
                "SELECT Id, InvoiceNumber, Amount, CurrencyCode, PlanName, DueDate FROM TenantInvoices WHERE Id = @Id AND TenantId = @TenantId",
                new { Id = invoiceId, TenantId = tenantId }, cancellationToken: cancellationToken));
        return row.Id == 0 ? null : row;
    }

    public async Task RecordSubscriptionInvoiceEmailSentAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TenantInvoices SET EmailSentAt = GETUTCDATE() WHERE Id = @Id",
            new { Id = invoiceId }, cancellationToken: cancellationToken));
    }

    public async Task<int> CountActiveVehiclesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Vehicles WHERE TenantId = @TenantId AND IsDeleted = 0",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<string?> ResolveBillingEmailAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            SELECT TOP 1 COALESCE(tb.BillingEmail, t.SupportEmail, u.Email)
            FROM Tenants t
            LEFT JOIN TenantBilling tb ON tb.TenantId = t.Id
            OUTER APPLY (
                SELECT TOP 1 Email FROM Users
                WHERE TenantId = t.Id AND IsDeleted = 0 AND IsActive = 1
                  AND Role IN (N'SuperAdmin', N'TenantAdmin', N'Admin', N'Owner')
                ORDER BY Id
            ) u
            WHERE t.Id = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
