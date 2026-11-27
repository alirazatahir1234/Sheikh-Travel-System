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
/// Payments, allocations, credit notes and refunds. Root predicate on the row's TenantId; joins repeat it. Every money write
/// locks the invoice / payment it changes (<c>UPDLOCK</c>), re-checks the cap with the same formula as the read side, and writes
/// history in the same transaction. Idempotency keys make retries return the original row.
/// </summary>
public sealed class TravelPaymentRepository(IDbConnectionFactory dbFactory) : ITravelPaymentRepository
{
    /// <summary>Per payment <c>py</c>: amounts applied to invoices and refunded from its advance (pm).</summary>
    internal const string PaymentMoney = """
        OUTER APPLY (
            SELECT COALESCE((SELECT SUM(a.AmountFromPayment) FROM TravelPaymentAllocations a WHERE a.PaymentId = py.Id AND a.TenantId = py.TenantId), 0) AS Allocated,
                   COALESCE((SELECT SUM(r.Amount) FROM TravelRefunds r WHERE r.PaymentId = py.Id AND r.TenantId = py.TenantId), 0) AS Refunded
        ) pm
        """;

    private const string ListSelect = """
        SELECT py.Id, py.ReceiptNumber, py.CustomerId, c.FullName AS CustomerName, py.ReceivedOn, py.Method, py.Reference, py.Amount, py.CurrencyCode,
               py.Status, pm.Allocated, pm.Refunded, py.ReconciledAt, py.BankReference, py.CreatedAt
        FROM TravelPayments py
        INNER JOIN Customers c ON c.Id = py.CustomerId AND c.TenantId = py.TenantId
        """;

    /// <summary>Invoice row locked for a money write, with its live balance and pending reservations.</summary>
    private const string LockInvoice = $"""
        SELECT i.Id, i.CustomerId, i.CurrencyCode, i.Status, i.Total, m.Paid, m.PendingPaid, m.Credited, m.Refunded
        FROM TravelInvoices i WITH (UPDLOCK, ROWLOCK)
        {TravelInvoiceRepository.Money}
        WHERE i.Id = @InvoiceId AND i.TenantId = @TenantId AND i.IsDeleted = 0
        """;

    private sealed class LockedInvoice : TravelInvoiceMoney
    {
        public int Id { get; init; }
        public int CustomerId { get; init; }
        public string CurrencyCode { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }

    // ── Reads ───────────────────────────────────────────────────────────────

