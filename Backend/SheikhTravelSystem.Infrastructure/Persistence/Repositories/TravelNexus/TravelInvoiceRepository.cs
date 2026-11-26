using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>
/// Customer invoices and finance reports. Root predicate <c>i.TenantId = @TenantId</c>; every subquery and join repeats the
/// tenant predicate. Paid / credited / refunded are always computed from their source rows (<see cref="Money"/>).
/// </summary>
public sealed class TravelInvoiceRepository(IDbConnectionFactory dbFactory) : ITravelInvoiceRepository
{
    /// <summary>Per invoice <c>i</c>: money (m) and derived balance / coverage / amount due before @Today (d).</summary>
    internal const string Money = """
        OUTER APPLY (
            SELECT
              COALESCE((SELECT SUM(a.AmountApplied) FROM TravelPaymentAllocations a
                INNER JOIN TravelPayments p ON p.Id = a.PaymentId AND p.TenantId = a.TenantId
                WHERE a.InvoiceId = i.Id AND a.TenantId = i.TenantId AND p.IsDeleted = 0 AND p.Status IN (N'Completed', N'Refunded')), 0) AS Paid,
              COALESCE((SELECT SUM(a.AmountApplied) FROM TravelPaymentAllocations a
                INNER JOIN TravelPayments p ON p.Id = a.PaymentId AND p.TenantId = a.TenantId
                WHERE a.InvoiceId = i.Id AND a.TenantId = i.TenantId AND p.IsDeleted = 0 AND p.Status = N'Pending'), 0) AS PendingPaid,
              COALESCE((SELECT SUM(cn.Amount) FROM TravelCreditNotes cn
                WHERE cn.InvoiceId = i.Id AND cn.TenantId = i.TenantId AND cn.Status = N'Issued'), 0) AS Credited,
              COALESCE((SELECT SUM(r.Amount) FROM TravelRefunds r WHERE r.InvoiceId = i.Id AND r.TenantId = i.TenantId), 0) AS Refunded,
              COALESCE((SELECT SUM(ins.Amount) FROM TravelInvoiceInstallments ins
                WHERE ins.InvoiceId = i.Id AND ins.TenantId = i.TenantId AND ins.DueDate < @Today), 0) AS InstallmentsDue,
              (SELECT COUNT(1) FROM TravelInvoiceInstallments ins WHERE ins.InvoiceId = i.Id AND ins.TenantId = i.TenantId) AS InstallmentCount
        ) m
        CROSS APPLY (
            SELECT i.Total - m.Credited - (m.Paid - m.Refunded) AS Balance,
                   CASE WHEN m.Paid - m.Refunded + m.Credited > 0 THEN m.Paid - m.Refunded + m.Credited ELSE 0 END AS Covered,
                   CASE WHEN m.InstallmentCount > 0 THEN m.InstallmentsDue WHEN i.DueDate < @Today THEN i.Total ELSE 0 END AS DueBefore
        ) d
        """;

    private const string Overdue = "CASE WHEN i.Status = N'Issued' AND d.DueBefore - d.Covered > 0 THEN d.DueBefore - d.Covered ELSE 0 END";

    private const string ListSelect = """
        SELECT i.Id, i.InvoiceNumber, i.CustomerId, c.FullName AS CustomerName, i.CurrencyCode, i.IssueDate, i.DueDate, i.Status,
               i.Total, m.Paid, m.PendingPaid, m.Credited, m.Refunded,
               i.BookingId, bk.BookingNumber, i.VisaCaseId, vc.CaseNumber AS VisaCaseNumber, i.GroupId, gr.GroupNumber, i.CreatedAt
        """;

    private const string ListJoins = """
        FROM TravelInvoices i
        INNER JOIN Customers c ON c.Id = i.CustomerId AND c.TenantId = i.TenantId
        LEFT JOIN TravelBookings bk ON bk.Id = i.BookingId AND bk.TenantId = i.TenantId
        LEFT JOIN TravelVisaCases vc ON vc.Id = i.VisaCaseId AND vc.TenantId = i.TenantId
        LEFT JOIN TravelGroups gr ON gr.Id = i.GroupId AND gr.TenantId = i.TenantId
        """;

    internal const string CreditNoteSelect = """
        SELECT cn.Id, cn.CreditNoteNumber, cn.InvoiceId, iv.InvoiceNumber, cn.CustomerId, cu.FullName AS CustomerName, cn.Amount, cn.CurrencyCode,
               cn.Reason, cn.CancellationId, cn.Status, cn.IssuedOn, cn.VoidReason
        FROM TravelCreditNotes cn
        INNER JOIN TravelInvoices iv ON iv.Id = cn.InvoiceId AND iv.TenantId = cn.TenantId
        INNER JOIN Customers cu ON cu.Id = cn.CustomerId AND cu.TenantId = cn.TenantId
        """;

    internal const string RefundSelect = """
        SELECT r.Id, r.RefundNumber, r.CustomerId, cu.FullName AS CustomerName, r.InvoiceId, iv.InvoiceNumber, r.PaymentId, py.ReceiptNumber,
               r.Amount, r.CurrencyCode, r.Method, r.Reference, r.PaidOn, r.CancellationId, r.Notes
        FROM TravelRefunds r
        INNER JOIN Customers cu ON cu.Id = r.CustomerId AND cu.TenantId = r.TenantId
        LEFT JOIN TravelInvoices iv ON iv.Id = r.InvoiceId AND iv.TenantId = r.TenantId
        LEFT JOIN TravelPayments py ON py.Id = r.PaymentId AND py.TenantId = r.TenantId
        """;

    internal const string AllocationSelect = """
        SELECT a.PaymentId, p.ReceiptNumber, p.ReceivedOn, p.Method, p.Status AS PaymentStatus, a.InvoiceId, iv.InvoiceNumber,
               iv.CurrencyCode AS InvoiceCurrencyCode, a.AmountFromPayment, p.CurrencyCode AS PaymentCurrencyCode, a.ExchangeRate, a.AmountApplied
        FROM TravelPaymentAllocations a
        INNER JOIN TravelPayments p ON p.Id = a.PaymentId AND p.TenantId = a.TenantId
        INNER JOIN TravelInvoices iv ON iv.Id = a.InvoiceId AND iv.TenantId = a.TenantId
        """;

    internal const string HistorySelect = """
        SELECT h.Id, h.EventType, h.FromValue, h.ToValue, h.Amount, h.CurrencyCode, h.Note, u.FullName AS ChangedByName, h.ChangedAt
        FROM TravelFinanceHistory h
        LEFT JOIN Users u ON u.Id = h.ChangedBy AND u.TenantId = h.TenantId
        """;

