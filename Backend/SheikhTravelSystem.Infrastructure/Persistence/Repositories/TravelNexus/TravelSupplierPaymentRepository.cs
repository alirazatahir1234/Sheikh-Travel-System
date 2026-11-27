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
/// Supplier payments, allocations, supplier credits and refunds received. Root predicate on the row's TenantId; joins repeat it.
/// Every money write locks the bill / payment it changes (<c>UPDLOCK</c>), re-checks the cap with the same formula as the read side,
/// and writes history in the same transaction. Idempotency keys make retries return the original row.
/// </summary>
public sealed class TravelSupplierPaymentRepository(IDbConnectionFactory dbFactory) : ITravelSupplierPaymentRepository
{
    /// <summary>Per supplier payment <c>py</c>: amounts applied to bills and returned from its advance (pm).</summary>
    internal const string PaymentMoney = """
        OUTER APPLY (
            SELECT COALESCE((SELECT SUM(a.AmountFromPayment) FROM TravelSupplierPaymentAllocations a WHERE a.PaymentId = py.Id AND a.TenantId = py.TenantId), 0) AS Allocated,
                   COALESCE((SELECT SUM(r.Amount) FROM TravelSupplierRefunds r WHERE r.PaymentId = py.Id AND r.TenantId = py.TenantId), 0) AS Refunded
        ) pm
        """;

    private const string ListSelect = """
        SELECT py.Id, py.PaymentNumber, py.SupplierId, s.Name AS SupplierName, py.PaidOn, py.Method, py.Reference, py.Amount, py.CurrencyCode,
               py.Status, pm.Allocated, pm.Refunded, py.ReconciledAt, py.BankReference, py.CreatedAt
        FROM TravelSupplierPayments py
        INNER JOIN TravelSuppliers s ON s.Id = py.SupplierId AND s.TenantId = py.TenantId
        """;

    /// <summary>Bill row <c>b</c> locked for a money write, with its live balance and pending reservations.</summary>
    private const string LockBill = $"""
        SELECT b.Id, b.SupplierId, b.CurrencyCode, b.Status, b.Net AS Total, m.Paid, m.PendingPaid, m.Credited, m.Refunded
        FROM TravelSupplierBills b WITH (UPDLOCK, ROWLOCK)
        {TravelSupplierBillRepository.Money}
        WHERE b.Id = @BillId AND b.TenantId = @TenantId AND b.IsDeleted = 0
        """;

    private sealed class LockedBill : TravelInvoiceMoney
    {
        public int Id { get; init; }
        public int SupplierId { get; init; }
        public string CurrencyCode { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }

    // ── Reads ───────────────────────────────────────────────────────────────