    public async Task<TravelPaymentState?> GetStateAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Id", id);
        var clauses = new List<string> { "py.TenantId = @TenantId", "py.Id = @Id", "py.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "py.BranchId", clauses, p);
        return await connection.QuerySingleOrDefaultAsync<TravelPaymentState>(new CommandDefinition($"""
            SELECT py.Id, py.CustomerId, py.Amount, py.CurrencyCode, py.Status, py.Method, py.ReconciledAt, pm.Allocated, pm.Refunded
            FROM TravelPayments py
            {PaymentMoney}
            WHERE py.TenantId = @TenantId AND {string.Join(" AND ", clauses)}
            """, p, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TravelPaymentListItemDto>> GetPagedAsync(int tenantId, TravelBranchScope scope, TravelPaymentListFilter f, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "py.TenantId = @TenantId", "py.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "py.BranchId", clauses, p);
        if (f.Search is not null) { clauses.Add("(py.ReceiptNumber LIKE @Search OR py.Reference LIKE @Search OR py.BankReference LIKE @Search OR c.FullName LIKE @Search)"); p.Add("Search", $"%{TravelSql.EscapeLike(f.Search)}%"); }
        if (f.Status is not null) { clauses.Add("py.Status = @Status"); p.Add("Status", f.Status); }
        if (f.Method is not null) { clauses.Add("py.Method = @Method"); p.Add("Method", f.Method); }
        if (f.CustomerId.HasValue) { clauses.Add("py.CustomerId = @CustomerId"); p.Add("CustomerId", f.CustomerId); }
        if (f.ReceivedFrom.HasValue) { clauses.Add("py.ReceivedOn >= @From"); p.Add("From", f.ReceivedFrom.Value.Date); }
        if (f.ReceivedTo.HasValue) { clauses.Add("py.ReceivedOn <= @To"); p.Add("To", f.ReceivedTo.Value.Date); }
        if (f.Reconciled == true) clauses.Add("py.ReconciledAt IS NOT NULL");
        if (f.Reconciled == false) clauses.Add("py.ReconciledAt IS NULL AND py.Status IN (N'Completed', N'Refunded') AND py.Method <> N'Cash'");

        var where = string.Join(" AND ", clauses);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT COUNT(1) FROM TravelPayments py INNER JOIN Customers c ON c.Id = py.CustomerId AND c.TenantId = py.TenantId WHERE {where}
            """, p, cancellationToken: cancellationToken));
        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);
        var items = (await connection.QueryAsync<TravelPaymentListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {PaymentMoney}
            WHERE {where}
            ORDER BY py.ReceivedOn DESC, py.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken))).ToList();
        return new PagedResult<TravelPaymentListItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<TravelPaymentDto?> GetDetailAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var args = new { Id = id, TenantId = tenantId };
        var payment = await connection.QuerySingleOrDefaultAsync<TravelPaymentDto>(new CommandDefinition($"""
            SELECT py.Id, py.ReceiptNumber, py.CustomerId, c.FullName AS CustomerName, py.ReceivedOn, py.Method, py.Reference, py.Amount, py.CurrencyCode,
                   py.Status, pm.Allocated, pm.Refunded, py.ReconciledAt, py.BankReference, py.CreatedAt, py.Notes, py.ClearedAt, u.FullName AS CreatedByName
            FROM TravelPayments py
            INNER JOIN Customers c ON c.Id = py.CustomerId AND c.TenantId = py.TenantId
            LEFT JOIN Users u ON u.Id = py.CreatedBy AND u.TenantId = py.TenantId
            {PaymentMoney}
            WHERE py.Id = @Id AND py.TenantId = @TenantId AND py.IsDeleted = 0
            """, args, cancellationToken: cancellationToken));
        if (payment is null) return null;
        payment.Allocations = (await connection.QueryAsync<TravelInvoiceAllocationDto>(new CommandDefinition($"""
            {TravelInvoiceRepository.AllocationSelect}
            WHERE a.PaymentId = @Id AND a.TenantId = @TenantId
            ORDER BY a.Id
            """, args, cancellationToken: cancellationToken))).ToList();
        payment.Refunds = (await connection.QueryAsync<TravelRefundDto>(new CommandDefinition($"""
            {TravelInvoiceRepository.RefundSelect}
            WHERE r.PaymentId = @Id AND r.TenantId = @TenantId
            ORDER BY r.PaidOn, r.Id
            """, args, cancellationToken: cancellationToken))).ToList();
        payment.History = (await connection.QueryAsync<TravelFinanceHistoryDto>(new CommandDefinition($"""
            {TravelInvoiceRepository.HistorySelect}
            WHERE h.PaymentId = @Id AND h.TenantId = @TenantId
            ORDER BY h.ChangedAt DESC, h.Id DESC
            """, args, cancellationToken: cancellationToken))).ToList();
        return payment;
    }

    // ── Payments ────────────────────────────────────────────────────────────

    public async Task<TravelFinanceWriteResult> RecordAsync(int tenantId, TravelPaymentWrite w, IReadOnlyList<TravelAllocationWrite> allocations, int? userId,
        DateTime utcNow, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            var existing = await ExistingByKeyAsync(connection, tx, "TravelPayments", tenantId, w.IdempotencyKey, cancellationToken);
            if (existing is int replay)
            {
                tx.Commit();
                return new TravelFinanceWriteResult(replay, null, Replayed: true);
            }

            var number = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.Receipt, utcNow, cancellationToken);
            var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                INSERT INTO TravelPayments (TenantId, ReceiptNumber, CustomerId, ReceivedOn, Method, Reference, Amount, CurrencyCode, Status, IdempotencyKey,
                    ClearedAt, BranchId, Notes, CreatedBy)
                SELECT @TenantId, @Number, c.Id, @ReceivedOn, @Method, @Reference, @Amount, @CurrencyCode, @Status, @Key,
                    CASE WHEN @Status = N'Completed' THEN @Now END, @BranchId, @Notes, @UserId
                FROM Customers c WHERE c.Id = @CustomerId AND c.TenantId = @TenantId;
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new
                {
                    TenantId = tenantId, Number = number, w.CustomerId, ReceivedOn = w.ReceivedOn.Date, w.Method, w.Reference, w.Amount, w.CurrencyCode,
                    w.Status, Key = w.IdempotencyKey, Now = utcNow, w.BranchId, w.Notes, UserId = userId
                }, tx, cancellationToken: cancellationToken)) ?? 0;
            if (id == 0)
            {
                tx.Rollback();
                return TravelFinanceWriteResult.Fail("Customer not found.");
            }
            await TravelInvoiceRepository.HistoryAsync(connection, tx, tenantId, 0, "PaymentRecorded", null, w.Status, w.Amount, w.CurrencyCode,
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
                "SELECT Id FROM TravelPayments WHERE TenantId = @TenantId AND IdempotencyKey = @Key",
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
        var payment = await connection.QuerySingleOrNullAsync<(int CustomerId, string CurrencyCode, decimal Amount, string Status, decimal Allocated, decimal Refunded)>(
            new CommandDefinition($"""
                SELECT py.CustomerId, py.CurrencyCode, py.Amount, py.Status, pm.Allocated, pm.Refunded
                FROM TravelPayments py WITH (UPDLOCK, ROWLOCK)
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
        var error = await ApplyAsync(connection, tx, tenantId, paymentId, pay.CustomerId, pay.CurrencyCode, allocations, userId, cancellationToken);
        if (error is not null)
        {
            tx.Rollback();
            return TravelFinanceWriteResult.Fail(error);
        }
        tx.Commit();
        return new TravelFinanceWriteResult(paymentId, null);
    }

    /// <summary>Locks each invoice, re-checks its open balance, and writes (or tops up) the allocation.</summary>
    private static async Task<string?> ApplyAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int paymentId, int customerId, string paymentCurrency,
        IReadOnlyList<TravelAllocationWrite> allocations, int? userId, CancellationToken ct)
    {
        foreach (var a in allocations)
        {
            var invoice = await connection.QuerySingleOrDefaultAsync<LockedInvoice>(new CommandDefinition(LockInvoice,
                new { InvoiceId = a.InvoiceId, TenantId = tenantId, Today = DateTime.UtcNow.Date }, tx, cancellationToken: ct));
            if (invoice is null || invoice.Status != "Issued" || invoice.CustomerId != customerId)
                return "An invoice to pay is not an open invoice of this customer.";
            var available = Math.Max(0, invoice.Balance - invoice.PendingPaid);
            if (a.AmountApplied > available)
                return $"{a.AmountApplied:0.##} {invoice.CurrencyCode} is more than the open balance of the invoice ({available:0.##} {invoice.CurrencyCode}).";

            var existingRate = await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition("""
                SELECT ExchangeRate FROM TravelPaymentAllocations WITH (UPDLOCK) WHERE PaymentId = @PaymentId AND InvoiceId = @InvoiceId AND TenantId = @TenantId
                """, new { PaymentId = paymentId, a.InvoiceId, TenantId = tenantId }, tx, cancellationToken: ct));
            if (existingRate is decimal rate && rate != a.ExchangeRate)
                return "This payment is already applied to the invoice at a different rate — remove that allocation first.";
            await connection.ExecuteAsync(new CommandDefinition(existingRate is null
                ? """
                  INSERT INTO TravelPaymentAllocations (TenantId, PaymentId, InvoiceId, AmountFromPayment, ExchangeRate, AmountApplied, CreatedBy)
                  VALUES (@TenantId, @PaymentId, @InvoiceId, @FromPayment, @Rate, @Applied, @UserId)
                  """
                : """
                  UPDATE TravelPaymentAllocations SET AmountFromPayment = AmountFromPayment + @FromPayment, AmountApplied = AmountApplied + @Applied
                  WHERE PaymentId = @PaymentId AND InvoiceId = @InvoiceId AND TenantId = @TenantId
                  """,
                new { TenantId = tenantId, PaymentId = paymentId, a.InvoiceId, FromPayment = a.AmountFromPayment, Rate = a.ExchangeRate, Applied = a.AmountApplied, UserId = userId },
                tx, cancellationToken: ct));
            await TravelInvoiceRepository.HistoryAsync(connection, tx, tenantId, a.InvoiceId, "Allocated", null, null, a.AmountApplied, invoice.CurrencyCode,
                paymentCurrency == invoice.CurrencyCode ? null : $"{a.AmountFromPayment:0.##} {paymentCurrency} at {a.ExchangeRate:0.######}", userId, ct, paymentId: paymentId);
        }
        return null;
    }

    public async Task<bool> UnallocateAsync(int tenantId, int paymentId, int invoiceId, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var removed = await connection.QuerySingleOrNullAsync<(decimal Applied, string CurrencyCode)>(new CommandDefinition("""
            DELETE a
            OUTPUT deleted.AmountApplied, iv.CurrencyCode
            FROM TravelPaymentAllocations a
            INNER JOIN TravelInvoices iv ON iv.Id = a.InvoiceId AND iv.TenantId = a.TenantId AND iv.Status = N'Issued'
            INNER JOIN TravelPayments py ON py.Id = a.PaymentId AND py.TenantId = a.TenantId AND py.ReconciledAt IS NULL
            WHERE a.PaymentId = @PaymentId AND a.InvoiceId = @InvoiceId AND a.TenantId = @TenantId
            """, new { PaymentId = paymentId, InvoiceId = invoiceId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (removed is null)
        {
            tx.Rollback();
            return false;
        }
        await TravelInvoiceRepository.HistoryAsync(connection, tx, tenantId, invoiceId, "Unallocated", null, null, removed.Value.Applied, removed.Value.CurrencyCode,
            null, userId, cancellationToken, paymentId: paymentId);
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
            UPDATE TravelPayments SET Status = @NewStatus, ClearedAt = CASE WHEN @NewStatus = N'Completed' THEN @Now ELSE ClearedAt END, UpdatedAt = SYSUTCDATETIME()
            OUTPUT inserted.Amount, inserted.CurrencyCode
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = @Expected AND ReconciledAt IS NULL
            """, new { Id = paymentId, TenantId = tenantId, Expected = expectedStatus, NewStatus = newStatus, Now = utcNow }, tx, cancellationToken: cancellationToken));
        if (changed is null)
        {
            tx.Rollback();
            return false;
        }
        await TravelInvoiceRepository.HistoryAsync(connection, tx, tenantId, 0, "PaymentStatus", expectedStatus, newStatus, changed.Value.Amount,
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
            UPDATE TravelPayments SET ReconciledAt = @Now, ReconciledBy = @UserId, BankReference = @BankReference, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND ReconciledAt IS NULL AND Status IN (N'Completed', N'Refunded')
            """, new { Id = paymentId, TenantId = tenantId, Now = utcNow, UserId = userId, BankReference = bankReference }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }
        await TravelInvoiceRepository.HistoryAsync(connection, tx, tenantId, 0, "Reconciled", null, bankReference, null, null, note, userId, cancellationToken,
            paymentId: paymentId);
        tx.Commit();
        return true;
    }

    // ── Credit notes ────────────────────────────────────────────────────────

    public async Task<TravelCreditNoteState?> GetCreditNoteAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelCreditNoteState>(new CommandDefinition("""
            SELECT Id, InvoiceId, Status, Amount FROM TravelCreditNotes WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TravelCreditNoteDto>> GetCreditNotesAsync(int tenantId, TravelBranchScope scope, int? customerId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "cn.TenantId = @TenantId" };
        TravelSql.ApplyBranchScope(scope, "iv.BranchId", clauses, p);
        if (customerId.HasValue) { clauses.Add("cn.CustomerId = @CustomerId"); p.Add("CustomerId", customerId); }
        if (from.HasValue) { clauses.Add("cn.IssuedOn >= @From"); p.Add("From", from.Value.Date); }
        if (to.HasValue) { clauses.Add("cn.IssuedOn <= @To"); p.Add("To", to.Value.Date); }
        var where = string.Join(" AND ", clauses);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT COUNT(1) FROM TravelCreditNotes cn INNER JOIN TravelInvoices iv ON iv.Id = cn.InvoiceId AND iv.TenantId = cn.TenantId WHERE {where}
            """, p, cancellationToken: cancellationToken));
        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);
        var items = (await connection.QueryAsync<TravelCreditNoteDto>(new CommandDefinition($"""
            {TravelInvoiceRepository.CreditNoteSelect}
            WHERE {where}
            ORDER BY cn.IssuedOn DESC, cn.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken))).ToList();
        return new PagedResult<TravelCreditNoteDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<TravelFinanceWriteResult> IssueCreditNoteAsync(int tenantId, Guid idempotencyKey, int invoiceId, decimal amount, string reason, int? cancellationId,
        DateTime issuedOn, int? userId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            if (await ExistingByKeyAsync(connection, tx, "TravelCreditNotes", tenantId, idempotencyKey, cancellationToken) is int replay)
            {
                tx.Commit();
                return new TravelFinanceWriteResult(replay, null, Replayed: true);
            }
            var invoice = await connection.QuerySingleOrDefaultAsync<LockedInvoice>(new CommandDefinition(LockInvoice,
                new { InvoiceId = invoiceId, TenantId = tenantId, Today = utcNow.Date }, tx, cancellationToken: cancellationToken));
            if (invoice is null || invoice.Status != "Issued")
            {
                tx.Rollback();
                return TravelFinanceWriteResult.Fail("Credit notes are issued against issued invoices.");
            }
            if (amount > invoice.Total - invoice.Credited)
            {
                tx.Rollback();
                return TravelFinanceWriteResult.Fail($"At most {invoice.Total - invoice.Credited:0.##} {invoice.CurrencyCode} can still be credited.");
            }
            var number = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.CreditNote, utcNow, cancellationToken);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO TravelCreditNotes (TenantId, CreditNoteNumber, InvoiceId, CustomerId, Amount, CurrencyCode, Reason, CancellationId, Status, IssuedOn,
                    IdempotencyKey, CreatedBy)
                VALUES (@TenantId, @Number, @InvoiceId, @CustomerId, @Amount, @Currency, @Reason, @CancellationId, N'Issued', @IssuedOn, @Key, @UserId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new
                {
                    TenantId = tenantId, Number = number, InvoiceId = invoiceId, invoice.CustomerId, Amount = amount, Currency = invoice.CurrencyCode,
                    Reason = reason, CancellationId = cancellationId, IssuedOn = issuedOn.Date, Key = idempotencyKey, UserId = userId
                }, tx, cancellationToken: cancellationToken));
            await TravelInvoiceRepository.HistoryAsync(connection, tx, tenantId, invoiceId, "CreditNoteIssued", null, number, amount, invoice.CurrencyCode, reason,
                userId, cancellationToken, creditNoteId: id);
            tx.Commit();
            return new TravelFinanceWriteResult(id, null);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            tx.Rollback();
            return TravelFinanceWriteResult.Fail("This credit note is being issued already. Reload the invoice.");
        }
    }

    public async Task<bool> VoidCreditNoteAsync(int tenantId, int creditNoteId, string reason, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var args = new { Id = creditNoteId, TenantId = tenantId, Reason = reason };
        // Lock the invoice first, then re-check that no refund depends on the credit.
        var invoiceId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT iv.Id FROM TravelInvoices iv WITH (UPDLOCK, ROWLOCK)
            INNER JOIN TravelCreditNotes cn ON cn.InvoiceId = iv.Id AND cn.TenantId = iv.TenantId
            WHERE cn.Id = @Id AND cn.TenantId = @TenantId AND cn.Status = N'Issued'
              AND NOT EXISTS (SELECT 1 FROM TravelRefunds r WHERE r.InvoiceId = iv.Id AND r.TenantId = iv.TenantId)
            """, args, tx, cancellationToken: cancellationToken));
        if (invoiceId is null)
        {
            tx.Rollback();
            return false;
        }
        var voided = await connection.QuerySingleOrNullAsync<(decimal Amount, string CurrencyCode, string Number)>(new CommandDefinition("""
            UPDATE TravelCreditNotes SET Status = N'Void', VoidReason = @Reason, UpdatedAt = SYSUTCDATETIME()
            OUTPUT inserted.Amount, inserted.CurrencyCode, inserted.CreditNoteNumber
            WHERE Id = @Id AND TenantId = @TenantId AND Status = N'Issued'
            """, args, tx, cancellationToken: cancellationToken));
        if (voided is null)
        {
            tx.Rollback();
            return false;
        }
        await TravelInvoiceRepository.HistoryAsync(connection, tx, tenantId, invoiceId.Value, "CreditNoteVoided", voided.Value.Number, "Void", voided.Value.Amount,
            voided.Value.CurrencyCode, reason, userId, cancellationToken, creditNoteId: creditNoteId);
        tx.Commit();
        return true;
    }

    // ── Refunds ─────────────────────────────────────────────────────────────

    public async Task<PagedResult<TravelRefundDto>> GetRefundsAsync(int tenantId, TravelBranchScope scope, int? customerId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "r.TenantId = @TenantId" };
        TravelSql.ApplyBranchScope(scope, "COALESCE(iv.BranchId, py.BranchId)", clauses, p);
        if (customerId.HasValue) { clauses.Add("r.CustomerId = @CustomerId"); p.Add("CustomerId", customerId); }
        if (from.HasValue) { clauses.Add("r.PaidOn >= @From"); p.Add("From", from.Value.Date); }
        if (to.HasValue) { clauses.Add("r.PaidOn <= @To"); p.Add("To", to.Value.Date); }
        var where = string.Join(" AND ", clauses);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT COUNT(1) FROM TravelRefunds r
            LEFT JOIN TravelInvoices iv ON iv.Id = r.InvoiceId AND iv.TenantId = r.TenantId
            LEFT JOIN TravelPayments py ON py.Id = r.PaymentId AND py.TenantId = r.TenantId
            WHERE {where}
            """, p, cancellationToken: cancellationToken));
        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);
        var items = (await connection.QueryAsync<TravelRefundDto>(new CommandDefinition($"""
            {TravelInvoiceRepository.RefundSelect}
            WHERE {where}
            ORDER BY r.PaidOn DESC, r.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken))).ToList();
        return new PagedResult<TravelRefundDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<TravelFinanceWriteResult> RecordRefundAsync(int tenantId, RecordTravelRefundDto refund, int customerId, string currencyCode, int? userId,
        DateTime utcNow, bool advanceCancellation, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            if (await ExistingByKeyAsync(connection, tx, "TravelRefunds", tenantId, refund.IdempotencyKey, cancellationToken) is int replay)
            {
                tx.Commit();
                return new TravelFinanceWriteResult(replay, null, Replayed: true);
            }

            var fullyRefundsPayment = false;
            if (refund.InvoiceId is int invoiceId)
            {
                var invoice = await connection.QuerySingleOrDefaultAsync<LockedInvoice>(new CommandDefinition(LockInvoice,
                    new { InvoiceId = invoiceId, TenantId = tenantId, Today = utcNow.Date }, tx, cancellationToken: cancellationToken));
                if (invoice is null || invoice.Status != "Issued" || refund.Amount > -invoice.Balance)
                {
                    tx.Rollback();
                    return TravelFinanceWriteResult.Fail("The invoice no longer owes that much back. Reload it.");
                }
            }
            else
            {
                var pay = await connection.QuerySingleOrNullAsync<(string Status, decimal Amount, decimal Allocated, decimal Refunded)>(new CommandDefinition($"""
                    SELECT py.Status, py.Amount, pm.Allocated, pm.Refunded
                    FROM TravelPayments py WITH (UPDLOCK, ROWLOCK)
                    {PaymentMoney}
                    WHERE py.Id = @Id AND py.TenantId = @TenantId AND py.IsDeleted = 0
                    """, new { Id = refund.PaymentId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
                if (pay is not { } p || p.Status != "Completed" || refund.Amount > p.Amount - p.Allocated - p.Refunded)
                {
                    tx.Rollback();
                    return TravelFinanceWriteResult.Fail("The payment no longer has that much unallocated. Reload it.");
                }
                fullyRefundsPayment = p.Allocated == 0 && p.Refunded + refund.Amount == p.Amount;
            }

            var number = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.Refund, utcNow, cancellationToken);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO TravelRefunds (TenantId, RefundNumber, CustomerId, InvoiceId, PaymentId, Amount, CurrencyCode, Method, Reference, PaidOn,
                    CancellationId, Notes, IdempotencyKey, CreatedBy)
                VALUES (@TenantId, @Number, @CustomerId, @InvoiceId, @PaymentId, @Amount, @Currency, @Method, @Reference, @PaidOn,
                    @CancellationId, @Notes, @Key, @UserId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new
                {
                    TenantId = tenantId, Number = number, CustomerId = customerId, refund.InvoiceId, refund.PaymentId, refund.Amount, Currency = currencyCode,
                    refund.Method, Reference = string.IsNullOrWhiteSpace(refund.Reference) ? null : refund.Reference.Trim(), PaidOn = refund.PaidOn.Date,
                    refund.CancellationId, Notes = string.IsNullOrWhiteSpace(refund.Notes) ? null : refund.Notes.Trim(), Key = refund.IdempotencyKey, UserId = userId
                }, tx, cancellationToken: cancellationToken));
            if (fullyRefundsPayment)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE TravelPayments SET Status = N'Refunded', UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND TenantId = @TenantId AND Status = N'Completed'
                    """, new { Id = refund.PaymentId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
            }
            await TravelInvoiceRepository.HistoryAsync(connection, tx, tenantId, refund.InvoiceId ?? 0, "RefundPaid", null, number, refund.Amount, currencyCode,
                refund.Notes, userId, cancellationToken, paymentId: refund.PaymentId, refundId: id);

            if (advanceCancellation && refund.CancellationId is int cancellationId)
            {
                var moved = await connection.QuerySingleOrNullAsync<(int BookingId, string From)>(new CommandDefinition("""
                    UPDATE TravelBookingCancellations SET RefundStatus = N'PaidToCustomer', UpdatedAt = SYSUTCDATETIME()
                    OUTPUT inserted.BookingId, deleted.RefundStatus
                    WHERE Id = @Id AND TenantId = @TenantId AND RefundStatus IN (N'Pending', N'ReceivedFromSupplier')
                    """, new { Id = cancellationId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
                if (moved is { } m)
                {
                    await connection.ExecuteAsync(new CommandDefinition("""
                        INSERT INTO TravelBookingHistory (TenantId, BookingId, ServiceType, ServiceId, EventType, FromValue, ToValue, Note, ChangedBy)
                        SELECT @TenantId, b.Id, NULL, @CancellationId, N'RefundStatus', @From, N'PaidToCustomer', @Note, @UserId
                        FROM TravelBookings b WHERE b.Id = @BookingId AND b.TenantId = @TenantId
                        """, new { TenantId = tenantId, m.BookingId, CancellationId = cancellationId, m.From, Note = $"Refund {number}", UserId = userId },
                        tx, cancellationToken: cancellationToken));
                }
            }
            tx.Commit();
            return new TravelFinanceWriteResult(id, null);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            tx.Rollback();
            return TravelFinanceWriteResult.Fail("This refund is being recorded already. Reload the record.");
        }
    }

    public async Task<TravelCancellationFacts?> GetCancellationFactsAsync(int tenantId, int cancellationId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelCancellationFacts>(new CommandDefinition("""
            SELECT x.Id, x.BookingId, b.CustomerId, x.RefundStatus, x.RefundToCustomer, x.CurrencyCode
            FROM TravelBookingCancellations x
            INNER JOIN TravelBookings b ON b.Id = x.BookingId AND b.TenantId = x.TenantId
            WHERE x.Id = @Id AND x.TenantId = @TenantId
            """, new { Id = cancellationId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Row already written with this idempotency key (locks the key range so a concurrent duplicate waits).</summary>
    private static Task<int?> ExistingByKeyAsync(IDbConnection connection, IDbTransaction tx, string table, int tenantId, Guid key, CancellationToken ct)
        => connection.ExecuteScalarAsync<int?>(new CommandDefinition(table switch
        {
            "TravelPayments" => "SELECT Id FROM TravelPayments WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @TenantId AND IdempotencyKey = @Key",
            "TravelCreditNotes" => "SELECT Id FROM TravelCreditNotes WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @TenantId AND IdempotencyKey = @Key",
            _ => "SELECT Id FROM TravelRefunds WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @TenantId AND IdempotencyKey = @Key"
        }, new { TenantId = tenantId, Key = key }, tx, cancellationToken: ct));
}