    /// <summary>The source's live amount for an invoice line <c>l</c> (null when cancelled or without a price).</summary>
    private const string CurrentSourceAmount = """
        CASE l.SourceType
          WHEN N'Flight' THEN (SELECT CASE WHEN f.Status <> N'Cancelled' AND f.IsDeleted = 0 THEN f.SellAmount END
                               FROM TravelFlightBookings f WHERE f.Id = l.SourceId AND f.TenantId = l.TenantId)
          WHEN N'Hotel' THEN (SELECT CASE WHEN h.Status <> N'Cancelled' AND h.IsDeleted = 0 THEN h.SellAmount END
                              FROM TravelHotelBookings h WHERE h.Id = l.SourceId AND h.TenantId = l.TenantId)
          WHEN N'VisaServiceFee' THEN (SELECT CASE WHEN v.Status <> N'Cancelled' AND v.IsDeleted = 0 THEN v.QuotedServiceFee END
                                       FROM TravelVisaCases v WHERE v.Id = l.SourceId AND v.TenantId = l.TenantId)
          WHEN N'VisaEmbassyFee' THEN (SELECT CASE WHEN v.Status <> N'Cancelled' AND v.IsDeleted = 0 THEN v.QuotedEmbassyFee END
                                       FROM TravelVisaCases v WHERE v.Id = l.SourceId AND v.TenantId = l.TenantId)
          WHEN N'GroupMember' THEN (SELECT CASE WHEN gm.Status <> N'Cancelled' AND gm.IsDeleted = 0 THEN gm.QuotedPrice END
                                    FROM TravelGroupMembers gm WHERE gm.Id = l.SourceId AND gm.TenantId = l.TenantId)
        END
        """;

    // ── State / lists / detail ──────────────────────────────────────────────