    public async Task<TravelSupplierPaymentState?> GetStateAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Id", id);
        var clauses = new List<string> { "py.Id = @Id", "py.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "py.BranchId", clauses, p);
        return await connection.QuerySingleOrDefaultAsync<TravelSupplierPaymentState>(new CommandDefinition($"""
            SELECT py.Id, py.SupplierId, py.Amount, py.CurrencyCode, py.Status, py.Method, py.ReconciledAt, pm.Allocated, pm.Refunded
            FROM TravelSupplierPayments py
            {PaymentMoney}
            WHERE py.TenantId = @TenantId AND {string.Join(" AND ", clauses)}
            """, p, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TravelSupplierPaymentListItemDto>> GetPagedAsync(int tenantId, TravelBranchScope scope, TravelSupplierPaymentListFilter f, int page,
        int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "py.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "py.BranchId", clauses, p);
        if (f.Search is not null)
        {
            clauses.Add("(py.PaymentNumber LIKE @Search OR py.Reference LIKE @Search OR py.BankReference LIKE @Search OR s.Name LIKE @Search)");
            p.Add("Search", $"%{TravelSql.EscapeLike(f.Search)}%");
        }
        if (f.Status is not null) { clauses.Add("py.Status = @Status"); p.Add("Status", f.Status); }
        if (f.SupplierId.HasValue) { clauses.Add("py.SupplierId = @SupplierId"); p.Add("SupplierId", f.SupplierId); }
        if (f.PaidFrom.HasValue) { clauses.Add("py.PaidOn >= @From"); p.Add("From", f.PaidFrom.Value.Date); }
        if (f.PaidTo.HasValue) { clauses.Add("py.PaidOn <= @To"); p.Add("To", f.PaidTo.Value.Date); }
        if (f.Reconciled == true) clauses.Add("py.ReconciledAt IS NOT NULL");
        if (f.Reconciled == false) clauses.Add("py.ReconciledAt IS NULL AND py.Status IN (N'Completed', N'Refunded') AND py.Method <> N'Cash'");

        var where = string.Join(" AND ", clauses);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT COUNT(1) FROM TravelSupplierPayments py INNER JOIN TravelSuppliers s ON s.Id = py.SupplierId AND s.TenantId = py.TenantId
            WHERE py.TenantId = @TenantId AND {where}
            """, p, cancellationToken: cancellationToken));
        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);
        var items = (await connection.QueryAsync<TravelSupplierPaymentListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {PaymentMoney}
            WHERE py.TenantId = @TenantId AND {where}
            ORDER BY py.PaidOn DESC, py.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken))).ToList();
        return new PagedResult<TravelSupplierPaymentListItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<TravelSupplierPaymentDto?> GetDetailAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var args = new { Id = id, TenantId = tenantId };
        var payment = await connection.QuerySingleOrDefaultAsync<TravelSupplierPaymentDto>(new CommandDefinition($"""
            SELECT py.Id, py.PaymentNumber, py.SupplierId, s.Name AS SupplierName, py.PaidOn, py.Method, py.Reference, py.Amount, py.CurrencyCode,
                   py.Status, pm.Allocated, pm.Refunded, py.ReconciledAt, py.BankReference, py.CreatedAt, py.Notes, py.ClearedAt
            FROM TravelSupplierPayments py
            INNER JOIN TravelSuppliers s ON s.Id = py.SupplierId AND s.TenantId = py.TenantId
            {PaymentMoney}
            WHERE py.Id = @Id AND py.TenantId = @TenantId AND py.IsDeleted = 0
            """, args, cancellationToken: cancellationToken));
        if (payment is null) return null;
        payment.Allocations = (await connection.QueryAsync<TravelSupplierAllocationDto>(new CommandDefinition($"""
            {TravelSupplierBillRepository.AllocationSelect}
            WHERE a.PaymentId = @Id AND a.TenantId = @TenantId
            ORDER BY a.Id
            """, args, cancellationToken: cancellationToken))).ToList();
        payment.Refunds = (await connection.QueryAsync<TravelSupplierRefundDto>(new CommandDefinition($"""
            {TravelSupplierBillRepository.RefundSelect}
            WHERE sr.PaymentId = @Id AND sr.TenantId = @TenantId
            ORDER BY sr.ReceivedOn, sr.Id
            """, args, cancellationToken: cancellationToken))).ToList();
        payment.History = (await connection.QueryAsync<TravelFinanceHistoryDto>(new CommandDefinition($"""
            {TravelSupplierBillRepository.HistorySelect}
            WHERE h.PaymentId = @Id AND h.TenantId = @TenantId
            ORDER BY h.ChangedAt DESC, h.Id DESC
            """, args, cancellationToken: cancellationToken))).ToList();
        return payment;
    }

    // ── Payments ────────────────────────────────────────────────────────────

    /// <remarks><see cref="TravelPaymentWrite.CustomerId"/> carries the supplier id.</remarks>
    public async Task<TravelFinanceWriteResult> RecordAsync(int tenantId, TravelPaymentWrite w, IReadOnlyList<TravelAllocationWrite> allocations, int? userId,
        DateTime utcNow, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            if (await ExistingByKeyAsync(connection, tx, "TravelSupplierPayments", tenantId, w.IdempotencyKey, cancellationToken) is int replay)
            {
                tx.Commit();
                return new TravelFinanceWriteResult(replay, null, Replayed: true);
            }
            var number = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.SupplierPayment, utcNow, cancellationToken);
            var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                INSERT INTO TravelSupplierPayments (TenantId, PaymentNumber, SupplierId, PaidOn, Method, Reference, Amount, CurrencyCode, Status, IdempotencyKey,
                    ClearedAt, BranchId, Notes, CreatedBy)
                SELECT @TenantId, @Number, s.Id, @PaidOn, @Method, @Reference, @Amount, @CurrencyCode, @Status, @Key,
                    CASE WHEN @Status = N'Completed' THEN @Now END, @BranchId, @Notes, @UserId
                FROM TravelSuppliers s WHERE s.Id = @SupplierId AND s.TenantId = @TenantId AND s.IsDeleted = 0;
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new
                {
                    TenantId = tenantId, Number = number, SupplierId = w.CustomerId, PaidOn = w.ReceivedOn.Date, w.Method, w.Reference, w.Amount, w.CurrencyCode,
                    w.Status, Key = w.IdempotencyKey, Now = utcNow, w.BranchId, w.Notes, UserId = userId
                }, tx, cancellationToken: cancellationToken)) ?? 0;
            if (id == 0)
            {
                tx.Rollback();
                return TravelFinanceWriteResult.Fail("Supplier not found.");
            }
            await TravelSupplierBillRepository.HistoryAsync(connection, tx, tenantId, "PaymentRecorded", null, w.Status, w.Amount, w.CurrencyCode,
                $"{number} · {w.Method}", userId, cancellationToken, paymentId: id);
            var error = await ApplyAsync(connection, tx, tenantId, id, w.CustomerId, w.CurrencyCode, allocations, userId, cancellationToken);
            if (error is not null)
            {
                tx.Rollback();
                return TravelFinanceWriteResult.Fail(error);
            }
            tx.Commit();
            return new TravelFinanceWriteResult(id, null);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // A concurrent submission with the same key won the race: return its row.
            tx.Rollback();
            using var again = dbFactory.CreateConnection();
            var replay = await again.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT Id FROM TravelSupplierPayments WHERE TenantId = @TenantId AND IdempotencyKey = @Key",
                new { TenantId = tenantId, Key = w.IdempotencyKey }, cancellationToken: cancellationToken));
            return replay is int r ? new TravelFinanceWriteResult(r, null, Replayed: true) : TravelFinanceWriteResult.Fail("The payment could not be recorded. Try again.");
        }
    }

    public async Task<TravelFinanceWriteResult> AllocateAsync(int tenantId, int paymentId, IReadOnlyList<TravelAllocationWrite> allocations, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var payment = await connection.QuerySingleOrNullAsync<(int SupplierId, string CurrencyCode, decimal Amount, string Status, decimal Allocated, decimal Refunded)>(
            new CommandDefinition($"""
                SELECT py.SupplierId, py.CurrencyCode, py.Amount, py.Status, pm.Allocated, pm.Refunded
                FROM TravelSupplierPayments py WITH (UPDLOCK, ROWLOCK)
                {PaymentMoney}
                WHERE py.Id = @Id AND py.TenantId = @TenantId AND py.IsDeleted = 0
                """, new { Id = paymentId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (payment is not { } pay || pay.Status is not ("Pending" or "Completed"))
        {
            tx.Rollback();
            return TravelFinanceWriteResult.Fail("The payment can no longer be allocated.");
        }
        if (allocations.Sum(a => a.AmountFromPayment) > pay.Amount - pay.Allocated - pay.Refunded)
        {
            tx.Rollback();
            return TravelFinanceWriteResult.Fail("The payment was allocated by someone else meanwhile — not enough is left.");
        }
        var error = await ApplyAsync(connection, tx, tenantId, paymentId, pay.SupplierId, pay.CurrencyCode, allocations, userId, cancellationToken);
        if (error is not null)
        {
            tx.Rollback();
            return TravelFinanceWriteResult.Fail(error);
        }
        tx.Commit();
        return new TravelFinanceWriteResult(paymentId, null);
    }

    /// <summary>Locks each bill, re-checks its open balance, and writes (or tops up) the allocation.</summary>
    private static async Task<string?> ApplyAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int paymentId, int supplierId, string paymentCurrency,
        IReadOnlyList<TravelAllocationWrite> allocations, int? userId, CancellationToken ct)
    {
        foreach (var a in allocations)
        {
            var bill = await connection.QuerySingleOrDefaultAsync<LockedBill>(new CommandDefinition(LockBill,
                new { BillId = a.InvoiceId, TenantId = tenantId }, tx, cancellationToken: ct));
            if (bill is null || bill.Status != "Posted" || bill.SupplierId != supplierId)
                return "A bill to pay is not an open bill of this supplier.";
            var available = Math.Max(0, bill.Balance - bill.PendingPaid);
            if (a.AmountApplied > available)
                return $"{a.AmountApplied:0.##} {bill.CurrencyCode} is more than the open balance of the bill ({available:0.##} {bill.CurrencyCode}).";

            var existingRate = await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition("""
                SELECT ExchangeRate FROM TravelSupplierPaymentAllocations WITH (UPDLOCK) WHERE PaymentId = @PaymentId AND BillId = @BillId AND TenantId = @TenantId
                """, new { PaymentId = paymentId, BillId = a.InvoiceId, TenantId = tenantId }, tx, cancellationToken: ct));
            if (existingRate is decimal rate && rate != a.ExchangeRate)
                return "This payment is already applied to the bill at a different rate — remove that allocation first.";
            await connection.ExecuteAsync(new CommandDefinition(existingRate is null
                ? """
                  INSERT INTO TravelSupplierPaymentAllocations (TenantId, PaymentId, BillId, AmountFromPayment, ExchangeRate, AmountApplied, CreatedBy)
                  VALUES (@TenantId, @PaymentId, @BillId, @FromPayment, @Rate, @Applied, @UserId)
                  """
                : """
                  UPDATE TravelSupplierPaymentAllocations SET AmountFromPayment = AmountFromPayment + @FromPayment, AmountApplied = AmountApplied + @Applied
                  WHERE PaymentId = @PaymentId AND BillId = @BillId AND TenantId = @TenantId
                  """,
                new { TenantId = tenantId, PaymentId = paymentId, BillId = a.InvoiceId, FromPayment = a.AmountFromPayment, Rate = a.ExchangeRate, Applied = a.AmountApplied, UserId = userId },
                tx, cancellationToken: ct));
            await TravelSupplierBillRepository.HistoryAsync(connection, tx, tenantId, "Allocated", null, null, a.AmountApplied, bill.CurrencyCode,
                paymentCurrency == bill.CurrencyCode ? null : $"{a.AmountFromPayment:0.##} {paymentCurrency} at {a.ExchangeRate:0.######}", userId, ct,
                billId: a.InvoiceId, paymentId: paymentId);
        }
        return null;
    }

    public async Task<bool> UnallocateAsync(int tenantId, int paymentId, int billId, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var removed = await connection.QuerySingleOrNullAsync<(decimal Applied, string CurrencyCode)>(new CommandDefinition("""
            DELETE a
            OUTPUT deleted.AmountApplied, sb.CurrencyCode
            FROM TravelSupplierPaymentAllocations a
            INNER JOIN TravelSupplierBills sb ON sb.Id = a.BillId AND sb.TenantId = a.TenantId AND sb.Status = N'Posted'
            INNER JOIN TravelSupplierPayments py ON py.Id = a.PaymentId AND py.TenantId = a.TenantId AND py.ReconciledAt IS NULL
            WHERE a.PaymentId = @PaymentId AND a.BillId = @BillId AND a.TenantId = @TenantId
            """, new { PaymentId = paymentId, BillId = billId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (removed is null)
        {
            tx.Rollback();
            return false;
        }
        await TravelSupplierBillRepository.HistoryAsync(connection, tx, tenantId, "Unallocated", null, null, removed.Value.Applied, removed.Value.CurrencyCode,
            null, userId, cancellationToken, billId: billId, paymentId: paymentId);
        tx.Commit();
        return true;
    }

    public async Task<bool> ChangeStatusAsync(int tenantId, int paymentId, string expectedStatus, string newStatus, string? note, int? userId, DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var changed = await connection.QuerySingleOrNullAsync<(decimal Amount, string CurrencyCode)>(new CommandDefinition("""
            UPDATE TravelSupplierPayments SET Status = @NewStatus, ClearedAt = CASE WHEN @NewStatus = N'Completed' THEN @Now ELSE ClearedAt END,
                UpdatedAt = SYSUTCDATETIME()
            OUTPUT inserted.Amount, inserted.CurrencyCode
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = @Expected AND ReconciledAt IS NULL
              AND (@NewStatus <> N'Failed' OR NOT EXISTS (SELECT 1 FROM TravelSupplierRefunds r WHERE r.PaymentId = @Id AND r.TenantId = @TenantId))
            """, new { Id = paymentId, TenantId = tenantId, Expected = expectedStatus, NewStatus = newStatus, Now = utcNow }, tx, cancellationToken: cancellationToken));
        if (changed is null)
        {
            tx.Rollback();
            return false;
        }
        await TravelSupplierBillRepository.HistoryAsync(connection, tx, tenantId, "PaymentStatus", expectedStatus, newStatus, changed.Value.Amount,
            changed.Value.CurrencyCode, note, userId, cancellationToken, paymentId: paymentId);
        tx.Commit();
        return true;
    }

    public async Task<bool> ReconcileAsync(int tenantId, int paymentId, string bankReference, string? note, int? userId, DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelSupplierPayments SET ReconciledAt = @Now, ReconciledBy = @UserId, BankReference = @BankReference, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND ReconciledAt IS NULL AND Status IN (N'Completed', N'Refunded')
            """, new { Id = paymentId, TenantId = tenantId, Now = utcNow, UserId = userId, BankReference = bankReference }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }
        await TravelSupplierBillRepository.HistoryAsync(connection, tx, tenantId, "Reconciled", null, bankReference, null, null, note, userId, cancellationToken,
            paymentId: paymentId);
        tx.Commit();
        return true;
    }

    // ── Supplier credits ────────────────────────────────────────────────────

    public async Task<TravelCreditNoteState?> GetCreditAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelCreditNoteState>(new CommandDefinition("""
            SELECT Id, BillId AS InvoiceId, Status, Amount FROM TravelSupplierCredits WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<TravelFinanceWriteResult> IssueCreditAsync(int tenantId, IssueTravelSupplierCreditDto credit, DateTime issuedOn, int? userId, DateTime utcNow,
        bool advanceCancellation, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            if (await ExistingByKeyAsync(connection, tx, "TravelSupplierCredits", tenantId, credit.IdempotencyKey, cancellationToken) is int replay)
            {
                tx.Commit();
                return new TravelFinanceWriteResult(replay, null, Replayed: true);
            }
            var bill = await connection.QuerySingleOrDefaultAsync<LockedBill>(new CommandDefinition(LockBill,
                new { BillId = credit.BillId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
            if (bill is null || bill.Status != "Posted")
            {
                tx.Rollback();
                return TravelFinanceWriteResult.Fail("Supplier credits are recorded against posted bills.");
            }
            if (credit.Amount > bill.Total - bill.Credited)
            {
                tx.Rollback();
                return TravelFinanceWriteResult.Fail($"At most {bill.Total - bill.Credited:0.##} {bill.CurrencyCode} can still be credited.");
            }
            var number = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.SupplierCredit, utcNow, cancellationToken);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO TravelSupplierCredits (TenantId, CreditNumber, BillId, SupplierId, Amount, CurrencyCode, Reason, CancellationId, Status, IssuedOn,
                    IdempotencyKey, CreatedBy)
                VALUES (@TenantId, @Number, @BillId, @SupplierId, @Amount, @Currency, @Reason, @CancellationId, N'Issued', @IssuedOn, @Key, @UserId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new
                {
                    TenantId = tenantId, Number = number, credit.BillId, bill.SupplierId, credit.Amount, Currency = bill.CurrencyCode, credit.Reason,
                    credit.CancellationId, IssuedOn = issuedOn.Date, Key = credit.IdempotencyKey, UserId = userId
                }, tx, cancellationToken: cancellationToken));
            await TravelSupplierBillRepository.HistoryAsync(connection, tx, tenantId, "CreditIssued", null, number, credit.Amount, bill.CurrencyCode, credit.Reason,
                userId, cancellationToken, billId: credit.BillId, creditId: id);
            if (advanceCancellation && credit.CancellationId is int cancellationId)
                await AdvanceCancellationAsync(connection, tx, tenantId, cancellationId, $"Supplier credit {number}", userId, cancellationToken);
            tx.Commit();
            return new TravelFinanceWriteResult(id, null);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            tx.Rollback();
            return TravelFinanceWriteResult.Fail("This supplier credit is being recorded already. Reload the bill.");
        }
    }

    public async Task<bool> VoidCreditAsync(int tenantId, int creditId, string reason, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var args = new { Id = creditId, TenantId = tenantId, Reason = reason };
        // Lock the bill first, then re-check that no refund received depends on the credit.
        var billId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT sb.Id FROM TravelSupplierBills sb WITH (UPDLOCK, ROWLOCK)
            INNER JOIN TravelSupplierCredits sc ON sc.BillId = sb.Id AND sc.TenantId = sb.TenantId
            WHERE sc.Id = @Id AND sc.TenantId = @TenantId AND sc.Status = N'Issued'
              AND NOT EXISTS (SELECT 1 FROM TravelSupplierRefunds r WHERE r.BillId = sb.Id AND r.TenantId = sb.TenantId)
            """, args, tx, cancellationToken: cancellationToken));
        if (billId is null)
        {
            tx.Rollback();
            return false;
        }
        var voided = await connection.QuerySingleOrNullAsync<(decimal Amount, string CurrencyCode, string Number)>(new CommandDefinition("""
            UPDATE TravelSupplierCredits SET Status = N'Void', VoidReason = @Reason, UpdatedAt = SYSUTCDATETIME()
            OUTPUT inserted.Amount, inserted.CurrencyCode, inserted.CreditNumber
            WHERE Id = @Id AND TenantId = @TenantId AND Status = N'Issued'
            """, args, tx, cancellationToken: cancellationToken));
        if (voided is null)
        {
            tx.Rollback();
            return false;
        }
        await TravelSupplierBillRepository.HistoryAsync(connection, tx, tenantId, "CreditVoided", voided.Value.Number, "Void", voided.Value.Amount,
            voided.Value.CurrencyCode, reason, userId, cancellationToken, billId: billId.Value, creditId: creditId);
        tx.Commit();
        return true;
    }

    public async Task<PagedResult<TravelSupplierCreditDto>> GetCreditsAsync(int tenantId, int? supplierId, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var args = new { TenantId = tenantId, SupplierId = supplierId, Offset = (page - 1) * pageSize, PageSize = pageSize };
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelSupplierCredits sc WHERE sc.TenantId = @TenantId AND (@SupplierId IS NULL OR sc.SupplierId = @SupplierId)
            """, args, cancellationToken: cancellationToken));
        var items = (await connection.QueryAsync<TravelSupplierCreditDto>(new CommandDefinition($"""
            {TravelSupplierBillRepository.CreditSelect}
            WHERE sc.TenantId = @TenantId AND (@SupplierId IS NULL OR sc.SupplierId = @SupplierId)
            ORDER BY sc.IssuedOn DESC, sc.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, args, cancellationToken: cancellationToken))).ToList();
        return new PagedResult<TravelSupplierCreditDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    // ── Refunds received ────────────────────────────────────────────────────

