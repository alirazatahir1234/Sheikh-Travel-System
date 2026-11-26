using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>
/// Supplier bills and payables reports. Root predicate <c>b.TenantId = @TenantId</c>; every subquery and join repeats the tenant
/// predicate. Paid / credited / refunded are always computed from their source rows (<see cref="Money"/>).
/// </summary>
public sealed class TravelSupplierBillRepository(IDbConnectionFactory dbFactory) : ITravelSupplierBillRepository
{
    /// <summary>Per bill <c>b</c>: money (m) and balance (d). Balance = Net − Credited − (Paid − RefundsReceived).</summary>
    internal const string Money = """
        OUTER APPLY (
            SELECT
              COALESCE((SELECT SUM(a.AmountApplied) FROM TravelSupplierPaymentAllocations a
                INNER JOIN TravelSupplierPayments p ON p.Id = a.PaymentId AND p.TenantId = a.TenantId
                WHERE a.BillId = b.Id AND a.TenantId = b.TenantId AND p.IsDeleted = 0 AND p.Status IN (N'Completed', N'Refunded')), 0) AS Paid,
              COALESCE((SELECT SUM(a.AmountApplied) FROM TravelSupplierPaymentAllocations a
                INNER JOIN TravelSupplierPayments p ON p.Id = a.PaymentId AND p.TenantId = a.TenantId
                WHERE a.BillId = b.Id AND a.TenantId = b.TenantId AND p.IsDeleted = 0 AND p.Status = N'Pending'), 0) AS PendingPaid,
              COALESCE((SELECT SUM(sc.Amount) FROM TravelSupplierCredits sc
                WHERE sc.BillId = b.Id AND sc.TenantId = b.TenantId AND sc.Status = N'Issued'), 0) AS Credited,
              COALESCE((SELECT SUM(sr.Amount) FROM TravelSupplierRefunds sr WHERE sr.BillId = b.Id AND sr.TenantId = b.TenantId), 0) AS Refunded
        ) m
        CROSS APPLY (SELECT b.Net - m.Credited - (m.Paid - m.Refunded) AS Balance) d
        """;

    private const string ListSelect = """
        SELECT b.Id, b.BillNumber, b.SupplierId, s.Name AS SupplierName, b.SupplierReference, b.CurrencyCode, b.BillDate, b.DueDate, b.Gross, b.CommissionTotal,
               b.Status, b.Net AS Total, m.Paid, m.PendingPaid, m.Credited, m.Refunded, b.CreatedAt
        """;

    private const string ListJoins = """
        FROM TravelSupplierBills b
        INNER JOIN TravelSuppliers s ON s.Id = b.SupplierId AND s.TenantId = b.TenantId
        """;

    internal const string AllocationSelect = """
        SELECT a.PaymentId, p.PaymentNumber, p.PaidOn, p.Method, p.Status AS PaymentStatus, a.BillId, sb.BillNumber, sb.CurrencyCode AS BillCurrencyCode,
               a.AmountFromPayment, p.CurrencyCode AS PaymentCurrencyCode, a.ExchangeRate, a.AmountApplied
        FROM TravelSupplierPaymentAllocations a
        INNER JOIN TravelSupplierPayments p ON p.Id = a.PaymentId AND p.TenantId = a.TenantId
        INNER JOIN TravelSupplierBills sb ON sb.Id = a.BillId AND sb.TenantId = a.TenantId
        """;

    internal const string CreditSelect = """
        SELECT sc.Id, sc.CreditNumber, sc.BillId, sb.BillNumber, sc.SupplierId, s.Name AS SupplierName, sc.Amount, sc.CurrencyCode, sc.Reason, sc.CancellationId,
               sc.Status, sc.IssuedOn
        FROM TravelSupplierCredits sc
        INNER JOIN TravelSupplierBills sb ON sb.Id = sc.BillId AND sb.TenantId = sc.TenantId
        INNER JOIN TravelSuppliers s ON s.Id = sc.SupplierId AND s.TenantId = sc.TenantId
        """;

    internal const string RefundSelect = """
        SELECT sr.Id, sr.RefundNumber, sr.SupplierId, s.Name AS SupplierName, sr.BillId, sb.BillNumber, sr.PaymentId, sp.PaymentNumber, sr.Amount,
               sr.CurrencyCode, sr.Method, sr.Reference, sr.ReceivedOn, sr.CancellationId
        FROM TravelSupplierRefunds sr
        INNER JOIN TravelSuppliers s ON s.Id = sr.SupplierId AND s.TenantId = sr.TenantId
        LEFT JOIN TravelSupplierBills sb ON sb.Id = sr.BillId AND sb.TenantId = sr.TenantId
        LEFT JOIN TravelSupplierPayments sp ON sp.Id = sr.PaymentId AND sp.TenantId = sr.TenantId
        """;

