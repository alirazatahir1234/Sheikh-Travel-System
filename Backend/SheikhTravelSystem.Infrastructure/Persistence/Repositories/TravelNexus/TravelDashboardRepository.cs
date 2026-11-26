using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Dashboard.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>Dashboard counts and trends. Every statement filters on TenantId; the branch clamp is applied to the owning row.</summary>
public sealed class TravelDashboardRepository(IDbConnectionFactory dbFactory) : ITravelDashboardRepository
{
    private const string Overdue = "CASE WHEN i.Status = N'Issued' AND d.DueBefore - d.Covered > 0 THEN d.DueBefore - d.Covered ELSE 0 END";

    public async Task<(int Count, IReadOnlyList<TravelCurrencyAmountDto> Amounts)> GetOverdueInvoicesAsync(int tenantId, TravelBranchScope scope, DateTime today,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Today", today.Date);
        var clauses = new List<string> { "i.TenantId = @TenantId", "i.IsDeleted = 0", "i.Status = N'Issued'" };
        TravelSql.ApplyBranchScope(scope, "i.BranchId", clauses, p);
        var rows = (await connection.QueryAsync<(string CurrencyCode, int Count, decimal Amount)>(new CommandDefinition($"""
            SELECT x.CurrencyCode, COUNT(1), SUM(x.OverdueAmount)
            FROM (SELECT i.CurrencyCode, {Overdue} AS OverdueAmount
                  FROM TravelInvoices i
                  {TravelInvoiceRepository.Money}
                  WHERE i.TenantId = @TenantId AND {string.Join(" AND ", clauses)}) x
            WHERE x.OverdueAmount > 0
            GROUP BY x.CurrencyCode
            """, p, cancellationToken: cancellationToken))).ToList();
        return (rows.Sum(r => r.Count), rows.OrderBy(r => r.CurrencyCode, StringComparer.Ordinal)
            .Select(r => new TravelCurrencyAmountDto { CurrencyCode = r.CurrencyCode, Amount = r.Amount }).ToList());
    }

