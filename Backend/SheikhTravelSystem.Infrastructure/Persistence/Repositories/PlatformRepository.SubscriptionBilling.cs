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

        var summary = await connection.QuerySingleAsync<(
            int Active, int AutoRenew, int OverdueTenants, decimal Mrr, decimal Outstanding, decimal Collected30,
            int Expired, int Deactivated)>(
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
                     WHERE s.Status IN (N'Active', N'PastDue')) AS Mrr,
                    (SELECT COALESCE(SUM(i.Amount), 0) FROM TenantInvoices i
                     WHERE i.Status IN (N'Pending', N'Overdue')) AS Outstanding,
                    (SELECT COALESCE(SUM(h.Amount), 0) FROM TenantBillingHistory h
                     WHERE h.PaidAt >= DATEADD(day, -30, GETUTCDATE())
                       AND h.Status = N'Completed') AS Collected30,
                    (SELECT COUNT(*) FROM TenantSubscriptions WHERE Status = N'Expired') AS Expired,
                    (SELECT COUNT(*) FROM Tenants WHERE IsActive = 0) AS Deactivated
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
                MIN(i.DueDate) AS OldestDueDate,
                COUNT(*) AS OverdueInvoiceCount,
                CAST(COALESCE(MAX(CAST(s.FeaturesLocked AS INT)), 0) AS BIT) AS FeaturesLocked
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
            overdue,
            AnnualRecurringRevenue: summary.Mrr * 12m,
            ExpiredSubscriptions: summary.Expired,
            DeactivatedTenants: summary.Deactivated);
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
        var sub = await connection.QuerySingleOrDefaultAsync<(
            string? Status, DateTime? PaidThrough, DateTime? AccessExpires)>(
            new CommandDefinition("""
                SELECT COALESCE(Status, N'Active') AS Status,
                       PaidThroughUtc AS PaidThrough,
                       AccessExpiresAtUtc AS AccessExpires
                FROM TenantSubscriptions WHERE TenantId = @TenantId
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        var unpaid = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM TenantInvoices
            WHERE TenantId = @TenantId AND Status IN (N'Pending', N'Overdue')
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;

        var resolved = SubscriptionBillingLifecycle.ResolveSubscriptionStatus(
            sub.Status, sub.PaidThrough, sub.AccessExpires, unpaid, asOfUtc);
        var pastPaid = sub.PaidThrough.HasValue && asOfUtc > sub.PaidThrough.Value && unpaid;
        var locked = SubscriptionBillingLifecycle.ShouldLockFeatures(resolved, pastPaid, asOfUtc);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TenantSubscriptions
            SET FeaturesLocked = @Locked,
                Status = CASE
                    WHEN Status IN (N'Suspended', N'Cancelled') THEN Status
                    ELSE @Resolved
                END,
                AccessExpiresAtUtc = COALESCE(AccessExpiresAtUtc, DATEADD(day, @Grace, PaidThroughUtc)),
                UpdatedAt = GETUTCDATE()
            WHERE TenantId = @TenantId
            """, new
        {
            TenantId = tenantId,
            Locked = locked,
            Resolved = resolved,
            Grace = SubscriptionPlanPricing.OverdueGraceDays
        }, cancellationToken: cancellationToken));
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

        // Priced per licensed vehicle/GPS seat at invoice time (USD list; PKR tenants store converted amounts).
        // IMPORTANT: creating a Pending invoice must NOT extend PaidThrough / SubscriptionEndDate.
        var planKey = sub.Code ?? sub.Plan;
        var currency = string.IsNullOrWhiteSpace(sub.Currency)
            ? SubscriptionPlanPricing.CurrencyUsd
            : sub.Currency!;
        decimal? exchangeRate = SubscriptionPlanPricing.IsPkr(currency) ? SubscriptionPlanPricing.UsdToPkrRate : null;
        var amount = SubscriptionPlanPricing.ResolveCycleAmountInCurrency(planKey, sub.Devices, sub.Cycle, currency);
        var monthlyAmount = SubscriptionPlanPricing.ResolveMonthlyAmountInCurrency(planKey, sub.Devices, currency);
        var invoiceNumber = $"SUB-{tenantId:D4}-{asOf:yyyyMMddHHmmss}";
        var periodStart = (sub.NextBilling ?? asOf).Date;
        var periodEnd = SubscriptionBillingCycle.PeriodEnd(periodStart, sub.Cycle);

        // Duplicate period guard (in addition to Pending/Overdue window check).
        var periodDup = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM TenantInvoices
            WHERE TenantId = @TenantId
              AND BillingPeriodStart = @PeriodStart AND BillingPeriodEnd = @PeriodEnd
              AND Status IN (N'Pending', N'Overdue', N'Paid')
            """, new { TenantId = tenantId, PeriodStart = periodStart, PeriodEnd = periodEnd },
            cancellationToken: cancellationToken));
        if (periodDup > 0)
            return null;

        var invoiceId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TenantInvoices (
                TenantId, InvoiceNumber, PlanName, Amount, CurrencyCode, Status,
                IssuedDate, DueDate, CreatedAt, BillingPeriodStart, BillingPeriodEnd, ExchangeRateUsed)
            VALUES (
                @TenantId, @InvoiceNumber, @Plan, @Amount, @Currency, N'Pending',
                GETUTCDATE(), DATEADD(day, @DueDays, GETUTCDATE()), GETUTCDATE(),
                @PeriodStart, @PeriodEnd, @ExchangeRate);
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
            PeriodEnd = periodEnd,
            ExchangeRate = exchangeRate
        }, cancellationToken: cancellationToken));

        // Refresh listed MonthlyAmount for MRR only — do not advance paid-through or end date.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TenantSubscriptions
            SET MonthlyAmount = @MonthlyAmount,
                UpdatedAt = GETUTCDATE()
            WHERE TenantId = @TenantId
            """, new { TenantId = tenantId, MonthlyAmount = monthlyAmount }, cancellationToken: cancellationToken));

        return invoiceId;
    }

    /// <summary>
    /// Re-derives <c>MonthlyAmount</c> (USD monthly list, or PKR when subscription currency is PKR)
    /// from the stored plan and licensed seat count. Call after the plan or <c>MaxGpsDevices</c> changes.
    /// </summary>
    private static async Task RefreshSubscriptionPriceAsync(
        System.Data.IDbConnection connection, int tenantId, CancellationToken ct)
    {
        var sub = await connection.QuerySingleOrDefaultAsync<(int TenantId, string? PlanKey, int? Devices, string? Currency)>(new CommandDefinition("""
            SELECT s.TenantId,
                   COALESCE(s.SubscriptionCode, s.PlanName, t.SubscriptionPlan) AS PlanKey,
                   s.MaxGpsDevices AS Devices,
                   s.CurrencyCode AS Currency
            FROM TenantSubscriptions s
            INNER JOIN Tenants t ON t.Id = s.TenantId
            WHERE s.TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: ct));
        if (sub.TenantId == 0) return;

        var amount = SubscriptionPlanPricing.ResolveMonthlyAmountInCurrency(sub.PlanKey, sub.Devices, sub.Currency);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TenantSubscriptions SET MonthlyAmount = @Amount, UpdatedAt = GETUTCDATE() WHERE TenantId = @TenantId",
            new { TenantId = tenantId, Amount = amount },
            cancellationToken: ct));
    }

    public async Task<bool> MarkSubscriptionInvoicePaidAsync(
        int tenantId, int invoiceId, string? paymentMethod, string? reference, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var invoice = await connection.QuerySingleOrDefaultAsync<(
            int Id, string InvoiceNumber, decimal Amount, string CurrencyCode, string? PlanName,
            DateTime? DueDate, DateTime? PeriodStart, DateTime? PeriodEnd, string Status)>(
            new CommandDefinition("""
                SELECT Id, InvoiceNumber, Amount, CurrencyCode, PlanName, DueDate,
                       BillingPeriodStart AS PeriodStart, BillingPeriodEnd AS PeriodEnd, Status
                FROM TenantInvoices WHERE Id = @Id AND TenantId = @TenantId
                """, new { Id = invoiceId, TenantId = tenantId }, transaction: tx, cancellationToken: cancellationToken));
        if (invoice.Id == 0) return false;
        if (string.Equals(invoice.Status, "Paid", StringComparison.OrdinalIgnoreCase))
        {
            tx.Commit();
            return true;
        }

        var sub = await connection.QuerySingleOrDefaultAsync<(string? Status, DateTime? PaidThrough)>(
            new CommandDefinition(
                "SELECT Status, PaidThroughUtc AS PaidThrough FROM TenantSubscriptions WHERE TenantId = @TenantId",
                new { TenantId = tenantId }, transaction: tx, cancellationToken: cancellationToken));

        // Do not reactivate an administratively Suspended subscription via payment alone.
        if (string.Equals(sub.Status, SubscriptionBillingLifecycle.StatusSuspended, StringComparison.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE TenantInvoices SET Status = N'Paid', PaidDate = GETUTCDATE()
                WHERE Id = @Id AND TenantId = @TenantId
                """, new { Id = invoiceId, TenantId = tenantId }, transaction: tx, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TenantBillingHistory (TenantId, InvoiceId, Amount, CurrencyCode, PaymentMethod, Status, Reference, PaidAt)
                VALUES (@TenantId, @InvoiceId, @Amount, @Currency, @Method, N'Completed', @Reference, GETUTCDATE())
                """, new
            {
                TenantId = tenantId,
                InvoiceId = invoiceId,
                Amount = invoice.Amount,
                Currency = invoice.CurrencyCode,
                Method = paymentMethod ?? "Manual",
                Reference = reference ?? $"PAY-{invoiceId:D6}"
            }, transaction: tx, cancellationToken: cancellationToken));
            tx.Commit();
            return true;
        }

        var otherBlocking = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM TenantInvoices
            WHERE TenantId = @TenantId AND Id <> @Id AND Status = N'Overdue'
            """, new { TenantId = tenantId, Id = invoiceId }, transaction: tx, cancellationToken: cancellationToken));

        var periodEnd = invoice.PeriodEnd
            ?? (invoice.PeriodStart.HasValue
                ? invoice.PeriodStart.Value
                : DateTime.UtcNow.Date.AddMonths(1));
        if (sub.PaidThrough.HasValue && sub.PaidThrough.Value > periodEnd)
            periodEnd = sub.PaidThrough.Value;

        var accessExpires = SubscriptionBillingLifecycle.ComputeAccessExpiry(periodEnd);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TenantInvoices SET Status = N'Paid', PaidDate = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = invoiceId, TenantId = tenantId }, transaction: tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantBillingHistory (TenantId, InvoiceId, Amount, CurrencyCode, PaymentMethod, Status, Reference, PaidAt)
            VALUES (@TenantId, @InvoiceId, @Amount, @Currency, @Method, N'Completed', @Reference, GETUTCDATE())
            """, new
        {
            TenantId = tenantId,
            InvoiceId = invoiceId,
            Amount = invoice.Amount,
            Currency = invoice.CurrencyCode,
            Method = paymentMethod ?? "Manual",
            Reference = reference ?? $"PAY-{invoiceId:D6}"
        }, transaction: tx, cancellationToken: cancellationToken));

        var reactivate = otherBlocking == 0;
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TenantSubscriptions
            SET PaidThroughUtc = @PaidThrough,
                AccessExpiresAtUtc = @AccessExpires,
                SubscriptionEndDate = @PaidThrough,
                NextBillingDate = @PaidThrough,
                Status = CASE WHEN @Reactivate = 1 THEN N'Active' ELSE Status END,
                FeaturesLocked = CASE WHEN @Reactivate = 1 THEN 0 ELSE FeaturesLocked END,
                UpdatedAt = GETUTCDATE()
            WHERE TenantId = @TenantId
            """, new
        {
            TenantId = tenantId,
            PaidThrough = periodEnd,
            AccessExpires = accessExpires,
            Reactivate = reactivate ? 1 : 0
        }, transaction: tx, cancellationToken: cancellationToken));

        if (reactivate)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE Tenants SET IsActive = 1 WHERE Id = @TenantId",
                new { TenantId = tenantId }, transaction: tx, cancellationToken: cancellationToken));
        }

        tx.Commit();
        await RefreshSubscriptionFeatureLockAsync(tenantId, DateTime.UtcNow, cancellationToken);
        return true;
    }

    public async Task ProcessSubscriptionExpiryAndDeactivationAsync(
        int tenantId, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sub = await connection.QuerySingleOrDefaultAsync<(
            string? Status, DateTime? PaidThrough, DateTime? AccessExpires, bool FeaturesLocked)>(
            new CommandDefinition("""
                SELECT COALESCE(Status, N'Active') AS Status,
                       PaidThroughUtc AS PaidThrough,
                       AccessExpiresAtUtc AS AccessExpires,
                       COALESCE(FeaturesLocked, 0) AS FeaturesLocked
                FROM TenantSubscriptions WHERE TenantId = @TenantId
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (string.IsNullOrEmpty(sub.Status)) return;
        if (string.Equals(sub.Status, SubscriptionBillingLifecycle.StatusSuspended, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sub.Status, SubscriptionBillingLifecycle.StatusCancelled, StringComparison.OrdinalIgnoreCase))
            return;

        var unpaid = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM TenantInvoices
            WHERE TenantId = @TenantId AND Status IN (N'Pending', N'Overdue')
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;

        var accessExpires = sub.AccessExpires ?? SubscriptionBillingLifecycle.ComputeAccessExpiry(sub.PaidThrough);
        var resolved = SubscriptionBillingLifecycle.ResolveSubscriptionStatus(
            sub.Status, sub.PaidThrough, accessExpires, unpaid, asOfUtc);

        if (!SubscriptionBillingLifecycle.ShouldDeactivateTenant(resolved, accessExpires, asOfUtc)
            && !string.Equals(resolved, SubscriptionBillingLifecycle.StatusExpired, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(resolved, SubscriptionBillingLifecycle.StatusPastDue, StringComparison.OrdinalIgnoreCase))
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE TenantSubscriptions
                    SET Status = N'PastDue', FeaturesLocked = 1,
                        AccessExpiresAtUtc = COALESCE(AccessExpiresAtUtc, @AccessExpires),
                        UpdatedAt = GETUTCDATE()
                    WHERE TenantId = @TenantId AND Status NOT IN (N'Suspended', N'Cancelled', N'Expired')
                    """, new { TenantId = tenantId, AccessExpires = accessExpires }, cancellationToken: cancellationToken));
            }
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TenantSubscriptions
            SET Status = N'Expired',
                FeaturesLocked = 1,
                AccessExpiresAtUtc = COALESCE(AccessExpiresAtUtc, @AccessExpires),
                UpdatedAt = GETUTCDATE()
            WHERE TenantId = @TenantId AND Status NOT IN (N'Suspended', N'Cancelled')
            """, new { TenantId = tenantId, AccessExpires = accessExpires }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Tenants SET IsActive = 0 WHERE Id = @TenantId AND IsActive = 1",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));

        // Audit via billing history note (no separate audit table required).
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantBillingHistory (TenantId, InvoiceId, Amount, CurrencyCode, PaymentMethod, Status, Reference, PaidAt)
            VALUES (@TenantId, NULL, 0, N'USD', N'System', N'SubscriptionExpired', @Ref, GETUTCDATE())
            """, new
        {
            TenantId = tenantId,
            Ref = $"EXPIRED-{asOfUtc:yyyyMMdd}"
        }, cancellationToken: cancellationToken));
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

    public async Task<(bool TenantIsActive, string SubscriptionStatus, DateTime? PaidThroughUtc, DateTime? AccessExpiresAtUtc)?> GetTenantSubscriptionAccessAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(
            bool? TenantActive, string? Status, DateTime? PaidThrough, DateTime? AccessExpires)>(
            new CommandDefinition("""
                SELECT COALESCE(t.IsActive, 0) AS TenantActive,
                       COALESCE(s.Status, N'Active') AS Status,
                       s.PaidThroughUtc AS PaidThrough,
                       s.AccessExpiresAtUtc AS AccessExpires
                FROM Tenants t
                LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
                WHERE t.Id = @TenantId
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row.TenantActive is null && row.Status is null)
            return null;

        return (row.TenantActive ?? false, row.Status ?? "Active", row.PaidThrough, row.AccessExpires);
    }

    public async Task<bool> WasSubscriptionLifecycleEmailSentAsync(
        int tenantId, string templateKey, string reference, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        // Dedupe via Notifications only (billing history is audit, written before email).
        var viaNotify = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM Notifications
            WHERE TenantId = @TenantId
              AND TemplateKey = @TemplateKey
              AND (Title LIKE '%' + @Reference + '%' OR Message LIKE '%' + @Reference + '%')
              AND CreatedAt >= DATEADD(day, -7, GETUTCDATE())
            """, new { TenantId = tenantId, TemplateKey = templateKey, Reference = reference },
            cancellationToken: cancellationToken));
        return viaNotify > 0;
    }
}
