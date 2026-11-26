using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>
/// Raw facts for the profitability engine. Every statement filters on TenantId; contexts apply the branch clamp.
/// Context roll-up: a booking file owned by a group and a visa case linked to a group both report under the group.
/// </summary>
public sealed class TravelProfitabilityRepository(IDbConnectionFactory dbFactory) : ITravelProfitabilityRepository
{
    private const int ChunkSize = 500;

    /// <summary>Filter on a derived row <c>x</c> (ContextType, ContextId) by the chunk's id lists.</summary>
    private const string KeyFilter = """
        ((x.ContextType = N'Booking' AND x.ContextId IN @BookingIds)
          OR (x.ContextType = N'VisaCase' AND x.ContextId IN @VisaCaseIds)
          OR (x.ContextType = N'Group' AND x.ContextId IN @GroupIds))
        """;

    /// <summary>Earliest flight departure / hotel check-in of booking <c>b</c>.</summary>
    private const string BookingTravelDate = """
        (SELECT MIN(t.d) FROM (
            SELECT MIN(CAST(sg.DepartLocal AS DATE)) AS d FROM TravelFlightSegments sg
              INNER JOIN TravelFlightBookings f ON f.Id = sg.FlightBookingId AND f.TenantId = sg.TenantId
              WHERE f.BookingId = b.Id AND f.TenantId = b.TenantId AND f.IsDeleted = 0
            UNION ALL
            SELECT MIN(h.CheckIn) FROM TravelHotelBookings h WHERE h.BookingId = b.Id AND h.TenantId = b.TenantId AND h.IsDeleted = 0) t)
        """;

    /// <summary>Booking files not owned by a group, visa cases not linked to a group, groups.</summary>
    private const string ContextsSelect = $"""
        SELECT N'Booking' AS ContextType, b.Id AS ContextId, CONCAT(b.BookingNumber, N' · ', b.Title) AS Label, {BookingTravelDate} AS TravelDate,
               b.BranchId, br.Name AS BranchName, b.AssignedAgentUserId AS AgentUserId, u.FullName AS AgentName
        FROM TravelBookings b
        LEFT JOIN Branches br ON br.Id = b.BranchId AND br.TenantId = b.TenantId
        LEFT JOIN Users u ON u.Id = b.AssignedAgentUserId AND u.TenantId = b.TenantId
        WHERE b.TenantId = @TenantId AND b.IsDeleted = 0 AND b.GroupId IS NULL
        UNION ALL
        SELECT N'VisaCase', v.Id, CONCAT(v.CaseNumber, N' · ', c.FullName), v.IntendedTravelDate, v.BranchId, br.Name, v.AssignedAgentUserId, u.FullName
        FROM TravelVisaCases v
        INNER JOIN Customers c ON c.Id = v.CustomerId AND c.TenantId = v.TenantId
        LEFT JOIN Branches br ON br.Id = v.BranchId AND br.TenantId = v.TenantId
        LEFT JOIN Users u ON u.Id = v.AssignedAgentUserId AND u.TenantId = v.TenantId
        WHERE v.TenantId = @TenantId AND v.IsDeleted = 0
          AND NOT EXISTS (SELECT 1 FROM TravelGroups g WHERE g.VisaCaseId = v.Id AND g.TenantId = v.TenantId AND g.IsDeleted = 0)
        UNION ALL
        SELECT N'Group', g.Id, CONCAT(g.GroupNumber, N' · ', g.Name), g.DepartureDate, g.BranchId, br.Name, NULL, NULL
        FROM TravelGroups g
        LEFT JOIN Branches br ON br.Id = g.BranchId AND br.TenantId = g.TenantId
        WHERE g.TenantId = @TenantId AND g.IsDeleted = 0
        """;