    internal const string HistorySelect = """
        SELECT h.Id, h.EventType, h.FromValue, h.ToValue, h.Amount, h.CurrencyCode, h.Note, u.FullName AS ChangedByName, h.ChangedAt
        FROM TravelSupplierFinanceHistory h
        LEFT JOIN Users u ON u.Id = h.ChangedBy AND u.TenantId = h.TenantId
        """;

    /// <summary>The supplier's flight / hotel services with a cost (cancelled ones at their supplier penalty), with the bill they sit on.</summary>
    private const string Services = """
        SELECT x.* FROM (
            SELECT N'Flight' AS SourceType, f.Id AS SourceId, f.SupplierId, f.TenantId,
                   CONCAT(N'Flight', N' ' + f.Pnr, N' · ', bk.BookingNumber) AS Description,
                   CASE WHEN f.Status = N'Cancelled'
                        THEN COALESCE((SELECT SUM(c.SupplierPenalty) FROM TravelBookingCancellations c WHERE c.FlightBookingId = f.Id AND c.TenantId = f.TenantId), 0)
                        ELSE f.CostAmount END AS Amount,
                   f.CurrencyCode, f.BookingId, CONCAT(bk.BookingNumber, N' · ', bk.Title) AS BookingLabel,
                   (SELECT MIN(CAST(sg.DepartLocal AS DATE)) FROM TravelFlightSegments sg WHERE sg.FlightBookingId = f.Id AND sg.TenantId = f.TenantId) AS ServiceDate,
                   (SELECT COUNT(1) FROM TravelFlightPassengers fp WHERE fp.FlightBookingId = f.Id AND fp.TenantId = f.TenantId) AS Units,
                   CAST(CASE WHEN f.Status = N'Cancelled' THEN 1 ELSE 0 END AS BIT) AS Cancelled
            FROM TravelFlightBookings f
            INNER JOIN TravelBookings bk ON bk.Id = f.BookingId AND bk.TenantId = f.TenantId AND bk.IsDeleted = 0
            WHERE f.TenantId = @TenantId AND f.SupplierId = @SupplierId AND f.IsDeleted = 0 AND f.CostAmount IS NOT NULL AND f.CurrencyCode IS NOT NULL
            UNION ALL
            SELECT N'Hotel', h.Id, h.SupplierId, h.TenantId,
                   CONCAT(N'Hotel ', ht.Name, N' · ', CONVERT(NVARCHAR(10), h.CheckIn, 23), N'–', CONVERT(NVARCHAR(10), h.CheckOut, 23), N' · ', bk.BookingNumber),
                   CASE WHEN h.Status = N'Cancelled'
                        THEN COALESCE((SELECT SUM(c.SupplierPenalty) FROM TravelBookingCancellations c WHERE c.HotelBookingId = h.Id AND c.TenantId = h.TenantId), 0)
                        ELSE h.CostAmount END,
                   h.CurrencyCode, h.BookingId, CONCAT(bk.BookingNumber, N' · ', bk.Title), h.CheckIn,
                   COALESCE((SELECT SUM(r.Quantity) FROM TravelHotelBookingRooms r WHERE r.HotelBookingId = h.Id AND r.TenantId = h.TenantId), 0)
                     * DATEDIFF(DAY, h.CheckIn, h.CheckOut),
                   CAST(CASE WHEN h.Status = N'Cancelled' THEN 1 ELSE 0 END AS BIT)
            FROM TravelHotelBookings h
            INNER JOIN TravelBookings bk ON bk.Id = h.BookingId AND bk.TenantId = h.TenantId AND bk.IsDeleted = 0
            INNER JOIN TravelHotels ht ON ht.Id = h.HotelId AND ht.TenantId = h.TenantId
            WHERE h.TenantId = @TenantId AND h.SupplierId = @SupplierId AND h.IsDeleted = 0 AND h.CostAmount IS NOT NULL AND h.CurrencyCode IS NOT NULL
        ) x
        """;

    // ── State / lists / detail ──────────────────────────────────────────────