    public async Task<int> CountOverdueSupplierBillsAsync(int tenantId, TravelBranchScope scope, DateTime today, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Today", today.Date);
        var clauses = new List<string> { "b.TenantId = @TenantId", "b.IsDeleted = 0", "b.Status = N'Posted'", "b.DueDate < @Today", "d.Balance > 0" };
        TravelSql.ApplyBranchScope(scope, "b.BranchId", clauses, p);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT COUNT(1) FROM TravelSupplierBills b
            {TravelSupplierBillRepository.Money}
            WHERE b.TenantId = @TenantId AND {string.Join(" AND ", clauses)}
            """, p, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TravelTrendPointDto>> GetEnquiryTrendAsync(int tenantId, TravelBranchScope scope, DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("From", fromUtc);
        var clauses = new List<string> { "e.TenantId = @TenantId", "e.IsDeleted = 0", "e.CreatedAt >= @From" };
        TravelSql.ApplyBranchScope(scope, "e.BranchId", clauses, p);
        // Week starts on Monday regardless of the server's DATEFIRST setting.
        return (await connection.QueryAsync<TravelTrendPointDto>(new CommandDefinition($"""
            SELECT x.WeekStart AS PeriodStart, CONVERT(NVARCHAR(10), x.WeekStart, 23) AS Label, COUNT(1) AS Total, SUM(CASE WHEN x.Status = N'Won' THEN 1 ELSE 0 END) AS Won
            FROM (SELECT DATEADD(DAY, -((DATEPART(WEEKDAY, e.CreatedAt) + @@DATEFIRST - 2) % 7), CAST(e.CreatedAt AS DATE)) AS WeekStart, e.Status
                  FROM TravelEnquiries e WHERE e.TenantId = @TenantId AND {string.Join(" AND ", clauses)}) x
            GROUP BY x.WeekStart ORDER BY x.WeekStart
            """, p, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<TravelMoneyTrendPointDto>> GetInvoicingTrendAsync(int tenantId, TravelBranchScope scope, DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("From", fromUtc.Date);
        var invoiceClauses = new List<string> { "i.TenantId = @TenantId", "i.IsDeleted = 0", "i.Status = N'Issued'", "i.IssueDate >= @From" };
        TravelSql.ApplyBranchScope(scope, "i.BranchId", invoiceClauses, p);
        var paymentClauses = new List<string> { "py.TenantId = @TenantId", "py.IsDeleted = 0", "py.Status IN (N'Completed', N'Refunded')", "py.ReceivedOn >= @From" };
        TravelSql.ApplyBranchScope(scope, "py.BranchId", paymentClauses, p);
        var issued = (await connection.QueryAsync<(DateTime Month, string CurrencyCode, decimal Amount)>(new CommandDefinition($"""
            SELECT DATEFROMPARTS(YEAR(i.IssueDate), MONTH(i.IssueDate), 1), i.CurrencyCode, SUM(i.Total)
            FROM TravelInvoices i WHERE i.TenantId = @TenantId AND {string.Join(" AND ", invoiceClauses)}
            GROUP BY DATEFROMPARTS(YEAR(i.IssueDate), MONTH(i.IssueDate), 1), i.CurrencyCode
            """, p, cancellationToken: cancellationToken))).ToList();
        var collected = (await connection.QueryAsync<(DateTime Month, string CurrencyCode, decimal Amount)>(new CommandDefinition($"""
            SELECT DATEFROMPARTS(YEAR(py.ReceivedOn), MONTH(py.ReceivedOn), 1), py.CurrencyCode, SUM(py.Amount)
            FROM TravelPayments py WHERE py.TenantId = @TenantId AND {string.Join(" AND ", paymentClauses)}
            GROUP BY DATEFROMPARTS(YEAR(py.ReceivedOn), MONTH(py.ReceivedOn), 1), py.CurrencyCode
            """, p, cancellationToken: cancellationToken))).ToList();
        return issued.Select(r => (r.Month, r.CurrencyCode)).Union(collected.Select(r => (r.Month, r.CurrencyCode)))
            .OrderBy(k => k.Month).ThenBy(k => k.CurrencyCode, StringComparer.Ordinal)
            .Select(k => new TravelMoneyTrendPointDto
            {
                PeriodStart = k.Month, Label = k.Month.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture), CurrencyCode = k.CurrencyCode,
                Issued = issued.Where(r => r.Month == k.Month && r.CurrencyCode == k.CurrencyCode).Sum(r => r.Amount),
                Collected = collected.Where(r => r.Month == k.Month && r.CurrencyCode == k.CurrencyCode).Sum(r => r.Amount)
            }).ToList();
    }

    public async Task<IReadOnlyList<TravelDecisionTrendPointDto>> GetVisaDecisionTrendAsync(int tenantId, TravelBranchScope scope, DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("From", fromUtc);
        var clauses = new List<string> { "v.TenantId = @TenantId", "v.IsDeleted = 0", "a.DecidedAt >= @From", "a.Decision IN (N'Approved', N'Rejected')" };
        TravelSql.ApplyBranchScope(scope, "v.BranchId", clauses, p);
        return (await connection.QueryAsync<TravelDecisionTrendPointDto>(new CommandDefinition($"""
            SELECT DATEFROMPARTS(YEAR(a.DecidedAt), MONTH(a.DecidedAt), 1) AS PeriodStart,
                   FORMAT(DATEFROMPARTS(YEAR(a.DecidedAt), MONTH(a.DecidedAt), 1), 'MMM yyyy', 'en-US') AS Label,
                   SUM(CASE WHEN a.Decision = N'Approved' THEN 1 ELSE 0 END) AS Approved, SUM(CASE WHEN a.Decision = N'Rejected' THEN 1 ELSE 0 END) AS Rejected
            FROM TravelVisaApplicants a
            INNER JOIN TravelVisaCases v ON v.Id = a.VisaCaseId AND v.TenantId = a.TenantId
            WHERE a.TenantId = @TenantId AND a.IsDeleted = 0 AND {string.Join(" AND ", clauses)}
            GROUP BY DATEFROMPARTS(YEAR(a.DecidedAt), MONTH(a.DecidedAt), 1)
            ORDER BY PeriodStart
            """, p, cancellationToken: cancellationToken))).ToList();
    }
}