    /// <summary>Rolls a raw (r.RawType, r.RawId) up to its reporting context (ctx); <paramref name="tenantColumn"/> is the row's tenant column.</summary>
    private static string RollUp(string tenantColumn) => $"""
        LEFT JOIN TravelBookings rb ON r.RawType = N'Booking' AND rb.Id = r.RawId AND rb.TenantId = {tenantColumn}
        OUTER APPLY (SELECT TOP 1 g.Id FROM TravelGroups g
                     WHERE r.RawType = N'VisaCase' AND g.VisaCaseId = r.RawId AND g.TenantId = {tenantColumn} AND g.IsDeleted = 0 ORDER BY g.Id) rg
        CROSS APPLY (SELECT CASE WHEN rb.GroupId IS NOT NULL OR rg.Id IS NOT NULL THEN N'Group' ELSE r.RawType END AS ContextType,
                            COALESCE(rb.GroupId, rg.Id, r.RawId) AS ContextId) ctx
        """;

    /// <summary>Issued invoice lines with their context: sourced lines by the service, manual lines by the invoice's context.</summary>
    private static readonly string RevenueSelect = $"""
        SELECT i.Id AS InvoiceId, i.InvoiceNumber, i.IssueDate, i.CurrencyCode, i.Subtotal AS InvoiceSubtotal, i.DiscountAmount AS InvoiceDiscount,
               l.LineTotal, l.SourceType, ctx.ContextType, ctx.ContextId, l.Description
        FROM TravelInvoiceLines l
        INNER JOIN TravelInvoices i ON i.Id = l.InvoiceId AND i.TenantId = l.TenantId AND i.Status = N'Issued' AND i.IsDeleted = 0
        LEFT JOIN TravelFlightBookings f ON l.SourceType = N'Flight' AND f.Id = l.SourceId AND f.TenantId = l.TenantId
        LEFT JOIN TravelHotelBookings h ON l.SourceType = N'Hotel' AND h.Id = l.SourceId AND h.TenantId = l.TenantId
        LEFT JOIN TravelGroupMembers gm ON l.SourceType = N'GroupMember' AND gm.Id = l.SourceId AND gm.TenantId = l.TenantId
        CROSS APPLY (SELECT
            CASE WHEN l.SourceType IN (N'Flight', N'Hotel') THEN N'Booking'
                 WHEN l.SourceType IN (N'VisaServiceFee', N'VisaEmbassyFee') THEN N'VisaCase'
                 WHEN l.SourceType = N'GroupMember' THEN N'Group'
                 WHEN i.BookingId IS NOT NULL THEN N'Booking' WHEN i.VisaCaseId IS NOT NULL THEN N'VisaCase' WHEN i.GroupId IS NOT NULL THEN N'Group' END AS RawType,
            CASE WHEN l.SourceType = N'Flight' THEN f.BookingId WHEN l.SourceType = N'Hotel' THEN h.BookingId
                 WHEN l.SourceType IN (N'VisaServiceFee', N'VisaEmbassyFee') THEN l.SourceId
                 WHEN l.SourceType = N'GroupMember' THEN gm.GroupId
                 ELSE COALESCE(i.BookingId, i.VisaCaseId, i.GroupId) END AS RawId) r
        {RollUp("l.TenantId")}
        WHERE l.TenantId = @TenantId AND r.RawId IS NOT NULL
        """;