    public async Task<TravelSupplierBillState?> GetStateAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Id", id);
        var clauses = new List<string> { "b.Id = @Id", "b.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "b.BranchId", clauses, p);
        return await connection.QuerySingleOrDefaultAsync<TravelSupplierBillState>(new CommandDefinition($"""
            SELECT b.Id, b.Status, b.SupplierId, b.CurrencyCode, b.BillDate, b.DueDate, b.BranchId,
                   b.Net AS Total, m.Paid, m.PendingPaid, m.Credited, m.Refunded,
                   (SELECT COUNT(1) FROM TravelSupplierPaymentAllocations a INNER JOIN TravelSupplierPayments py ON py.Id = a.PaymentId AND py.TenantId = a.TenantId
                     WHERE a.BillId = b.Id AND a.TenantId = b.TenantId AND py.Status IN (N'Pending', N'Completed', N'Refunded')) AS AllocationCount,
                   (SELECT COUNT(1) FROM TravelSupplierCredits sc WHERE sc.BillId = b.Id AND sc.TenantId = b.TenantId AND sc.Status = N'Issued') AS CreditCount,
                   (SELECT COUNT(1) FROM TravelSupplierRefunds sr WHERE sr.BillId = b.Id AND sr.TenantId = b.TenantId) AS RefundCount
            FROM TravelSupplierBills b
            {Money}
            WHERE b.TenantId = @TenantId AND {string.Join(" AND ", clauses)}
            """, p, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TravelSupplierBillListItemDto>> GetPagedAsync(int tenantId, TravelBranchScope scope, TravelSupplierBillListFilter f, int page,
        int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Today", f.Today.Date);
        var clauses = new List<string> { "b.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "b.BranchId", clauses, p);
        if (f.Search is not null)
        {
            clauses.Add("(b.BillNumber LIKE @Search OR b.SupplierReference LIKE @Search OR s.Name LIKE @Search)");
            p.Add("Search", $"%{TravelSql.EscapeLike(f.Search)}%");
        }
        if (f.Status is not null) { clauses.Add("b.Status = @Status"); p.Add("Status", f.Status); }
        if (f.SupplierId.HasValue) { clauses.Add("b.SupplierId = @SupplierId"); p.Add("SupplierId", f.SupplierId); }
        if (f.CurrencyCode is not null) { clauses.Add("b.CurrencyCode = @Currency"); p.Add("Currency", f.CurrencyCode); }
        if (f.BillFrom.HasValue) { clauses.Add("b.BillDate >= @From"); p.Add("From", f.BillFrom.Value.Date); }
        if (f.BillTo.HasValue) { clauses.Add("b.BillDate <= @To"); p.Add("To", f.BillTo.Value.Date); }
        if (f.OverdueOnly) clauses.Add("b.Status = N'Posted' AND b.DueDate < @Today AND d.Balance > 0");

        var where = string.Join(" AND ", clauses);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT COUNT(1)
            {ListJoins}
            {Money}
            WHERE b.TenantId = @TenantId AND {where}
            """, p, cancellationToken: cancellationToken));
        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);
        var items = (await connection.QueryAsync<TravelSupplierBillListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {ListJoins}
            {Money}
            WHERE b.TenantId = @TenantId AND {where}
            ORDER BY CASE WHEN b.Status = N'Draft' THEN 0 ELSE 1 END, COALESCE(b.BillDate, CAST(b.CreatedAt AS DATE)) DESC, b.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken))).ToList();
        return new PagedResult<TravelSupplierBillListItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<TravelSupplierBillDto?> GetDetailAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var args = new { Id = id, TenantId = tenantId };
        var bill = await connection.QuerySingleOrDefaultAsync<TravelSupplierBillDto>(new CommandDefinition($"""
            {ListSelect}, b.Notes, b.PostedAt, b.VoidReason, b.BranchId
            {ListJoins}
            {Money}
            WHERE b.Id = @Id AND b.TenantId = @TenantId AND b.IsDeleted = 0
            """, args, cancellationToken: cancellationToken));
        if (bill is null) return null;

        bill.Lines = (await connection.QueryAsync<TravelSupplierBillLineDto>(new CommandDefinition("""
            SELECT l.Id, l.BillId, l.Sequence, l.Description, l.SourceType, l.SourceId,
                   COALESCE(l.ContextType, CASE WHEN l.SourceType IS NOT NULL THEN N'Booking' END) AS ContextType,
                   COALESCE(l.ContextId, f.BookingId, h.BookingId) AS ContextId,
                   COALESCE(bk.BookingNumber, vc.CaseNumber, gr.GroupNumber) AS ContextLabel,
                   l.Amount, l.CommissionAmount, l.NetAmount
            FROM TravelSupplierBillLines l
            LEFT JOIN TravelFlightBookings f ON l.SourceType = N'Flight' AND f.Id = l.SourceId AND f.TenantId = l.TenantId
            LEFT JOIN TravelHotelBookings h ON l.SourceType = N'Hotel' AND h.Id = l.SourceId AND h.TenantId = l.TenantId
            LEFT JOIN TravelBookings bk ON bk.Id = COALESCE(f.BookingId, h.BookingId, CASE WHEN l.ContextType = N'Booking' THEN l.ContextId END)
                AND bk.TenantId = l.TenantId
            LEFT JOIN TravelVisaCases vc ON l.ContextType = N'VisaCase' AND vc.Id = l.ContextId AND vc.TenantId = l.TenantId
            LEFT JOIN TravelGroups gr ON l.ContextType = N'Group' AND gr.Id = l.ContextId AND gr.TenantId = l.TenantId
            WHERE l.BillId = @Id AND l.TenantId = @TenantId
            ORDER BY l.Sequence
            """, args, cancellationToken: cancellationToken))).ToList();
        bill.Allocations = (await connection.QueryAsync<TravelSupplierAllocationDto>(new CommandDefinition($"""
            {AllocationSelect}
            WHERE a.BillId = @Id AND a.TenantId = @TenantId
            ORDER BY p.PaidOn, a.Id
            """, args, cancellationToken: cancellationToken))).ToList();
        bill.Credits = (await connection.QueryAsync<TravelSupplierCreditDto>(new CommandDefinition($"""
            {CreditSelect}
            WHERE sc.BillId = @Id AND sc.TenantId = @TenantId
            ORDER BY sc.IssuedOn, sc.Id
            """, args, cancellationToken: cancellationToken))).ToList();
        bill.Refunds = (await connection.QueryAsync<TravelSupplierRefundDto>(new CommandDefinition($"""
            {RefundSelect}
            WHERE sr.BillId = @Id AND sr.TenantId = @TenantId
            ORDER BY sr.ReceivedOn, sr.Id
            """, args, cancellationToken: cancellationToken))).ToList();
        bill.History = (await connection.QueryAsync<TravelFinanceHistoryDto>(new CommandDefinition($"""
            {HistorySelect}
            WHERE h.BillId = @Id AND h.TenantId = @TenantId
            ORDER BY h.ChangedAt DESC, h.Id DESC
            """, args, cancellationToken: cancellationToken))).ToList();
        return bill;
    }

    public async Task<IReadOnlyList<TravelSupplierBillListItemDto>> GetOpenBillsAsync(int tenantId, int supplierId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelSupplierBillListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {ListJoins}
            {Money}
            WHERE b.TenantId = @TenantId AND b.SupplierId = @SupplierId AND b.IsDeleted = 0 AND b.Status = N'Posted' AND d.Balance > 0
            ORDER BY b.DueDate, b.Id
            """, new { TenantId = tenantId, SupplierId = supplierId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<TravelBillableCostDto>> GetBillableCostsAsync(int tenantId, int supplierId, string? currencyCode, int? includeBillId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelBillableCostDto>(new CommandDefinition($"""
            SELECT sv.SourceType, sv.SourceId, sv.SupplierId, sv.Description, sv.Amount, sv.CurrencyCode, sv.BookingId, sv.BookingLabel, sv.ServiceDate,
                   sv.Units, sv.Cancelled, live.BillId AS OnBillId
            FROM ({Services}) sv
            OUTER APPLY (SELECT TOP 1 l.BillId FROM TravelSupplierBillLines l
                         WHERE l.TenantId = sv.TenantId AND l.SourceType = sv.SourceType AND l.SourceId = sv.SourceId AND l.IsVoided = 0) live
            WHERE sv.TenantId = @TenantId AND (@Currency IS NULL OR sv.CurrencyCode = @Currency) AND sv.Amount > 0
              AND (live.BillId IS NULL OR live.BillId = @IncludeBillId)
            ORDER BY sv.ServiceDate, sv.SourceType, sv.SourceId
            """, new { TenantId = tenantId, SupplierId = supplierId, Currency = currencyCode, IncludeBillId = includeBillId },
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<bool> SupplierReferenceInUseAsync(int tenantId, int supplierId, string reference, int? excludeBillId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelSupplierBills
            WHERE TenantId = @TenantId AND SupplierId = @SupplierId AND SupplierReference = @Reference AND IsDeleted = 0 AND Status <> N'Void'
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """, new { TenantId = tenantId, SupplierId = supplierId, Reference = reference, ExcludeId = excludeBillId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> ContextExistsAsync(int tenantId, string contextType, int contextId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = contextType switch
        {
            "Booking" => "SELECT COUNT(1) FROM TravelBookings WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            "VisaCase" => "SELECT COUNT(1) FROM TravelVisaCases WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            "Group" => "SELECT COUNT(1) FROM TravelGroups WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            _ => null
        };
        if (sql is null) return false;
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Id = contextId, TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;
    }

    // ── Writes ──────────────────────────────────────────────────────────────

    public async Task<int> CreateAsync(int tenantId, TravelSupplierBillHeaderWrite h, IReadOnlyList<TravelSupplierBillLineWrite> lines, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                INSERT INTO TravelSupplierBills (TenantId, SupplierId, SupplierReference, CurrencyCode, BillDate, DueDate, Gross, CommissionTotal, Net, Status,
                    BranchId, Notes, CreatedBy)
                SELECT @TenantId, s.Id, @SupplierReference, @CurrencyCode, @BillDate, @DueDate, @Gross, @CommissionTotal, @Net, N'Draft', @BranchId, @Notes, @UserId
                FROM TravelSuppliers s WHERE s.Id = @SupplierId AND s.TenantId = @TenantId AND s.IsDeleted = 0;
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, HeaderArgs(tenantId, h, userId), tx, cancellationToken: cancellationToken)) ?? 0;
            if (id == 0)
            {
                tx.Rollback();
                return 0;
            }
            await WriteLinesAsync(connection, tx, tenantId, id, lines, cancellationToken);
            await HistoryAsync(connection, tx, tenantId, "BillCreated", null, "Draft", h.Net, h.CurrencyCode, h.SupplierReference, userId, cancellationToken, billId: id);
            tx.Commit();
            return id;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // A service was put on another bill meanwhile, or the supplier reference was entered concurrently.
            tx.Rollback();
            return 0;
        }
    }

    public async Task<bool> UpdateDraftAsync(int tenantId, int id, TravelSupplierBillHeaderWrite h, IReadOnlyList<TravelSupplierBillLineWrite> lines, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var args = HeaderArgs(tenantId, h, userId);
            args.Add("Id", id);
            var rows = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE TravelSupplierBills SET SupplierReference = @SupplierReference, BillDate = @BillDate, DueDate = @DueDate, Gross = @Gross,
                    CommissionTotal = @CommissionTotal, Net = @Net, Notes = @Notes, UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Draft'
                """, args, tx, cancellationToken: cancellationToken));
            if (rows == 0)
            {
                tx.Rollback();
                return false;
            }
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM TravelSupplierBillLines WHERE BillId = @Id AND TenantId = @TenantId",
                new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
            await WriteLinesAsync(connection, tx, tenantId, id, lines, cancellationToken);
            await HistoryAsync(connection, tx, tenantId, "BillUpdated", null, null, h.Net, h.CurrencyCode, null, userId, cancellationToken, billId: id);
            tx.Commit();
            return true;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            tx.Rollback();
            return false;
        }
    }

    public async Task<string?> PostAsync(int tenantId, int id, DateTime billDate, DateTime dueDate, int? userId, DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var bill = await connection.QuerySingleOrDefaultAsync<(decimal Net, string CurrencyCode)?>(new CommandDefinition("""
            SELECT Net, CurrencyCode FROM TravelSupplierBills WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Draft'
              AND EXISTS (SELECT 1 FROM TravelSupplierBillLines l WHERE l.BillId = @Id AND l.TenantId = @TenantId)
            """, new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (bill is not { } b)
        {
            tx.Rollback();
            return null;
        }
        var number = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.SupplierBill, utcNow, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelSupplierBills SET Status = N'Posted', BillNumber = @Number, BillDate = @BillDate, DueDate = @DueDate, PostedAt = @Now,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND Status = N'Draft'
            """, new { Id = id, TenantId = tenantId, Number = number, BillDate = billDate.Date, DueDate = dueDate.Date, Now = utcNow },
            tx, cancellationToken: cancellationToken));
        await HistoryAsync(connection, tx, tenantId, "BillPosted", "Draft", number, b.Net, b.CurrencyCode, null, userId, cancellationToken, billId: id);
        tx.Commit();
        return number;
    }

    public async Task<bool> VoidAsync(int tenantId, int id, string reason, int? userId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var voided = await connection.QuerySingleOrDefaultAsync<(decimal Net, string CurrencyCode, string? Number)?>(new CommandDefinition("""
            UPDATE TravelSupplierBills SET Status = N'Void', VoidReason = @Reason, VoidedAt = @Now, UpdatedAt = SYSUTCDATETIME()
            OUTPUT inserted.Net, inserted.CurrencyCode, inserted.BillNumber
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Posted'
              AND NOT EXISTS (SELECT 1 FROM TravelSupplierPaymentAllocations a
                              INNER JOIN TravelSupplierPayments p ON p.Id = a.PaymentId AND p.TenantId = a.TenantId
                              WHERE a.BillId = @Id AND a.TenantId = @TenantId AND p.Status IN (N'Pending', N'Completed', N'Refunded'))
              AND NOT EXISTS (SELECT 1 FROM TravelSupplierCredits sc WHERE sc.BillId = @Id AND sc.TenantId = @TenantId AND sc.Status = N'Issued')
              AND NOT EXISTS (SELECT 1 FROM TravelSupplierRefunds sr WHERE sr.BillId = @Id AND sr.TenantId = @TenantId)
            """, new { Id = id, TenantId = tenantId, Reason = reason, Now = utcNow }, tx, cancellationToken: cancellationToken));
        if (voided is not { } v)
        {
            tx.Rollback();
            return false;
        }
        // Release the services so they can be billed again.
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TravelSupplierBillLines SET IsVoided = 1 WHERE BillId = @Id AND TenantId = @TenantId",
            new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        await HistoryAsync(connection, tx, tenantId, "BillVoided", v.Number, "Void", v.Net, v.CurrencyCode, reason, userId, cancellationToken, billId: id);
        tx.Commit();
        return true;
    }

    public async Task<bool> DeleteDraftAsync(int tenantId, int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelSupplierBills SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Draft'
            """, new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TravelSupplierBillLines SET IsVoided = 1 WHERE BillId = @Id AND TenantId = @TenantId",
            new { Id = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        tx.Commit();
        return true;
    }

    // ── Reports ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TravelPayablesCurrencySummaryDto>> GetCurrencySummaryAsync(int tenantId, TravelBranchScope scope, DateTime today,
        DateTime monthStart, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Today", today.Date);
        p.Add("MonthStart", monthStart.Date);
        var billClauses = new List<string> { "b.IsDeleted = 0", "b.Status = N'Posted'" };
        TravelSql.ApplyBranchScope(scope, "b.BranchId", billClauses, p);
        var paymentClauses = new List<string> { "py.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "py.BranchId", paymentClauses, p);

        var billRows = (await connection.QueryAsync<(string CurrencyCode, decimal Outstanding, decimal CreditDue, decimal Overdue)>(new CommandDefinition($"""
            SELECT b.CurrencyCode,
                   SUM(CASE WHEN d.Balance > 0 THEN d.Balance ELSE 0 END),
                   SUM(CASE WHEN d.Balance < 0 THEN -d.Balance ELSE 0 END),
                   SUM(CASE WHEN d.Balance > 0 AND b.DueDate < @Today THEN d.Balance ELSE 0 END)
            FROM TravelSupplierBills b
            {Money}
            WHERE b.TenantId = @TenantId AND {string.Join(" AND ", billClauses)}
            GROUP BY b.CurrencyCode
            """, p, cancellationToken: cancellationToken))).ToList();
        var paymentRows = (await connection.QueryAsync<(string CurrencyCode, decimal Paid, decimal Unallocated)>(new CommandDefinition($"""
            SELECT py.CurrencyCode,
                   SUM(CASE WHEN py.Status = N'Completed' AND py.PaidOn >= @MonthStart AND py.PaidOn <= @Today THEN py.Amount ELSE 0 END),
                   SUM(CASE WHEN py.Status = N'Completed' THEN py.Amount - pm.Allocated - pm.Refunded ELSE 0 END)
            FROM TravelSupplierPayments py
            {TravelSupplierPaymentRepository.PaymentMoney}
            WHERE py.TenantId = @TenantId AND {string.Join(" AND ", paymentClauses)}
            GROUP BY py.CurrencyCode
            """, p, cancellationToken: cancellationToken))).ToList();

        return billRows.Select(r => r.CurrencyCode).Union(paymentRows.Select(r => r.CurrencyCode)).Distinct().Select(code =>
        {
            var bill = billRows.FirstOrDefault(r => r.CurrencyCode == code);
            var pay = paymentRows.FirstOrDefault(r => r.CurrencyCode == code);
            return new TravelPayablesCurrencySummaryDto
            {
                CurrencyCode = code, Outstanding = bill.Outstanding, SupplierCreditDue = bill.CreditDue, Overdue = bill.Overdue,
                PaidThisMonth = pay.Paid, UnallocatedAdvances = pay.Unallocated
            };
        }).ToList();
    }

    public async Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "b.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "b.BranchId", clauses, p);
        var rows = await connection.QueryAsync<(string Status, int Total)>(new CommandDefinition($"""
            SELECT b.Status, COUNT(1) FROM TravelSupplierBills b WHERE b.TenantId = @TenantId AND {string.Join(" AND ", clauses)} GROUP BY b.Status
            """, p, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.Status, r => r.Total);
    }

    public async Task<IReadOnlyList<TravelSupplierBalanceDto>> GetSupplierBalancesAsync(int tenantId, TravelBranchScope scope, int? supplierId, DateTime today,
        bool openOnly, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Today", today.Date);
        p.Add("SupplierId", supplierId);
        var billClauses = new List<string> { "b.IsDeleted = 0", "b.Status = N'Posted'", "(@SupplierId IS NULL OR b.SupplierId = @SupplierId)" };
        TravelSql.ApplyBranchScope(scope, "b.BranchId", billClauses, p);
        var paymentClauses = new List<string> { "py.IsDeleted = 0", "py.Status = N'Completed'", "(@SupplierId IS NULL OR py.SupplierId = @SupplierId)" };
        TravelSql.ApplyBranchScope(scope, "py.BranchId", paymentClauses, p);

        var bills = (await connection.QueryAsync<TravelSupplierBalanceDto>(new CommandDefinition($"""
            SELECT b.SupplierId, s.Name AS SupplierName, b.CurrencyCode,
                   SUM(CASE WHEN d.Balance <> 0 THEN 1 ELSE 0 END) AS OpenBills,
                   SUM(b.Net) AS Billed, SUM(m.Paid - m.Refunded) AS Paid, SUM(m.Credited) AS Credited, SUM(d.Balance) AS Balance,
                   SUM(CASE WHEN d.Balance > 0 AND b.DueDate < @Today THEN d.Balance ELSE 0 END) AS Overdue
            FROM TravelSupplierBills b
            INNER JOIN TravelSuppliers s ON s.Id = b.SupplierId AND s.TenantId = b.TenantId
            {Money}
            WHERE b.TenantId = @TenantId AND {string.Join(" AND ", billClauses)}
            GROUP BY b.SupplierId, s.Name, b.CurrencyCode
            """, p, cancellationToken: cancellationToken))).ToList();
        var advances = (await connection.QueryAsync<(int SupplierId, string SupplierName, string CurrencyCode, decimal Unallocated)>(new CommandDefinition($"""
            SELECT py.SupplierId, s.Name, py.CurrencyCode, SUM(py.Amount - pm.Allocated - pm.Refunded)
            FROM TravelSupplierPayments py
            INNER JOIN TravelSuppliers s ON s.Id = py.SupplierId AND s.TenantId = py.TenantId
            {TravelSupplierPaymentRepository.PaymentMoney}
            WHERE py.TenantId = @TenantId AND {string.Join(" AND ", paymentClauses)}
            GROUP BY py.SupplierId, s.Name, py.CurrencyCode
            HAVING SUM(py.Amount - pm.Allocated - pm.Refunded) > 0
            """, p, cancellationToken: cancellationToken))).ToList();
        foreach (var a in advances)
        {
            var row = bills.FirstOrDefault(r => r.SupplierId == a.SupplierId && r.CurrencyCode == a.CurrencyCode);
            if (row is null)
                bills.Add(new TravelSupplierBalanceDto { SupplierId = a.SupplierId, SupplierName = a.SupplierName, CurrencyCode = a.CurrencyCode, UnallocatedAdvances = a.Unallocated });
            else row.UnallocatedAdvances = a.Unallocated;
        }
        return bills.Where(r => !openOnly || r.Balance != 0 || r.UnallocatedAdvances > 0)
            .OrderByDescending(r => r.Overdue).ThenByDescending(r => r.Balance).ThenBy(r => r.SupplierName).ToList();
    }

    /// <summary>Positive balance = we owe the supplier: bills and refunds received are debits; payments and credits are credits.</summary>
    public async Task<IReadOnlyList<TravelStatementMovement>> GetStatementMovementsAsync(int tenantId, int supplierId, string currencyCode,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelStatementMovement>(new CommandDefinition("""
            SELECT b.BillDate AS [Date], N'Bill' AS EntryType, b.BillNumber AS Reference,
                   CONCAT(N'Bill', CASE WHEN b.SupplierReference IS NOT NULL THEN CONCAT(N' · their ref ', b.SupplierReference) ELSE N'' END) AS Description,
                   b.Net AS Debit, CAST(0 AS DECIMAL(18,2)) AS Credit, b.Id AS InvoiceId, CAST(NULL AS INT) AS PaymentId, 1 AS [Order]
            FROM TravelSupplierBills b
            WHERE b.TenantId = @TenantId AND b.SupplierId = @SupplierId AND b.CurrencyCode = @Currency AND b.IsDeleted = 0 AND b.Status = N'Posted'
            UNION ALL
            SELECT sc.IssuedOn, N'SupplierCredit', sc.CreditNumber, CONCAT(N'Supplier credit on ', sb.BillNumber, N' · ', LEFT(sc.Reason, 80)),
                   0, sc.Amount, sc.BillId, NULL, 2
            FROM TravelSupplierCredits sc
            INNER JOIN TravelSupplierBills sb ON sb.Id = sc.BillId AND sb.TenantId = sc.TenantId
            WHERE sc.TenantId = @TenantId AND sc.SupplierId = @SupplierId AND sc.CurrencyCode = @Currency AND sc.Status = N'Issued' AND sb.Status = N'Posted'
            UNION ALL
            SELECT p.PaidOn, N'Payment', p.PaymentNumber,
                   CONCAT(N'Payment (', p.Method, N') applied to ', sb.BillNumber,
                          CASE WHEN p.CurrencyCode <> sb.CurrencyCode THEN CONCAT(N' · ', a.AmountFromPayment, N' ', p.CurrencyCode, N' at ', a.ExchangeRate) ELSE N'' END),
                   0, a.AmountApplied, a.BillId, a.PaymentId, 3
            FROM TravelSupplierPaymentAllocations a
            INNER JOIN TravelSupplierPayments p ON p.Id = a.PaymentId AND p.TenantId = a.TenantId
            INNER JOIN TravelSupplierBills sb ON sb.Id = a.BillId AND sb.TenantId = a.TenantId
            WHERE a.TenantId = @TenantId AND p.SupplierId = @SupplierId AND sb.CurrencyCode = @Currency AND p.IsDeleted = 0
              AND p.Status IN (N'Completed', N'Refunded') AND sb.Status = N'Posted'
            UNION ALL
            SELECT sr.ReceivedOn, N'RefundReceived', sr.RefundNumber, CONCAT(N'Refund received (', sr.Method, N') on ', sb.BillNumber), sr.Amount, 0, sr.BillId, NULL, 4
            FROM TravelSupplierRefunds sr
            INNER JOIN TravelSupplierBills sb ON sb.Id = sr.BillId AND sb.TenantId = sr.TenantId
            WHERE sr.TenantId = @TenantId AND sr.SupplierId = @SupplierId AND sr.CurrencyCode = @Currency
            UNION ALL
            -- Advances: the part of a cleared payment in this currency not applied to any bill
            SELECT p.PaidOn, N'Advance', p.PaymentNumber, CONCAT(N'Payment on account (', p.Method, N')'),
                   0, p.Amount - COALESCE((SELECT SUM(a.AmountFromPayment) FROM TravelSupplierPaymentAllocations a WHERE a.PaymentId = p.Id AND a.TenantId = p.TenantId), 0),
                   NULL, p.Id, 3
            FROM TravelSupplierPayments p
            WHERE p.TenantId = @TenantId AND p.SupplierId = @SupplierId AND p.CurrencyCode = @Currency AND p.IsDeleted = 0
              AND p.Status IN (N'Completed', N'Refunded')
              AND p.Amount - COALESCE((SELECT SUM(a.AmountFromPayment) FROM TravelSupplierPaymentAllocations a WHERE a.PaymentId = p.Id AND a.TenantId = p.TenantId), 0) > 0
            UNION ALL
            SELECT sr.ReceivedOn, N'RefundReceived', sr.RefundNumber, CONCAT(N'Advance returned (', sr.Method, N') from ', sp.PaymentNumber), sr.Amount, 0, NULL, sr.PaymentId, 4
            FROM TravelSupplierRefunds sr
            INNER JOIN TravelSupplierPayments sp ON sp.Id = sr.PaymentId AND sp.TenantId = sr.TenantId
            WHERE sr.TenantId = @TenantId AND sr.SupplierId = @SupplierId AND sr.CurrencyCode = @Currency
            """, new { TenantId = tenantId, SupplierId = supplierId, Currency = currencyCode }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<string>> GetSupplierCurrenciesAsync(int tenantId, int supplierId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT CurrencyCode FROM TravelSupplierBills WHERE TenantId = @TenantId AND SupplierId = @SupplierId AND IsDeleted = 0 AND Status = N'Posted'
            UNION
            SELECT CurrencyCode FROM TravelSupplierPayments
            WHERE TenantId = @TenantId AND SupplierId = @SupplierId AND IsDeleted = 0 AND Status IN (N'Completed', N'Refunded')
            ORDER BY CurrencyCode
            """, new { TenantId = tenantId, SupplierId = supplierId }, cancellationToken: cancellationToken))).ToList();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static DynamicParameters HeaderArgs(int tenantId, TravelSupplierBillHeaderWrite h, int? userId)
    {
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("SupplierId", h.SupplierId);
        p.Add("SupplierReference", h.SupplierReference);
        p.Add("CurrencyCode", h.CurrencyCode);
        p.Add("BillDate", h.BillDate?.Date);
        p.Add("DueDate", h.DueDate?.Date);
        p.Add("Gross", h.Gross);
        p.Add("CommissionTotal", h.CommissionTotal);
        p.Add("Net", h.Net);
        p.Add("BranchId", h.BranchId);
        p.Add("Notes", h.Notes);
        p.Add("UserId", userId);
        return p;
    }

    private static async Task WriteLinesAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int billId, IReadOnlyList<TravelSupplierBillLineWrite> lines,
        CancellationToken ct)
    {
        foreach (var l in lines)
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TravelSupplierBillLines (TenantId, BillId, Sequence, Description, SourceType, SourceId, ContextType, ContextId, Amount, CommissionAmount,
                    NetAmount)
                VALUES (@TenantId, @BillId, @Sequence, @Description, @SourceType, @SourceId, @ContextType, @ContextId, @Amount, @CommissionAmount, @NetAmount)
                """, new
                {
                    TenantId = tenantId, BillId = billId, l.Sequence, l.Description, l.SourceType, l.SourceId, l.ContextType, l.ContextId, l.Amount,
                    l.CommissionAmount, l.NetAmount
                }, tx, cancellationToken: ct));
    }

    internal static Task HistoryAsync(IDbConnection connection, IDbTransaction tx, int tenantId, string eventType, string? from, string? to, decimal? amount,
        string? currency, string? note, int? userId, CancellationToken ct, int? billId = null, int? paymentId = null, int? creditId = null, int? refundId = null,
        int? payoutId = null, int? invoiceId = null)
        => connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TravelSupplierFinanceHistory (TenantId, BillId, PaymentId, CreditId, RefundId, PayoutId, InvoiceId, EventType, FromValue, ToValue, Amount,
                CurrencyCode, Note, ChangedBy)
            VALUES (@TenantId, @BillId, @PaymentId, @CreditId, @RefundId, @PayoutId, @InvoiceId, @EventType, LEFT(@From, 100), LEFT(@To, 100), @Amount, @Currency,
                LEFT(@Note, 1000), @UserId)
            """, new
            {
                TenantId = tenantId, BillId = billId, PaymentId = paymentId, CreditId = creditId, RefundId = refundId, PayoutId = payoutId, InvoiceId = invoiceId,
                EventType = eventType, From = from, To = to, Amount = amount, Currency = currency, Note = note, UserId = userId
            }, tx, cancellationToken: ct));
}