    public async Task<TravelFinanceWriteResult> RecordRefundAsync(int tenantId, RecordTravelSupplierRefundDto refund, int supplierId, string currencyCode, int? userId,
        DateTime utcNow, bool advanceCancellation, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            if (await ExistingByKeyAsync(connection, tx, "TravelSupplierRefunds", tenantId, refund.IdempotencyKey, cancellationToken) is int replay)
            {
                tx.Commit();
                return new TravelFinanceWriteResult(replay, null, Replayed: true);
            }

            var fullyReturnsPayment = false;
            if (refund.BillId is int billId)
            {
                var bill = await connection.QuerySingleOrDefaultAsync<LockedBill>(new CommandDefinition(LockBill,
                    new { BillId = billId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
                if (bill is null || bill.Status != "Posted" || bill.SupplierId != supplierId || refund.Amount > -bill.Balance)
                {
                    tx.Rollback();
                    return TravelFinanceWriteResult.Fail("The supplier no longer owes that much back on this bill. Reload it.");
                }
            }
            else
            {
                var pay = await connection.QuerySingleOrNullAsync<(int SupplierId, string Status, decimal Amount, decimal Allocated, decimal Refunded)>(
                    new CommandDefinition($"""
                        SELECT py.SupplierId, py.Status, py.Amount, pm.Allocated, pm.Refunded
                        FROM TravelSupplierPayments py WITH (UPDLOCK, ROWLOCK)
                        {PaymentMoney}
                        WHERE py.Id = @Id AND py.TenantId = @TenantId AND py.IsDeleted = 0
                        """, new { Id = refund.PaymentId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
                if (pay is not { } p || p.SupplierId != supplierId || p.Status != "Completed" || refund.Amount > p.Amount - p.Allocated - p.Refunded)
                {
                    tx.Rollback();
                    return TravelFinanceWriteResult.Fail("The payment no longer has that much unallocated. Reload it.");
                }
                fullyReturnsPayment = p.Allocated == 0 && p.Refunded + refund.Amount == p.Amount;
            }

            var number = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.SupplierRefund, utcNow, cancellationToken);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO TravelSupplierRefunds (TenantId, RefundNumber, SupplierId, BillId, PaymentId, Amount, CurrencyCode, Method, Reference, ReceivedOn,
                    CancellationId, Notes, IdempotencyKey, CreatedBy)
                VALUES (@TenantId, @Number, @SupplierId, @BillId, @PaymentId, @Amount, @Currency, @Method, @Reference, @ReceivedOn,
                    @CancellationId, @Notes, @Key, @UserId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new
                {
                    TenantId = tenantId, Number = number, SupplierId = supplierId, refund.BillId, refund.PaymentId, refund.Amount, Currency = currencyCode,
                    refund.Method, Reference = string.IsNullOrWhiteSpace(refund.Reference) ? null : refund.Reference.Trim(), ReceivedOn = refund.ReceivedOn.Date,
                    refund.CancellationId, Notes = string.IsNullOrWhiteSpace(refund.Notes) ? null : refund.Notes.Trim(), Key = refund.IdempotencyKey, UserId = userId
                }, tx, cancellationToken: cancellationToken));
            if (fullyReturnsPayment)
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE TravelSupplierPayments SET Status = N'Refunded', UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND TenantId = @TenantId AND Status = N'Completed'
                    """, new { Id = refund.PaymentId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
            await TravelSupplierBillRepository.HistoryAsync(connection, tx, tenantId, "RefundReceived", null, number, refund.Amount, currencyCode, refund.Notes,
                userId, cancellationToken, billId: refund.BillId, paymentId: refund.PaymentId, refundId: id);
            if (advanceCancellation && refund.CancellationId is int cancellationId)
                await AdvanceCancellationAsync(connection, tx, tenantId, cancellationId, $"Supplier refund {number}", userId, cancellationToken);
            tx.Commit();
            return new TravelFinanceWriteResult(id, null);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            tx.Rollback();
            return TravelFinanceWriteResult.Fail("This refund is being recorded already. Reload the record.");
        }
    }

    public async Task<PagedResult<TravelSupplierRefundDto>> GetRefundsAsync(int tenantId, int? supplierId, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var args = new { TenantId = tenantId, SupplierId = supplierId, Offset = (page - 1) * pageSize, PageSize = pageSize };
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelSupplierRefunds sr WHERE sr.TenantId = @TenantId AND (@SupplierId IS NULL OR sr.SupplierId = @SupplierId)
            """, args, cancellationToken: cancellationToken));
        var items = (await connection.QueryAsync<TravelSupplierRefundDto>(new CommandDefinition($"""
            {TravelSupplierBillRepository.RefundSelect}
            WHERE sr.TenantId = @TenantId AND (@SupplierId IS NULL OR sr.SupplierId = @SupplierId)
            ORDER BY sr.ReceivedOn DESC, sr.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, args, cancellationToken: cancellationToken))).ToList();
        return new PagedResult<TravelSupplierRefundDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<(int SupplierId, string RefundStatus, string? CurrencyCode)?> GetCancellationSupplierAsync(int tenantId, int cancellationId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrNullAsync<(int SupplierId, string RefundStatus, string? CurrencyCode)>(new CommandDefinition("""
            SELECT COALESCE(f.SupplierId, h.SupplierId, 0) AS SupplierId, x.RefundStatus, x.CurrencyCode
            FROM TravelBookingCancellations x
            LEFT JOIN TravelFlightBookings f ON f.Id = x.FlightBookingId AND f.TenantId = x.TenantId
            LEFT JOIN TravelHotelBookings h ON h.Id = x.HotelBookingId AND h.TenantId = x.TenantId
            WHERE x.Id = @Id AND x.TenantId = @TenantId
            """, new { Id = cancellationId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Moves a Phase 5 cancellation's refund tracking to ReceivedFromSupplier (only from Pending) and logs it on the booking.</summary>
    private static async Task AdvanceCancellationAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int cancellationId, string note, int? userId,
        CancellationToken ct)
    {
        var moved = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition("""
            UPDATE TravelBookingCancellations SET RefundStatus = N'ReceivedFromSupplier', UpdatedAt = SYSUTCDATETIME()
            OUTPUT inserted.BookingId
            WHERE Id = @Id AND TenantId = @TenantId AND RefundStatus = N'Pending'
            """, new { Id = cancellationId, TenantId = tenantId }, tx, cancellationToken: ct));
        if (moved is not int bookingId) return;
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TravelBookingHistory (TenantId, BookingId, ServiceType, ServiceId, EventType, FromValue, ToValue, Note, ChangedBy)
            SELECT @TenantId, b.Id, NULL, @CancellationId, N'RefundStatus', N'Pending', N'ReceivedFromSupplier', @Note, @UserId
            FROM TravelBookings b WHERE b.Id = @BookingId AND b.TenantId = @TenantId
            """, new { TenantId = tenantId, BookingId = bookingId, CancellationId = cancellationId, Note = note, UserId = userId }, tx, cancellationToken: ct));
    }

    /// <summary>Row already written with this idempotency key (locks the key range so a concurrent duplicate waits).</summary>
    private static Task<int?> ExistingByKeyAsync(IDbConnection connection, IDbTransaction tx, string table, int tenantId, Guid key, CancellationToken ct)
        => connection.ExecuteScalarAsync<int?>(new CommandDefinition(table switch
        {
            "TravelSupplierPayments" => "SELECT Id FROM TravelSupplierPayments WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @TenantId AND IdempotencyKey = @Key",
            "TravelSupplierCredits" => "SELECT Id FROM TravelSupplierCredits WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @TenantId AND IdempotencyKey = @Key",
            _ => "SELECT Id FROM TravelSupplierRefunds WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @TenantId AND IdempotencyKey = @Key"
        }, new { TenantId = tenantId, Key = key }, tx, cancellationToken: ct));
}