    /// <summary>Posted bill lines (net of supplier commission) and estimates for what is not billed yet.</summary>
    private static readonly string CostSelect = $"""
        SELECT ctx.ContextType, ctx.ContextId, sb.CurrencyCode, bl.NetAmount AS Amount, sb.BillDate AS Date, CAST(0 AS BIT) AS Estimated,
               sb.BillNumber AS Reference, bl.Description, sb.Id AS BillId
        FROM TravelSupplierBillLines bl
        INNER JOIN TravelSupplierBills sb ON sb.Id = bl.BillId AND sb.TenantId = bl.TenantId AND sb.Status = N'Posted' AND sb.IsDeleted = 0
        LEFT JOIN TravelFlightBookings f ON bl.SourceType = N'Flight' AND f.Id = bl.SourceId AND f.TenantId = bl.TenantId
        LEFT JOIN TravelHotelBookings h ON bl.SourceType = N'Hotel' AND h.Id = bl.SourceId AND h.TenantId = bl.TenantId
        CROSS APPLY (SELECT CASE WHEN bl.SourceType IS NOT NULL THEN N'Booking' ELSE bl.ContextType END AS RawType,
                            COALESCE(f.BookingId, h.BookingId, bl.ContextId) AS RawId) r
        {RollUp("bl.TenantId")}
        WHERE bl.TenantId = @TenantId AND bl.IsVoided = 0 AND r.RawId IS NOT NULL

        UNION ALL

        SELECT ctx.ContextType, ctx.ContextId, s.CurrencyCode,
               CASE WHEN s.Status = N'Cancelled' THEN COALESCE(pen.Penalty, 0) ELSE s.CostAmount END,
               COALESCE(s.ServiceDate, CAST(s.CreatedAt AS DATE)), CAST(1 AS BIT), bk.BookingNumber,
               CASE WHEN s.Status = N'Cancelled' THEN CONCAT(s.Description, N' (cancellation penalty)') ELSE s.Description END, NULL
        FROM (
            SELECT N'Flight' AS SourceType, f.Id, f.TenantId, f.BookingId, f.CostAmount, f.CurrencyCode, f.Status, f.CreatedAt,
                   (SELECT MIN(CAST(sg.DepartLocal AS DATE)) FROM TravelFlightSegments sg WHERE sg.FlightBookingId = f.Id AND sg.TenantId = f.TenantId) AS ServiceDate,
                   CONCAT(N'Flight', N' ' + f.Pnr) AS Description
            FROM TravelFlightBookings f
            WHERE f.TenantId = @TenantId AND f.IsDeleted = 0 AND f.CostAmount IS NOT NULL AND f.CurrencyCode IS NOT NULL
            UNION ALL
            SELECT N'Hotel', h.Id, h.TenantId, h.BookingId, h.CostAmount, h.CurrencyCode, h.Status, h.CreatedAt, h.CheckIn, CONCAT(N'Hotel ', ht.Name)
            FROM TravelHotelBookings h
            INNER JOIN TravelHotels ht ON ht.Id = h.HotelId AND ht.TenantId = h.TenantId
            WHERE h.TenantId = @TenantId AND h.IsDeleted = 0 AND h.CostAmount IS NOT NULL AND h.CurrencyCode IS NOT NULL
        ) s
        INNER JOIN TravelBookings bk ON bk.Id = s.BookingId AND bk.TenantId = s.TenantId AND bk.IsDeleted = 0
        OUTER APPLY (SELECT SUM(x.SupplierPenalty) AS Penalty FROM TravelBookingCancellations x
                     WHERE x.TenantId = s.TenantId AND ((s.SourceType = N'Flight' AND x.FlightBookingId = s.Id) OR (s.SourceType = N'Hotel' AND x.HotelBookingId = s.Id))) pen
        CROSS APPLY (SELECT N'Booking' AS RawType, s.BookingId AS RawId) r
        {RollUp("s.TenantId")}
        WHERE s.TenantId = @TenantId
          AND NOT EXISTS (SELECT 1 FROM TravelSupplierBillLines bl
                          INNER JOIN TravelSupplierBills sb ON sb.Id = bl.BillId AND sb.TenantId = bl.TenantId AND sb.Status = N'Posted' AND sb.IsDeleted = 0
                          WHERE bl.TenantId = s.TenantId AND bl.SourceType = s.SourceType AND bl.SourceId = s.Id AND bl.IsVoided = 0)

        UNION ALL

        SELECT ctx.ContextType, ctx.ContextId, v.CurrencyCode, v.QuotedEmbassyFee, COALESCE(v.IntendedTravelDate, CAST(v.CreatedAt AS DATE)), CAST(1 AS BIT),
               v.CaseNumber, N'Embassy fee (quoted)', NULL
        FROM TravelVisaCases v
        CROSS APPLY (SELECT N'VisaCase' AS RawType, v.Id AS RawId) r
        {RollUp("v.TenantId")}
        WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND v.Status <> N'Cancelled' AND v.QuotedEmbassyFee > 0 AND v.CurrencyCode IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM TravelSupplierBillLines bl
                          INNER JOIN TravelSupplierBills sb ON sb.Id = bl.BillId AND sb.TenantId = bl.TenantId AND sb.Status = N'Posted' AND sb.IsDeleted = 0
                          WHERE bl.TenantId = v.TenantId AND bl.ContextType = N'VisaCase' AND bl.ContextId = v.Id AND bl.IsVoided = 0)
        """;