    public async Task<TravelInvoiceState?> GetStateAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Id", id);
        p.Add("Today", DateTime.UtcNow.Date);
        var clauses = new List<string> { "i.TenantId = @TenantId", "i.Id = @Id", "i.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "i.BranchId", clauses, p);
        return await connection.QuerySingleOrDefaultAsync<TravelInvoiceState>(new CommandDefinition($"""
            SELECT i.Id, i.Status, i.CustomerId, i.CurrencyCode, i.InvoiceNumber, i.IssueDate, i.DueDate, i.BranchId,
                   i.Total, m.Paid, m.PendingPaid, m.Credited, m.Refunded,
                   (SELECT COUNT(1) FROM TravelPaymentAllocations a INNER JOIN TravelPayments py ON py.Id = a.PaymentId AND py.TenantId = a.TenantId
                     WHERE a.InvoiceId = i.Id AND a.TenantId = i.TenantId AND py.Status IN (N'Pending', N'Completed', N'Refunded')) AS AllocationCount,
                   (SELECT COUNT(1) FROM TravelCreditNotes cn WHERE cn.InvoiceId = i.Id AND cn.TenantId = i.TenantId AND cn.Status = N'Issued') AS CreditNoteCount,
                   (SELECT COUNT(1) FROM TravelRefunds r WHERE r.InvoiceId = i.Id AND r.TenantId = i.TenantId) AS RefundCount,
                   m.InstallmentCount,
                   COALESCE((SELECT SUM(ins.Amount) FROM TravelInvoiceInstallments ins WHERE ins.InvoiceId = i.Id AND ins.TenantId = i.TenantId), 0) AS InstallmentTotal,
                   (SELECT MIN(ins.DueDate) FROM TravelInvoiceInstallments ins WHERE ins.InvoiceId = i.Id AND ins.TenantId = i.TenantId) AS FirstInstallmentDate,
                   (SELECT MAX(ins.DueDate) FROM TravelInvoiceInstallments ins WHERE ins.InvoiceId = i.Id AND ins.TenantId = i.TenantId) AS LastInstallmentDate
            FROM TravelInvoices i
            {Money}
            WHERE {string.Join(" AND ", clauses)}
            """, p, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TravelInvoiceListItemDto>> GetPagedAsync(int tenantId, TravelBranchScope scope, TravelInvoiceListFilter f, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Today", f.Today.Date);
        var clauses = new List<string> { "i.TenantId = @TenantId", "i.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "i.BranchId", clauses, p);
        if (f.Search is not null) { clauses.Add("(i.InvoiceNumber LIKE @Search OR c.FullName LIKE @Search OR bk.BookingNumber LIKE @Search OR vc.CaseNumber LIKE @Search OR gr.GroupNumber LIKE @Search)"); p.Add("Search", $"%{TravelSql.EscapeLike(f.Search)}%"); }
        if (f.Status is not null) { clauses.Add("i.Status = @Status"); p.Add("Status", f.Status); }
        switch (f.PaymentStatus)
        {
            case "Unpaid": clauses.Add("i.Status = N'Issued' AND d.Balance > 0 AND d.Covered = 0"); break;
            case "PartiallyPaid": clauses.Add("i.Status = N'Issued' AND d.Balance > 0 AND d.Covered > 0"); break;
            case "Paid": clauses.Add("i.Status = N'Issued' AND d.Balance = 0"); break;
            case "CreditDue": clauses.Add("i.Status = N'Issued' AND d.Balance < 0"); break;
        }
        if (f.CustomerId.HasValue) { clauses.Add("i.CustomerId = @CustomerId"); p.Add("CustomerId", f.CustomerId); }
        if (f.CurrencyCode is not null) { clauses.Add("i.CurrencyCode = @Currency"); p.Add("Currency", f.CurrencyCode); }
        if (f.IssueFrom.HasValue) { clauses.Add("i.IssueDate >= @IssueFrom"); p.Add("IssueFrom", f.IssueFrom.Value.Date); }
        if (f.IssueTo.HasValue) { clauses.Add("i.IssueDate <= @IssueTo"); p.Add("IssueTo", f.IssueTo.Value.Date); }
        if (f.DueFrom.HasValue) { clauses.Add("i.DueDate >= @DueFrom"); p.Add("DueFrom", f.DueFrom.Value.Date); }
        if (f.DueTo.HasValue) { clauses.Add("i.DueDate <= @DueTo"); p.Add("DueTo", f.DueTo.Value.Date); }
        if (f.OverdueOnly) clauses.Add($"({Overdue}) > 0");

        var where = string.Join(" AND ", clauses);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(1) {ListJoins} {Money} WHERE {where}", p, cancellationToken: cancellationToken));
        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);
        var items = (await connection.QueryAsync<TravelInvoiceListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {ListJoins}
            {Money}
            WHERE {where}
            ORDER BY CASE i.Status WHEN N'Draft' THEN 0 ELSE 1 END, i.IssueDate DESC, i.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken))).ToList();
        return new PagedResult<TravelInvoiceListItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<TravelInvoiceListItemDto>> GetOpenInvoicesAsync(int tenantId, int customerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelInvoiceListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {ListJoins}
            {Money}
            WHERE i.TenantId = @TenantId AND i.CustomerId = @CustomerId AND i.IsDeleted = 0 AND i.Status = N'Issued' AND d.Balance - m.PendingPaid > 0
            ORDER BY i.DueDate, i.Id
            """, new { TenantId = tenantId, CustomerId = customerId, Today = DateTime.UtcNow.Date }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<TravelInstallmentDto>> GetInstallmentsAsync(IReadOnlyList<int> invoiceIds, int tenantId, CancellationToken cancellationToken = default)
    {
        if (invoiceIds.Count == 0) return [];
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelInstallmentDto>(new CommandDefinition("""
            SELECT InvoiceId, Sequence, DueDate, Amount FROM TravelInvoiceInstallments
            WHERE TenantId = @TenantId AND InvoiceId IN @Ids
            ORDER BY InvoiceId, DueDate, Sequence
            """, new { TenantId = tenantId, Ids = invoiceIds.ToArray() }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<TravelInvoiceDto?> GetDetailAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var args = new { Id = id, TenantId = tenantId, Today = DateTime.UtcNow.Date };
        var invoice = await connection.QuerySingleOrDefaultAsync<TravelInvoiceDto>(new CommandDefinition($"""
            {ListSelect},
                   i.Subtotal, i.DiscountAmount, i.Notes, i.IssuedAt, i.VoidedAt, i.VoidReason, i.BranchId, br.Name AS BranchName,
                   i.AgentUserId, ag.FullName AS AgentName, i.SubAgentId, sa.Name AS SubAgentName
            {ListJoins}
            LEFT JOIN Branches br ON br.Id = i.BranchId AND br.TenantId = i.TenantId
            LEFT JOIN Users ag ON ag.Id = i.AgentUserId AND ag.TenantId = i.TenantId
            LEFT JOIN TravelSubAgents sa ON sa.Id = i.SubAgentId AND sa.TenantId = i.TenantId
            {Money}
            WHERE i.Id = @Id AND i.TenantId = @TenantId AND i.IsDeleted = 0
            """, args, cancellationToken: cancellationToken));
        if (invoice is null) return null;

        invoice.Lines = (await connection.QueryAsync<TravelInvoiceLineDto>(new CommandDefinition($"""
            SELECT l.Id, l.InvoiceId, l.Sequence, l.Description, l.Quantity, l.UnitPrice, l.LineTotal, l.SourceType, l.SourceId,
                   {CurrentSourceAmount} AS CurrentSourceAmount
            FROM TravelInvoiceLines l
            WHERE l.InvoiceId = @Id AND l.TenantId = @TenantId
            ORDER BY l.Sequence
            """, args, cancellationToken: cancellationToken))).ToList();
        invoice.Installments = await GetInstallmentsAsync([id], tenantId, cancellationToken);
        invoice.Allocations = (await connection.QueryAsync<TravelInvoiceAllocationDto>(new CommandDefinition($"""
            {AllocationSelect}
            WHERE a.InvoiceId = @Id AND a.TenantId = @TenantId
            ORDER BY p.ReceivedOn, a.Id
            """, args, cancellationToken: cancellationToken))).ToList();
        invoice.CreditNotes = (await connection.QueryAsync<TravelCreditNoteDto>(new CommandDefinition($"""
            {CreditNoteSelect}
            WHERE cn.InvoiceId = @Id AND cn.TenantId = @TenantId
            ORDER BY cn.IssuedOn, cn.Id
            """, args, cancellationToken: cancellationToken))).ToList();
        invoice.Refunds = (await connection.QueryAsync<TravelRefundDto>(new CommandDefinition($"""
            {RefundSelect}
            WHERE r.InvoiceId = @Id AND r.TenantId = @TenantId
            ORDER BY r.PaidOn, r.Id
            """, args, cancellationToken: cancellationToken))).ToList();
        invoice.History = (await connection.QueryAsync<TravelFinanceHistoryDto>(new CommandDefinition($"""
            {HistorySelect}
            WHERE h.InvoiceId = @Id AND h.TenantId = @TenantId
            ORDER BY h.ChangedAt DESC, h.Id DESC
            """, args, cancellationToken: cancellationToken))).ToList();
        return invoice;
    }

    // ── Billable sources ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TravelBillableItemDto>> GetBillableAsync(int tenantId, TravelBranchScope scope, int? customerId, string? currencyCode,
        int? includeInvoiceId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("CustomerId", customerId);
        p.Add("Currency", currencyCode);
        p.Add("IncludeInvoiceId", includeInvoiceId);
        var clauses = new List<string> { "(ll.InvoiceId IS NULL OR ll.InvoiceId = @IncludeInvoiceId)", "(@CustomerId IS NULL OR x.CustomerId = @CustomerId)",
            "(@Currency IS NULL OR x.CurrencyCode = @Currency)" };
        TravelSql.ApplyBranchScope(scope, "x.BranchId", clauses, p);
        return (await connection.QueryAsync<TravelBillableItemDto>(new CommandDefinition($"""
            SELECT TOP (500) x.SourceType, x.SourceId, x.CustomerId, x.CustomerName, x.Description, x.Amount, x.CurrencyCode,
                   x.ContextType, x.ContextId, x.ContextLabel, x.ServiceDate, ll.InvoiceId AS OnInvoiceId
            FROM (
                -- Flights of customer-owned booking files (group files are billed through the pilgrims' package price)
                SELECT N'Flight' AS SourceType, f.Id AS SourceId, b.CustomerId, c.FullName AS CustomerName,
                       CONCAT(N'Flight', CASE WHEN f.Pnr IS NOT NULL THEN CONCAT(N' · PNR ', f.Pnr) ELSE N'' END,
                              CASE WHEN rt.Route IS NOT NULL THEN CONCAT(N' · ', rt.Route) ELSE N'' END,
                              N' · ', (SELECT COUNT(1) FROM TravelFlightPassengers fp WHERE fp.FlightBookingId = f.Id AND fp.TenantId = f.TenantId
                                       AND fp.TicketStatus IN (N'NotIssued', N'Issued')), N' passenger(s)') AS Description,
                       f.SellAmount AS Amount, f.CurrencyCode, N'Booking' AS ContextType, b.Id AS ContextId,
                       CONCAT(b.BookingNumber, N' · ', b.Title) AS ContextLabel, CAST(rt.FirstDepart AS DATE) AS ServiceDate, b.BranchId
                FROM TravelFlightBookings f
                INNER JOIN TravelBookings b ON b.Id = f.BookingId AND b.TenantId = f.TenantId AND b.IsDeleted = 0 AND b.Status <> N'Cancelled' AND b.CustomerId IS NOT NULL
                INNER JOIN Customers c ON c.Id = b.CustomerId AND c.TenantId = b.TenantId
                OUTER APPLY (SELECT STRING_AGG(CONCAT(s.FromAirport, N'→', s.ToAirport), N', ') WITHIN GROUP (ORDER BY s.Sequence) AS Route,
                                    MIN(s.DepartLocal) AS FirstDepart
                             FROM TravelFlightSegments s WHERE s.FlightBookingId = f.Id AND s.TenantId = f.TenantId) rt
                WHERE f.TenantId = @TenantId AND f.IsDeleted = 0 AND f.Status <> N'Cancelled' AND f.SellAmount > 0 AND f.CurrencyCode IS NOT NULL
                UNION ALL
                SELECT N'Hotel', h.Id, b.CustomerId, c.FullName,
                       CONCAT(N'Hotel ', ht.Name, N', ', ht.City, N' · ', DATEDIFF(DAY, h.CheckIn, h.CheckOut), N' night(s)',
                              CASE WHEN rm.Rooms IS NOT NULL THEN CONCAT(N' · ', rm.Rooms) ELSE N'' END),
                       h.SellAmount, h.CurrencyCode, N'Booking', b.Id, CONCAT(b.BookingNumber, N' · ', b.Title), h.CheckIn, b.BranchId
                FROM TravelHotelBookings h
                INNER JOIN TravelBookings b ON b.Id = h.BookingId AND b.TenantId = h.TenantId AND b.IsDeleted = 0 AND b.Status <> N'Cancelled' AND b.CustomerId IS NOT NULL
                INNER JOIN Customers c ON c.Id = b.CustomerId AND c.TenantId = b.TenantId
                INNER JOIN TravelHotels ht ON ht.Id = h.HotelId AND ht.TenantId = h.TenantId
                OUTER APPLY (SELECT STRING_AGG(CONCAT(r.Quantity, N'× ', r.RoomType), N', ') AS Rooms
                             FROM TravelHotelBookingRooms r WHERE r.HotelBookingId = h.Id AND r.TenantId = h.TenantId) rm
                WHERE h.TenantId = @TenantId AND h.IsDeleted = 0 AND h.Status <> N'Cancelled' AND h.SellAmount > 0 AND h.CurrencyCode IS NOT NULL
                UNION ALL
                SELECT N'VisaServiceFee', v.Id, v.CustomerId, c.FullName,
                       CONCAT(N'Visa service fee · ', co.Name, N' ', vt.Name, N' · ', ap.Applicants, N' applicant(s)'),
                       v.QuotedServiceFee, v.CurrencyCode, N'VisaCase', v.Id, v.CaseNumber, v.IntendedTravelDate, v.BranchId
                FROM TravelVisaCases v
                INNER JOIN Customers c ON c.Id = v.CustomerId AND c.TenantId = v.TenantId
                INNER JOIN TravelVisaTypes vt ON vt.Id = v.VisaTypeId AND vt.TenantId = v.TenantId
                INNER JOIN TravelCountries co ON co.Code = v.CountryCode
                OUTER APPLY (SELECT COUNT(1) AS Applicants FROM TravelVisaApplicants a WHERE a.VisaCaseId = v.Id AND a.TenantId = v.TenantId AND a.IsDeleted = 0) ap
                WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND v.Status <> N'Cancelled' AND v.CustomerId IS NOT NULL
                  AND v.QuotedServiceFee > 0 AND v.CurrencyCode IS NOT NULL
                UNION ALL
                SELECT N'VisaEmbassyFee', v.Id, v.CustomerId, c.FullName,
                       CONCAT(N'Embassy fee · ', co.Name, N' ', vt.Name, N' · ', ap.Applicants, N' applicant(s)'),
                       v.QuotedEmbassyFee, v.CurrencyCode, N'VisaCase', v.Id, v.CaseNumber, v.IntendedTravelDate, v.BranchId
                FROM TravelVisaCases v
                INNER JOIN Customers c ON c.Id = v.CustomerId AND c.TenantId = v.TenantId
                INNER JOIN TravelVisaTypes vt ON vt.Id = v.VisaTypeId AND vt.TenantId = v.TenantId
                INNER JOIN TravelCountries co ON co.Code = v.CountryCode
                OUTER APPLY (SELECT COUNT(1) AS Applicants FROM TravelVisaApplicants a WHERE a.VisaCaseId = v.Id AND a.TenantId = v.TenantId AND a.IsDeleted = 0) ap
                WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND v.Status <> N'Cancelled' AND v.CustomerId IS NOT NULL
                  AND v.QuotedEmbassyFee > 0 AND v.CurrencyCode IS NOT NULL
                UNION ALL
                -- Pilgrims, billed to their booking party at the quoted package price
                SELECT N'GroupMember', m.Id, m.CustomerId, c.FullName,
                       CONCAT(pk.Name, N' · ', t.GivenNames, N' ', t.Surname, N' (', m.PriceCategory, N', ', m.RoomPreference, N')'),
                       m.QuotedPrice, m.CurrencyCode, N'Group', g.Id, CONCAT(g.GroupNumber, N' · ', g.Name), g.DepartureDate, g.BranchId
                FROM TravelGroupMembers m
                INNER JOIN TravelGroups g ON g.Id = m.GroupId AND g.TenantId = m.TenantId AND g.IsDeleted = 0 AND g.Status <> N'Cancelled'
                INNER JOIN TravelPackages pk ON pk.Id = g.PackageId AND pk.TenantId = g.TenantId
                INNER JOIN TravelTravelers t ON t.Id = m.TravelerId AND t.TenantId = m.TenantId
                INNER JOIN Customers c ON c.Id = m.CustomerId AND c.TenantId = m.TenantId
                WHERE m.TenantId = @TenantId AND m.IsDeleted = 0 AND m.Status <> N'Cancelled' AND m.QuotedPrice > 0 AND m.CurrencyCode IS NOT NULL
            ) x
            OUTER APPLY (SELECT TOP (1) l.InvoiceId FROM TravelInvoiceLines l
                         WHERE l.TenantId = @TenantId AND l.SourceType = x.SourceType AND l.SourceId = x.SourceId AND l.IsVoided = 0) ll
            WHERE {string.Join(" AND ", clauses)}
            ORDER BY x.CustomerName, x.ContextLabel, x.ServiceDate, x.SourceType, x.SourceId
            """, p, cancellationToken: cancellationToken))).ToList();
    }

    // ── Writes ──────────────────────────────────────────────────────────────

    public async Task<int> CreateAsync(int tenantId, TravelInvoiceHeaderWrite h, IReadOnlyList<TravelInvoiceLineWrite> lines,
        IReadOnlyList<SaveTravelInstallmentDto> installments, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                INSERT INTO TravelInvoices (TenantId, CustomerId, BookingId, VisaCaseId, GroupId, CurrencyCode, IssueDate, DueDate, Subtotal,
                    DiscountAmount, Total, Status, BranchId, Notes, AgentUserId, SubAgentId, CreatedBy)
                SELECT @TenantId, c.Id, @BookingId, @VisaCaseId, @GroupId, @CurrencyCode, @IssueDate, @DueDate, @Subtotal,
                    @DiscountAmount, @Total, N'Draft', @BranchId, @Notes,
                    COALESCE(@AgentUserId,
                        (SELECT bk.AssignedAgentUserId FROM TravelBookings bk WHERE bk.Id = @BookingId AND bk.TenantId = @TenantId),
                        (SELECT vc.AssignedAgentUserId FROM TravelVisaCases vc WHERE vc.Id = @VisaCaseId AND vc.TenantId = @TenantId),
                        cp.AssignedAgentUserId),
                    COALESCE(@SubAgentId, (SELECT sa.Id FROM TravelSubAgents sa WHERE sa.Id = cp.SubAgentId AND sa.TenantId = @TenantId AND sa.IsDeleted = 0)),
                    @UserId
                FROM Customers c
                LEFT JOIN TravelCustomerProfiles cp ON cp.CustomerId = c.Id AND cp.TenantId = c.TenantId AND cp.IsDeleted = 0
                WHERE c.Id = @CustomerId AND c.TenantId = @TenantId;
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, HeaderArgs(tenantId, h, userId), tx, cancellationToken: cancellationToken)) ?? 0;
            if (id == 0)
            {
                tx.Rollback();
                return 0;
            }
            await WriteChildrenAsync(connection, tx, tenantId, id, lines, installments, cancellationToken);
            await HistoryAsync(connection, tx, tenantId, id, "InvoiceCreated", null, "Draft", h.Total, h.CurrencyCode, null, userId, cancellationToken);
            tx.Commit();
            return id;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // UX_TravelInvoiceLines_Tenant_Source: an item was put on another invoice meanwhile.
            tx.Rollback();
            return 0;
        }
    }

    public async Task<bool> UpdateDraftAsync(int tenantId, int id, TravelInvoiceHeaderWrite h, IReadOnlyList<TravelInvoiceLineWrite> lines,
        IReadOnlyList<SaveTravelInstallmentDto> installments, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var args = HeaderArgs(tenantId, h, userId);
            args.Add("Id", id);
            var rows = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE TravelInvoices SET BookingId = @BookingId, VisaCaseId = @VisaCaseId, GroupId = @GroupId, IssueDate = @IssueDate, DueDate = @DueDate,
                    Subtotal = @Subtotal, DiscountAmount = @DiscountAmount, Total = @Total, Notes = @Notes,
                    AgentUserId = COALESCE(@AgentUserId, AgentUserId), SubAgentId = COALESCE(@SubAgentId, SubAgentId), UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Draft'
                """, args, tx, cancellationToken: cancellationToken));
            if (rows == 0)
            {
                tx.Rollback();
                return false;
            }
            await connection.ExecuteAsync(new CommandDefinition("""
                DELETE FROM TravelInvoiceLines WHERE InvoiceId = @Id AND TenantId = @TenantId;
                DELETE FROM TravelInvoiceInstallments WHERE InvoiceId = @Id AND TenantId = @TenantId;
                """, new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
            await WriteChildrenAsync(connection, tx, tenantId, id, lines, installments, cancellationToken);
            await HistoryAsync(connection, tx, tenantId, id, "InvoiceUpdated", null, null, h.Total, h.CurrencyCode, null, userId, cancellationToken);
            tx.Commit();
            return true;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            tx.Rollback();
            return false;
        }
    }

    public async Task<string?> IssueAsync(int tenantId, int id, DateTime issueDate, DateTime dueDate, int? userId, DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var locked = await connection.QuerySingleOrDefaultAsync<(int Id, decimal Total, string CurrencyCode)?>(new CommandDefinition("""
            SELECT Id, Total, CurrencyCode FROM TravelInvoices WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Draft'
            """, new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (locked is null)
        {
            tx.Rollback();
            return null;
        }
        var number = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.Invoice, utcNow, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelInvoices SET Status = N'Issued', InvoiceNumber = @Number, IssueDate = @IssueDate, DueDate = @DueDate,
                IssuedAt = @Now, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND Status = N'Draft'
            """, new { Id = id, TenantId = tenantId, Number = number, IssueDate = issueDate.Date, DueDate = dueDate.Date, Now = utcNow },
            tx, cancellationToken: cancellationToken));
        await HistoryAsync(connection, tx, tenantId, id, "InvoiceIssued", "Draft", number, locked.Value.Total, locked.Value.CurrencyCode, null, userId, cancellationToken);
        tx.Commit();
        return number;
    }

    public async Task<bool> VoidAsync(int tenantId, int id, string reason, int? userId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var args = new { Id = id, TenantId = tenantId, Reason = reason, Now = utcNow };
        var locked = await connection.QuerySingleOrDefaultAsync<(decimal Total, string CurrencyCode, int Movements)?>(new CommandDefinition("""
            SELECT i.Total, i.CurrencyCode,
                   (SELECT COUNT(1) FROM TravelPaymentAllocations a INNER JOIN TravelPayments p ON p.Id = a.PaymentId AND p.TenantId = a.TenantId
                     WHERE a.InvoiceId = i.Id AND a.TenantId = i.TenantId AND p.Status IN (N'Pending', N'Completed', N'Refunded'))
                 + (SELECT COUNT(1) FROM TravelCreditNotes cn WHERE cn.InvoiceId = i.Id AND cn.TenantId = i.TenantId AND cn.Status = N'Issued')
                 + (SELECT COUNT(1) FROM TravelRefunds r WHERE r.InvoiceId = i.Id AND r.TenantId = i.TenantId) AS Movements
            FROM TravelInvoices i WITH (UPDLOCK, ROWLOCK)
            WHERE i.Id = @Id AND i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.Status = N'Issued'
            """, args, tx, cancellationToken: cancellationToken));
        if (locked is null || locked.Value.Movements > 0)
        {
            tx.Rollback();
            return false;
        }
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelInvoices SET Status = N'Void', VoidReason = @Reason, VoidedAt = @Now, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId;
            UPDATE TravelInvoiceLines SET IsVoided = 1 WHERE InvoiceId = @Id AND TenantId = @TenantId;
            """, args, tx, cancellationToken: cancellationToken));
        await HistoryAsync(connection, tx, tenantId, id, "InvoiceVoided", "Issued", "Void", locked.Value.Total, locked.Value.CurrencyCode, reason, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    public async Task<bool> DeleteDraftAsync(int tenantId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelInvoices SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Draft'
            """, new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }
        // Freeing the lines makes their items billable again.
        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM TravelInvoiceLines WHERE InvoiceId = @Id AND TenantId = @TenantId;
            DELETE FROM TravelInvoiceInstallments WHERE InvoiceId = @Id AND TenantId = @TenantId;
            """, new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        tx.Commit();
        return true;
    }

    // ── Reports ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TravelFinanceCurrencySummaryDto>> GetCurrencySummaryAsync(int tenantId, TravelBranchScope scope, DateTime today,
        DateTime monthStart, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Today", today.Date);
        p.Add("MonthStart", monthStart.Date);
        var invoiceClauses = new List<string> { "i.TenantId = @TenantId", "i.IsDeleted = 0", "i.Status = N'Issued'" };
        TravelSql.ApplyBranchScope(scope, "i.BranchId", invoiceClauses, p);
        var paymentClauses = new List<string> { "py.TenantId = @TenantId", "py.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "py.BranchId", paymentClauses, p);

        var invoiceRows = (await connection.QueryAsync<(string CurrencyCode, decimal Outstanding, decimal CreditDue, decimal Overdue)>(new CommandDefinition($"""
            SELECT i.CurrencyCode,
                   SUM(CASE WHEN d.Balance > 0 THEN d.Balance ELSE 0 END),
                   SUM(CASE WHEN d.Balance < 0 THEN -d.Balance ELSE 0 END),
                   SUM({Overdue})
            FROM TravelInvoices i
            {Money}
            WHERE i.TenantId = @TenantId AND {string.Join(" AND ", invoiceClauses)}
            GROUP BY i.CurrencyCode
            """, p, cancellationToken: cancellationToken))).ToList();
        var paymentRows = (await connection.QueryAsync<(string CurrencyCode, decimal Collected, decimal Unallocated)>(new CommandDefinition($"""
            SELECT py.CurrencyCode,
                   SUM(CASE WHEN py.Status = N'Completed' AND py.ReceivedOn >= @MonthStart AND py.ReceivedOn <= @Today THEN py.Amount ELSE 0 END),
                   SUM(CASE WHEN py.Status = N'Completed' THEN py.Amount - pm.Allocated - pm.Refunded ELSE 0 END)
            FROM TravelPayments py
            {TravelPaymentRepository.PaymentMoney}
            WHERE py.TenantId = @TenantId AND {string.Join(" AND ", paymentClauses)}
            GROUP BY py.CurrencyCode
            """, p, cancellationToken: cancellationToken))).ToList();

        var currencies = invoiceRows.Select(r => r.CurrencyCode).Union(paymentRows.Select(r => r.CurrencyCode)).Distinct();
        return currencies.Select(code =>
        {
            var inv = invoiceRows.FirstOrDefault(r => r.CurrencyCode == code);
            var pay = paymentRows.FirstOrDefault(r => r.CurrencyCode == code);
            return new TravelFinanceCurrencySummaryDto
            {
                CurrencyCode = code, Outstanding = inv.Outstanding, CreditDue = inv.CreditDue, Overdue = inv.Overdue,
                CollectedThisMonth = pay.Collected, UnallocatedAdvances = pay.Unallocated
            };
        }).ToList();
    }

    public async Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "i.TenantId = @TenantId", "i.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "i.BranchId", clauses, p);
        var rows = await connection.QueryAsync<(string Status, int Total)>(new CommandDefinition(
            $"SELECT i.Status, COUNT(1) FROM TravelInvoices i WHERE {string.Join(" AND ", clauses)} GROUP BY i.Status", p, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.Status, r => r.Total);
    }

    public async Task<IReadOnlyList<TravelCustomerBalanceDto>> GetCustomerBalancesAsync(int tenantId, TravelBranchScope scope, int? customerId, DateTime today,
        bool openOnly, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Today", today.Date);
        p.Add("CustomerId", customerId);
        var invoiceClauses = new List<string> { "i.TenantId = @TenantId", "i.IsDeleted = 0", "i.Status = N'Issued'", "(@CustomerId IS NULL OR i.CustomerId = @CustomerId)" };
        TravelSql.ApplyBranchScope(scope, "i.BranchId", invoiceClauses, p);
        var paymentClauses = new List<string> { "py.TenantId = @TenantId", "py.IsDeleted = 0", "py.Status = N'Completed'", "(@CustomerId IS NULL OR py.CustomerId = @CustomerId)" };
        TravelSql.ApplyBranchScope(scope, "py.BranchId", paymentClauses, p);

        var invoices = (await connection.QueryAsync<TravelCustomerBalanceDto>(new CommandDefinition($"""
            SELECT i.CustomerId, c.FullName AS CustomerName, i.CurrencyCode,
                   SUM(CASE WHEN d.Balance <> 0 THEN 1 ELSE 0 END) AS OpenInvoices,
                   SUM(i.Total) AS Invoiced, SUM(m.Paid) AS Paid, SUM(m.Credited) AS Credited, SUM(m.Refunded) AS Refunded,
                   SUM(d.Balance) AS Balance, SUM({Overdue}) AS Overdue
            FROM TravelInvoices i
            INNER JOIN Customers c ON c.Id = i.CustomerId AND c.TenantId = i.TenantId
            {Money}
            WHERE {string.Join(" AND ", invoiceClauses)}
            GROUP BY i.CustomerId, c.FullName, i.CurrencyCode
            """, p, cancellationToken: cancellationToken))).ToList();
        var advances = (await connection.QueryAsync<(int CustomerId, string CustomerName, string CurrencyCode, decimal Unallocated)>(new CommandDefinition($"""
            SELECT py.CustomerId, c.FullName, py.CurrencyCode, SUM(py.Amount - pm.Allocated - pm.Refunded)
            FROM TravelPayments py
            INNER JOIN Customers c ON c.Id = py.CustomerId AND c.TenantId = py.TenantId
            {TravelPaymentRepository.PaymentMoney}
            WHERE {string.Join(" AND ", paymentClauses)}
            GROUP BY py.CustomerId, c.FullName, py.CurrencyCode
            HAVING SUM(py.Amount - pm.Allocated - pm.Refunded) > 0
            """, p, cancellationToken: cancellationToken))).ToList();

        foreach (var a in advances)
        {
            var row = invoices.FirstOrDefault(r => r.CustomerId == a.CustomerId && r.CurrencyCode == a.CurrencyCode);
            if (row is null)
                invoices.Add(new TravelCustomerBalanceDto { CustomerId = a.CustomerId, CustomerName = a.CustomerName, CurrencyCode = a.CurrencyCode, UnallocatedAdvances = a.Unallocated });
            else row.UnallocatedAdvances = a.Unallocated;
        }
        return invoices.Where(r => !openOnly || r.Balance != 0 || r.UnallocatedAdvances > 0)
            .OrderByDescending(r => r.Overdue).ThenByDescending(r => r.Balance).ThenBy(r => r.CustomerName).ToList();
    }

    public async Task<IReadOnlyList<TravelStatementMovement>> GetStatementMovementsAsync(int tenantId, int customerId, string currencyCode,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelStatementMovement>(new CommandDefinition("""
            SELECT i.IssueDate AS [Date], N'Invoice' AS EntryType, i.InvoiceNumber AS Reference,
                   CONCAT(N'Invoice', CASE WHEN i.Notes IS NOT NULL THEN CONCAT(N' · ', LEFT(i.Notes, 80)) ELSE N'' END) AS Description,
                   i.Total AS Debit, CAST(0 AS DECIMAL(18,2)) AS Credit, i.Id AS InvoiceId, CAST(NULL AS INT) AS PaymentId, 1 AS [Order]
            FROM TravelInvoices i
            WHERE i.TenantId = @TenantId AND i.CustomerId = @CustomerId AND i.CurrencyCode = @Currency AND i.IsDeleted = 0 AND i.Status = N'Issued'
            UNION ALL
            SELECT cn.IssuedOn, N'CreditNote', cn.CreditNoteNumber, CONCAT(N'Credit note on ', iv.InvoiceNumber, N' · ', LEFT(cn.Reason, 80)),
                   0, cn.Amount, cn.InvoiceId, NULL, 2
            FROM TravelCreditNotes cn
            INNER JOIN TravelInvoices iv ON iv.Id = cn.InvoiceId AND iv.TenantId = cn.TenantId
            WHERE cn.TenantId = @TenantId AND cn.CustomerId = @CustomerId AND cn.CurrencyCode = @Currency AND cn.Status = N'Issued' AND iv.Status = N'Issued'
            UNION ALL
            SELECT p.ReceivedOn, N'Payment', p.ReceiptNumber,
                   CONCAT(N'Payment (', p.Method, N') applied to ', iv.InvoiceNumber,
                          CASE WHEN p.CurrencyCode <> iv.CurrencyCode THEN CONCAT(N' · ', a.AmountFromPayment, N' ', p.CurrencyCode, N' at ', a.ExchangeRate) ELSE N'' END),
                   0, a.AmountApplied, a.InvoiceId, a.PaymentId, 3
            FROM TravelPaymentAllocations a
            INNER JOIN TravelPayments p ON p.Id = a.PaymentId AND p.TenantId = a.TenantId
            INNER JOIN TravelInvoices iv ON iv.Id = a.InvoiceId AND iv.TenantId = a.TenantId
            WHERE a.TenantId = @TenantId AND p.CustomerId = @CustomerId AND iv.CurrencyCode = @Currency AND p.IsDeleted = 0
              AND p.Status IN (N'Completed', N'Refunded') AND iv.Status = N'Issued'
            UNION ALL
            SELECT r.PaidOn, N'Refund', r.RefundNumber, CONCAT(N'Refund (', r.Method, N') on ', iv.InvoiceNumber), r.Amount, 0, r.InvoiceId, NULL, 4
            FROM TravelRefunds r
            INNER JOIN TravelInvoices iv ON iv.Id = r.InvoiceId AND iv.TenantId = r.TenantId
            WHERE r.TenantId = @TenantId AND r.CustomerId = @CustomerId AND r.CurrencyCode = @Currency
            UNION ALL
            -- Advances: the part of a cleared payment in this currency not applied to any invoice
            SELECT p.ReceivedOn, N'Advance', p.ReceiptNumber, CONCAT(N'Payment on account (', p.Method, N')'),
                   0, p.Amount - COALESCE((SELECT SUM(a.AmountFromPayment) FROM TravelPaymentAllocations a WHERE a.PaymentId = p.Id AND a.TenantId = p.TenantId), 0),
                   NULL, p.Id, 3
            FROM TravelPayments p
            WHERE p.TenantId = @TenantId AND p.CustomerId = @CustomerId AND p.CurrencyCode = @Currency AND p.IsDeleted = 0
              AND p.Status IN (N'Completed', N'Refunded')
              AND p.Amount - COALESCE((SELECT SUM(a.AmountFromPayment) FROM TravelPaymentAllocations a WHERE a.PaymentId = p.Id AND a.TenantId = p.TenantId), 0) > 0
            UNION ALL
            SELECT r.PaidOn, N'Refund', r.RefundNumber, CONCAT(N'Advance refunded (', r.Method, N') from ', py.ReceiptNumber), r.Amount, 0, NULL, r.PaymentId, 4
            FROM TravelRefunds r
            INNER JOIN TravelPayments py ON py.Id = r.PaymentId AND py.TenantId = r.TenantId
            WHERE r.TenantId = @TenantId AND r.CustomerId = @CustomerId AND r.CurrencyCode = @Currency
            """, new { TenantId = tenantId, CustomerId = customerId, Currency = currencyCode }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<string>> GetCustomerCurrenciesAsync(int tenantId, int customerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT CurrencyCode FROM TravelInvoices WHERE TenantId = @TenantId AND CustomerId = @CustomerId AND IsDeleted = 0 AND Status = N'Issued'
            UNION
            SELECT CurrencyCode FROM TravelPayments WHERE TenantId = @TenantId AND CustomerId = @CustomerId AND IsDeleted = 0 AND Status IN (N'Completed', N'Refunded')
            ORDER BY CurrencyCode
            """, new { TenantId = tenantId, CustomerId = customerId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<TravelChangedInvoiceLineDto>> GetChangedLinesAsync(int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "i.TenantId = @TenantId", "i.IsDeleted = 0", "i.Status = N'Issued'", "l.SourceType IS NOT NULL" };
        TravelSql.ApplyBranchScope(scope, "i.BranchId", clauses, p);
        return (await connection.QueryAsync<TravelChangedInvoiceLineDto>(new CommandDefinition($"""
            SELECT TOP (500) i.Id AS InvoiceId, i.InvoiceNumber, c.FullName AS CustomerName, i.CurrencyCode, l.Description, l.SourceType,
                   l.LineTotal AS Invoiced, cur.Amount AS Current
            FROM TravelInvoiceLines l
            INNER JOIN TravelInvoices i ON i.Id = l.InvoiceId AND i.TenantId = l.TenantId
            INNER JOIN Customers c ON c.Id = i.CustomerId AND c.TenantId = i.TenantId
            CROSS APPLY (SELECT {CurrentSourceAmount} AS Amount) cur
            WHERE {string.Join(" AND ", clauses)} AND (cur.Amount IS NULL OR cur.Amount <> l.LineTotal)
            ORDER BY i.IssueDate DESC, i.Id DESC, l.Sequence
            """, p, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<TravelGroupFinanceRowDto>> GetGroupFinanceAsync(int tenantId, int groupId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var args = new { TenantId = tenantId, GroupId = groupId, Today = DateTime.UtcNow.Date };
        var members = (await connection.QueryAsync<TravelGroupFinanceRowDto>(new CommandDefinition("""
            SELECT m.CustomerId, c.FullName AS CustomerName, COALESCE(m.CurrencyCode, pk.CurrencyCode) AS CurrencyCode,
                   COUNT(1) AS Pilgrims, COALESCE(SUM(m.QuotedPrice), 0) AS Quoted,
                   SUM(CASE WHEN NOT EXISTS (
                         SELECT 1 FROM TravelInvoiceLines l INNER JOIN TravelInvoices iv ON iv.Id = l.InvoiceId AND iv.TenantId = l.TenantId
                         WHERE l.TenantId = m.TenantId AND l.SourceType = N'GroupMember' AND l.SourceId = m.Id AND l.IsVoided = 0 AND iv.Status = N'Issued')
                       THEN 1 ELSE 0 END) AS UninvoicedPilgrims
            FROM TravelGroupMembers m
            INNER JOIN TravelGroups g ON g.Id = m.GroupId AND g.TenantId = m.TenantId
            INNER JOIN TravelPackages pk ON pk.Id = g.PackageId AND pk.TenantId = g.TenantId
            INNER JOIN Customers c ON c.Id = m.CustomerId AND c.TenantId = m.TenantId
            WHERE m.GroupId = @GroupId AND m.TenantId = @TenantId AND m.IsDeleted = 0 AND m.Status <> N'Cancelled'
            GROUP BY m.CustomerId, c.FullName, COALESCE(m.CurrencyCode, pk.CurrencyCode)
            """, args, cancellationToken: cancellationToken))).ToList();
        var invoices = (await connection.QueryAsync<(int CustomerId, string CurrencyCode, decimal Invoiced, decimal Paid, decimal Credited, decimal Balance)>(new CommandDefinition($"""
            SELECT i.CustomerId, i.CurrencyCode, SUM(i.Total), SUM(m.Paid - m.Refunded), SUM(m.Credited), SUM(d.Balance)
            FROM TravelInvoices i
            {Money}
            WHERE i.GroupId = @GroupId AND i.TenantId = @TenantId AND i.IsDeleted = 0 AND i.Status = N'Issued'
            GROUP BY i.CustomerId, i.CurrencyCode
            """, args, cancellationToken: cancellationToken))).ToList();
        foreach (var row in members)
        {
            var inv = invoices.FirstOrDefault(x => x.CustomerId == row.CustomerId && x.CurrencyCode == row.CurrencyCode);
            row.Invoiced = inv.Invoiced;
            row.Paid = inv.Paid;
            row.Credited = inv.Credited;
            row.Balance = inv.Balance;
        }
        return members.OrderBy(r => r.CustomerName).ToList();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static DynamicParameters HeaderArgs(int tenantId, TravelInvoiceHeaderWrite h, int? userId)
    {
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("CustomerId", h.CustomerId);
        p.Add("BookingId", h.BookingId);
        p.Add("VisaCaseId", h.VisaCaseId);
        p.Add("GroupId", h.GroupId);
        p.Add("CurrencyCode", h.CurrencyCode);
        p.Add("IssueDate", h.IssueDate);
        p.Add("DueDate", h.DueDate);
        p.Add("Subtotal", h.Subtotal);
        p.Add("DiscountAmount", h.DiscountAmount);
        p.Add("Total", h.Total);
        p.Add("BranchId", h.BranchId);
        p.Add("Notes", h.Notes);
        p.Add("AgentUserId", h.AgentUserId);
        p.Add("SubAgentId", h.SubAgentId);
        p.Add("UserId", userId);
        return p;
    }

    private static async Task WriteChildrenAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int invoiceId, IReadOnlyList<TravelInvoiceLineWrite> lines,
        IReadOnlyList<SaveTravelInstallmentDto> installments, CancellationToken ct)
    {
        foreach (var l in lines)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TravelInvoiceLines (TenantId, InvoiceId, Sequence, Description, Quantity, UnitPrice, LineTotal, SourceType, SourceId)
                VALUES (@TenantId, @InvoiceId, @Sequence, @Description, @Quantity, @UnitPrice, @LineTotal, @SourceType, @SourceId)
                """, new { TenantId = tenantId, InvoiceId = invoiceId, l.Sequence, l.Description, l.Quantity, l.UnitPrice, l.LineTotal, l.SourceType, l.SourceId },
                tx, cancellationToken: ct));
        }
        var sequence = 1;
        foreach (var i in installments.OrderBy(i => i.DueDate))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TravelInvoiceInstallments (TenantId, InvoiceId, Sequence, DueDate, Amount) VALUES (@TenantId, @InvoiceId, @Sequence, @DueDate, @Amount)
                """, new { TenantId = tenantId, InvoiceId = invoiceId, Sequence = sequence++, DueDate = i.DueDate.Date, Amount = Math.Round(i.Amount, 2, MidpointRounding.AwayFromZero) },
                tx, cancellationToken: ct));
        }
    }

    internal static Task HistoryAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int invoiceId, string eventType, string? from, string? to,
        decimal? amount, string? currency, string? note, int? userId, CancellationToken ct, int? paymentId = null, int? creditNoteId = null, int? refundId = null)
        => connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TravelFinanceHistory (TenantId, InvoiceId, PaymentId, CreditNoteId, RefundId, EventType, FromValue, ToValue, Amount, CurrencyCode, Note, ChangedBy)
            VALUES (@TenantId, @InvoiceId, @PaymentId, @CreditNoteId, @RefundId, @EventType, LEFT(@From, 100), LEFT(@To, 100), @Amount, @Currency, LEFT(@Note, 1000), @UserId)
            """, new
            {
                TenantId = tenantId, InvoiceId = invoiceId == 0 ? (int?)null : invoiceId, PaymentId = paymentId, CreditNoteId = creditNoteId, RefundId = refundId,
                EventType = eventType, From = from, To = to, Amount = amount, Currency = currency, Note = note, UserId = userId
            }, tx, cancellationToken: ct));
}