    public async Task<IReadOnlyList<TravelContextFact>> GetContextsAsync(int tenantId, TravelBranchScope scope, IReadOnlyList<string> contextTypes,
        DateTime? travelFrom, DateTime? travelTo, CancellationToken cancellationToken = default)
    {
        if (contextTypes.Count == 0) return [];
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Types", contextTypes);
        var clauses = new List<string> { "x.ContextType IN @Types" };
        TravelSql.ApplyBranchScope(scope, "x.BranchId", clauses, p);
        if (travelFrom.HasValue) { clauses.Add("x.TravelDate >= @From"); p.Add("From", travelFrom.Value.Date); }
        if (travelTo.HasValue) { clauses.Add("x.TravelDate <= @To"); p.Add("To", travelTo.Value.Date); }
        return (await connection.QueryAsync<TravelContextFact>(new CommandDefinition($"""
            SELECT x.ContextType, x.ContextId, x.Label, x.TravelDate, x.BranchId, x.BranchName, x.AgentUserId, x.AgentName
            FROM ({ContextsSelect}) x
            WHERE {string.Join(" AND ", clauses)}
            """, p, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<TravelContextFact>> GetContextsByKeyAsync(int tenantId, IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
        => await ByKeysAsync<TravelContextFact>(tenantId, keys, $"""
            SELECT x.ContextType, x.ContextId, x.Label, x.TravelDate, x.BranchId, x.BranchName, x.AgentUserId, x.AgentName
            FROM ({ContextsSelect}) x
            WHERE {KeyFilter}
            """, cancellationToken);

    public async Task<IReadOnlyList<TravelRevenueFact>> GetRevenueFactsAsync(int tenantId, IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
        => await ByKeysAsync<TravelRevenueFact>(tenantId, keys, $"""
            SELECT x.InvoiceId, x.InvoiceNumber, x.IssueDate, x.CurrencyCode, x.InvoiceSubtotal, x.InvoiceDiscount, x.LineTotal, x.SourceType,
                   x.ContextType, x.ContextId, x.Description
            FROM ({RevenueSelect}) x
            WHERE {KeyFilter}
            """, cancellationToken);

    public async Task<IReadOnlyList<string>> GetInvoiceContextKeysAsync(int tenantId, IReadOnlyList<int> invoiceIds, CancellationToken cancellationToken = default)
    {
        if (invoiceIds.Count == 0) return [];
        using var connection = dbFactory.CreateConnection();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chunk in invoiceIds.Distinct().Chunk(ChunkSize))
        {
            var rows = await connection.QueryAsync<(string ContextType, int ContextId)>(new CommandDefinition($"""
                SELECT DISTINCT x.ContextType, x.ContextId FROM ({RevenueSelect}) x WHERE x.InvoiceId IN @Ids
                """, new { TenantId = tenantId, Ids = chunk }, cancellationToken: cancellationToken));
            foreach (var (type, id) in rows) keys.Add($"{type}:{id}");
        }
        return keys.ToList();
    }

    public async Task<IReadOnlyList<TravelInvoiceCreditFact>> GetInvoiceCreditsAsync(int tenantId, IReadOnlyList<int> invoiceIds, CancellationToken cancellationToken = default)
    {
        if (invoiceIds.Count == 0) return [];
        using var connection = dbFactory.CreateConnection();
        var result = new List<TravelInvoiceCreditFact>();
        foreach (var chunk in invoiceIds.Distinct().Chunk(ChunkSize))
            result.AddRange(await connection.QueryAsync<TravelInvoiceCreditFact>(new CommandDefinition("""
                SELECT cn.InvoiceId, cn.Amount, cn.IssuedOn
                FROM TravelCreditNotes cn
                WHERE cn.TenantId = @TenantId AND cn.Status = N'Issued' AND cn.InvoiceId IN @Ids
                """, new { TenantId = tenantId, Ids = chunk }, cancellationToken: cancellationToken)));
        return result;
    }

    public async Task<IReadOnlyList<TravelCostFact>> GetCostFactsAsync(int tenantId, IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
        => (await ByKeysAsync<TravelCostFact>(tenantId, keys, $"""
            SELECT x.ContextType, x.ContextId, x.CurrencyCode, x.Amount, x.Date, x.Estimated, x.Reference, x.Description, x.BillId
            FROM ({CostSelect}) x
            WHERE {KeyFilter}
            """, cancellationToken)).Where(c => c.Amount != 0).ToList();

    public async Task<IReadOnlyList<TravelBillCreditFact>> GetBillCreditsAsync(int tenantId, IReadOnlyList<int> billIds, CancellationToken cancellationToken = default)
    {
        if (billIds.Count == 0) return [];
        using var connection = dbFactory.CreateConnection();
        var result = new List<TravelBillCreditFact>();
        foreach (var chunk in billIds.Distinct().Chunk(ChunkSize))
            result.AddRange(await connection.QueryAsync<TravelBillCreditFact>(new CommandDefinition("""
                SELECT sc.BillId, sb.Net AS BillNet, sc.Amount, sc.IssuedOn, sc.CurrencyCode
                FROM TravelSupplierCredits sc
                INNER JOIN TravelSupplierBills sb ON sb.Id = sc.BillId AND sb.TenantId = sc.TenantId
                WHERE sc.TenantId = @TenantId AND sc.Status = N'Issued' AND sc.BillId IN @Ids
                """, new { TenantId = tenantId, Ids = chunk }, cancellationToken: cancellationToken)));
        return result;
    }

    /// <summary>Runs <paramref name="sql"/> (which filters with <see cref="KeyFilter"/>) over the keys in chunks.</summary>
    private async Task<List<T>> ByKeysAsync<T>(int tenantId, IReadOnlyList<string> keys, string sql, CancellationToken ct)
    {
        var parsed = keys.Distinct(StringComparer.Ordinal).Select(Parse).Where(k => k is not null).Select(k => k!.Value).ToList();
        if (parsed.Count == 0) return [];
        using var connection = dbFactory.CreateConnection();
        var result = new List<T>();
        foreach (var chunk in parsed.Chunk(ChunkSize))
        {
            var args = new
            {
                TenantId = tenantId,
                BookingIds = chunk.Where(k => k.Type == "Booking").Select(k => k.Id).ToList(),
                VisaCaseIds = chunk.Where(k => k.Type == "VisaCase").Select(k => k.Id).ToList(),
                GroupIds = chunk.Where(k => k.Type == "Group").Select(k => k.Id).ToList()
            };
            result.AddRange(await connection.QueryAsync<T>(new CommandDefinition(sql, args, cancellationToken: ct)));
        }
        return result;
    }

    private static (string Type, int Id)? Parse(string key)
    {
        var i = key.IndexOf(':');
        if (i <= 0 || !int.TryParse(key.AsSpan(i + 1), out var id)) return null;
        var type = key[..i];
        return type is "Booking" or "VisaCase" or "Group" ? (type, id) : null;
    }
}
