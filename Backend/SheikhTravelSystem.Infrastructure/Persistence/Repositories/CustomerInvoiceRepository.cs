using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Invoices;
using SheikhTravelSystem.Application.Features.Invoices.DTOs;
using SheikhTravelSystem.Application.Features.SmartOps;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class CustomerInvoiceRepository(IDbConnectionFactory dbFactory) : ICustomerInvoiceRepository
{
    private const string InvoiceFromJoin = """
        FROM CustomerInvoices i
        LEFT JOIN Trips tr ON tr.Id = i.TripId AND tr.TenantId = i.TenantId AND tr.IsDeleted = 0
        LEFT JOIN Bookings b ON b.Id = COALESCE(i.BookingId, tr.BookingId) AND b.TenantId = i.TenantId AND b.IsDeleted = 0
        LEFT JOIN Vehicles v ON v.Id = COALESCE(tr.VehicleId, b.VehicleId) AND v.TenantId = i.TenantId AND v.IsDeleted = 0
        LEFT JOIN Drivers d ON d.Id = COALESCE(tr.DriverId, b.DriverId) AND d.TenantId = i.TenantId AND d.IsDeleted = 0
        """;

    public async Task<PagedResult<InvoiceDto>> GetPagedAsync(
        int tenantId,
        int page,
        int pageSize,
        string? status,
        string? paymentStatus,
        int? bookingId,
        int? tripId,
        int? customerId,
        DataScopeResult? scope,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await SyncOverdueStatusesAsync(tenantId, DateTime.UtcNow, cancellationToken);

        var offset = (page - 1) * pageSize;
        var clauses = new List<string> { "i.IsDeleted = 0", "i.TenantId = @TenantId" };
        if (!string.IsNullOrWhiteSpace(status)) clauses.Add("i.Status = @Status");
        if (bookingId.HasValue) clauses.Add("i.BookingId = @BookingId");
        if (tripId.HasValue) clauses.Add("i.TripId = @TripId");
        if (customerId.HasValue) clauses.Add("i.CustomerId = @CustomerId");

        var parameters = new DynamicParameters(new
        {
            TenantId = tenantId,
            Status = status,
            BookingId = bookingId,
            TripId = tripId,
            CustomerId = customerId,
            Offset = offset,
            PageSize = pageSize,
            AsOfUtc = DateTime.UtcNow,
            GraceHours = SmartOpsRuleLogic.OverdueInvoiceGraceHours
        });

        if (scope is not null)
            DataScopeSqlBuilder.ApplyLinkedFleetScope(parameters, scope, clauses, "v", "d");

        if (!string.IsNullOrWhiteSpace(paymentStatus))
        {
            var ps = paymentStatus.Trim();
            if (ps.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                clauses.Add("(i.Status = N'Paid' OR i.Balance <= 0)");
            else if (ps.Equals("Overdue", StringComparison.OrdinalIgnoreCase))
                clauses.Add("""
                    i.Balance > 0 AND i.Status NOT IN (N'Void', N'Paid', N'Draft')
                    AND (
                        i.Status = N'Overdue'
                        OR DATEADD(hour, @GraceHours, COALESCE(i.DueDate, i.IssuedAt)) < @AsOfUtc
                    )
                    """);
            else if (ps.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                clauses.Add("""
                    i.Balance > 0 AND i.Status NOT IN (N'Void', N'Paid', N'Draft', N'Overdue')
                    AND (
                        i.Status IN (N'Pending', N'Issued')
                        AND DATEADD(hour, @GraceHours, COALESCE(i.DueDate, i.IssuedAt)) >= @AsOfUtc
                    )
                    """);
        }

        var where = "WHERE " + string.Join(" AND ", clauses);

        var rows = (await connection.QueryAsync<InvoiceRow>(new CommandDefinition($"""
            SELECT i.Id, i.BookingId, i.TripId, i.CustomerId, i.InvoiceNumber, i.TotalAmount, i.PaidAmount, i.Balance,
                   i.Status, i.IssuedAt, i.DueDate, i.CustomerName, i.CustomerEmail, i.BookingNumber, i.TripNumber,
                   i.CreatedAt, i.LastEmailedAt
            {InvoiceFromJoin}
            {where}
            ORDER BY i.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, parameters, cancellationToken: cancellationToken))).ToList();

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) {InvoiceFromJoin} {where}",
            parameters, cancellationToken: cancellationToken));

        var asOf = DateTime.UtcNow;
        var items = rows.Select(r => MapListRow(r, asOf)).ToList();

        return new PagedResult<InvoiceDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<InvoiceDetailDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<InvoiceDetailRow>(new CommandDefinition("""
            SELECT Id, BookingId, TripId, CustomerId, InvoiceNumber, TotalAmount, PaidAmount, Balance,
                   Status, IssuedAt, DueDate, CustomerName, CustomerEmail, BookingNumber, TripNumber,
                   RouteName, Notes, CreatedAt, UpdatedAt, LastEmailedAt, EmailSentCount
            FROM CustomerInvoices
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));

        return row is null ? null : MapDetailRow(row, DateTime.UtcNow);
    }

    public async Task<InvoiceBookingSnapshot?> GetBookingSnapshotAsync(
        int bookingId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<InvoiceBookingSnapshot>(new CommandDefinition("""
            SELECT b.Id AS BookingId, b.CustomerId, b.TotalAmount, b.BookingNumber, b.Status,
                   c.FullName AS CustomerName, c.Email AS CustomerEmail,
                   CASE WHEN r.Id IS NULL THEN NULL ELSE r.Source + N' -> ' + r.Destination END AS RouteName
            FROM Bookings b
            LEFT JOIN Customers c ON c.Id = b.CustomerId AND c.TenantId = @TenantId
            LEFT JOIN Routes r ON r.Id = b.RouteId AND r.TenantId = @TenantId
            WHERE b.Id = @BookingId AND b.TenantId = @TenantId AND b.IsDeleted = 0
            """, new { BookingId = bookingId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<InvoiceTripSnapshot?> GetTripSnapshotAsync(
        int tripId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<InvoiceTripSnapshot>(new CommandDefinition("""
            SELECT
                t.Id AS TripId,
                t.BookingId,
                t.CustomerId,
                t.VehicleId,
                t.TripNumber,
                t.Status,
                c.FullName AS CustomerName,
                c.Email AS CustomerEmail,
                b.BookingNumber,
                COALESCE(b.TotalAmount, r.BasePrice, 0) AS TotalAmount,
                CASE
                    WHEN r.Id IS NOT NULL THEN r.Source + N' -> ' + r.Destination
                    WHEN t.PickupAddress IS NOT NULL AND t.DestinationAddress IS NOT NULL
                        THEN t.PickupAddress + N' -> ' + t.DestinationAddress
                    ELSE t.TripName
                END AS RouteName
            FROM Trips t
            LEFT JOIN Customers c ON c.Id = t.CustomerId AND c.TenantId = @TenantId
            LEFT JOIN Bookings b ON b.Id = t.BookingId AND b.TenantId = @TenantId AND b.IsDeleted = 0
            LEFT JOIN Routes r ON r.Id = COALESCE(t.RouteId, b.RouteId) AND r.TenantId = @TenantId
            WHERE t.Id = @TripId AND t.TenantId = @TenantId AND t.IsDeleted = 0
            """, new { TripId = tripId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<decimal> GetTotalPaidForBookingAsync(
        int bookingId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT ISNULL(SUM(Amount), 0)
            FROM Payments
            WHERE BookingId = @BookingId AND TenantId = @TenantId AND IsDeleted = 0
              AND Status IN (@Paid, @Partial)
            """, new
            {
                BookingId = bookingId,
                TenantId = tenantId,
                Paid = (int)PaymentStatus.Paid,
                Partial = (int)PaymentStatus.PartiallyPaid
            }, cancellationToken: cancellationToken));
    }

    public async Task<bool> HasActiveInvoiceForBookingAsync(
        int bookingId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM CustomerInvoices
                WHERE BookingId = @BookingId AND TenantId = @TenantId AND IsDeleted = 0
                  AND Status <> N'Void'
            ) THEN 1 ELSE 0 END
            """, new { BookingId = bookingId, TenantId = tenantId }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> HasActiveInvoiceForTripAsync(
        int tripId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM CustomerInvoices
                WHERE TripId = @TripId AND TenantId = @TenantId AND IsDeleted = 0
                  AND Status <> N'Void'
            ) THEN 1 ELSE 0 END
            """, new { TripId = tripId, TenantId = tenantId }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<string> AllocateInvoiceNumberAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var prefix = await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            SELECT TOP 1 Value FROM PlatformSettings
            WHERE TenantId = @TenantId AND Category = N'Numbering' AND [Key] = N'InvoicePrefix'
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(prefix))
            prefix = "INV-";

        var counterRaw = await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            SELECT TOP 1 Value FROM PlatformSettings
            WHERE TenantId = @TenantId AND Category = N'Numbering' AND [Key] = N'InvoiceSequence'
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        int next;
        if (int.TryParse(counterRaw, out var counter))
        {
            next = counter + 1;
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM PlatformSettings WHERE TenantId = @TenantId AND Category = N'Numbering' AND [Key] = N'InvoiceSequence')
                    UPDATE PlatformSettings SET Value = @Value, UpdatedAt = SYSUTCDATETIME()
                    WHERE TenantId = @TenantId AND Category = N'Numbering' AND [Key] = N'InvoiceSequence';
                ELSE
                    INSERT INTO PlatformSettings (TenantId, Category, [Key], Value, DataType)
                    VALUES (@TenantId, N'Numbering', N'InvoiceSequence', @Value, N'number');
                """, new { TenantId = tenantId, Value = next.ToString() }, cancellationToken: cancellationToken));
        }
        else
        {
            var maxSeq = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                SELECT MAX(TRY_CAST(
                    CASE
                        WHEN CHARINDEX(N'-', InvoiceNumber) > 0
                        THEN RIGHT(InvoiceNumber, CHARINDEX(N'-', REVERSE(InvoiceNumber)) - 1)
                        ELSE InvoiceNumber
                    END AS INT))
                FROM CustomerInvoices
                WHERE TenantId = @TenantId AND IsDeleted = 0
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

            next = (maxSeq ?? 0) + 1;
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM PlatformSettings WHERE TenantId = @TenantId AND Category = N'Numbering' AND [Key] = N'InvoiceSequence')
                    INSERT INTO PlatformSettings (TenantId, Category, [Key], Value, DataType)
                    VALUES (@TenantId, N'Numbering', N'InvoiceSequence', @Value, N'number');
                ELSE
                    UPDATE PlatformSettings SET Value = @Value, UpdatedAt = SYSUTCDATETIME()
                    WHERE TenantId = @TenantId AND Category = N'Numbering' AND [Key] = N'InvoiceSequence';
                """, new { TenantId = tenantId, Value = next.ToString() }, cancellationToken: cancellationToken));
        }

        return $"{prefix.Trim()}{DateTime.UtcNow.Year}-{next:D4}";
    }

    public async Task<int> CreateAsync(
        int tenantId,
        int? createdBy,
        InvoiceCreatePayload payload,
        string invoiceNumber,
        decimal paidAmount,
        decimal balance,
        InvoiceStatus status,
        DateTime issuedAtUtc,
        DateTime dueDateUtc,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO CustomerInvoices
                (TenantId, BookingId, TripId, CustomerId, InvoiceNumber, TotalAmount, PaidAmount, Balance, Status,
                 IssuedAt, DueDate, CustomerName, CustomerEmail, BookingNumber, TripNumber, RouteName,
                 CreatedAt, CreatedBy, IsDeleted, EmailSentCount)
            VALUES
                (@TenantId, @BookingId, @TripId, @CustomerId, @InvoiceNumber, @TotalAmount, @PaidAmount, @Balance, @Status,
                 @IssuedAt, @DueDate, @CustomerName, @CustomerEmail, @BookingNumber, @TripNumber, @RouteName,
                 SYSUTCDATETIME(), @CreatedBy, 0, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
            {
                TenantId = tenantId,
                payload.BookingId,
                payload.TripId,
                payload.CustomerId,
                InvoiceNumber = invoiceNumber,
                payload.TotalAmount,
                PaidAmount = paidAmount,
                Balance = balance,
                Status = status.ToString(),
                IssuedAt = issuedAtUtc,
                DueDate = dueDateUtc,
                payload.CustomerName,
                payload.CustomerEmail,
                payload.BookingNumber,
                payload.TripNumber,
                payload.RouteName,
                CreatedBy = createdBy
            }, cancellationToken: cancellationToken));
    }

    public async Task<bool> ExistsAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM CustomerInvoices WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task VoidAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE CustomerInvoices
            SET Status = N'Void', UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task MarkPaidAsync(
        int id, int tenantId, decimal paidAmount, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE CustomerInvoices
            SET PaidAmount = @PaidAmount, Balance = 0, Status = N'Paid', UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId, PaidAmount = paidAmount },
            cancellationToken: cancellationToken));
    }

    public async Task RecordEmailSentAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE CustomerInvoices
            SET LastEmailedAt = SYSUTCDATETIME(),
                EmailSentCount = EmailSentCount + 1,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task SyncOverdueStatusesAsync(int tenantId, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE i SET Status = N'Overdue', UpdatedAt = SYSUTCDATETIME()
            FROM CustomerInvoices i
            WHERE i.TenantId = @TenantId AND i.IsDeleted = 0
              AND i.Balance > 0
              AND i.Status IN (N'Pending', N'Issued')
              AND DATEADD(hour, @GraceHours, COALESCE(i.DueDate, i.IssuedAt)) < @AsOfUtc;

            UPDATE i SET Status = N'Pending', UpdatedAt = SYSUTCDATETIME()
            FROM CustomerInvoices i
            WHERE i.TenantId = @TenantId AND i.IsDeleted = 0
              AND i.Balance > 0
              AND i.Status = N'Overdue'
              AND DATEADD(hour, @GraceHours, COALESCE(i.DueDate, i.IssuedAt)) >= @AsOfUtc;
            """, new
            {
                TenantId = tenantId,
                AsOfUtc = asOfUtc,
                GraceHours = SmartOpsRuleLogic.OverdueInvoiceGraceHours
            }, cancellationToken: cancellationToken));
    }

    public async Task<InvoiceBillingSummaryDto> GetBillingSummaryAsync(
        int tenantId,
        DataScopeResult? scope,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await SyncOverdueStatusesAsync(tenantId, asOfUtc, cancellationToken);

        var clauses = new List<string> { "i.IsDeleted = 0", "i.TenantId = @TenantId", "i.Status <> N'Void'" };
        var parameters = new DynamicParameters(new
        {
            TenantId = tenantId,
            AsOfUtc = asOfUtc,
            GraceHours = SmartOpsRuleLogic.OverdueInvoiceGraceHours
        });

        if (scope is not null)
            DataScopeSqlBuilder.ApplyLinkedFleetScope(parameters, scope, clauses, "v", "d");

        var where = "WHERE " + string.Join(" AND ", clauses);

        return await connection.QuerySingleAsync<InvoiceBillingSummaryDto>(new CommandDefinition($"""
            SELECT
                ISNULL(SUM(i.TotalAmount), 0) AS TotalBilled,
                ISNULL(SUM(i.PaidAmount), 0) AS TotalCollected,
                ISNULL(SUM(CASE WHEN i.Balance > 0 THEN i.Balance ELSE 0 END), 0) AS Outstanding,
                SUM(CASE WHEN i.Balance > 0 AND i.Status IN (N'Pending', N'Issued') THEN 1 ELSE 0 END) AS PendingCount,
                SUM(CASE WHEN i.Balance > 0 AND i.Status = N'Overdue' THEN 1 ELSE 0 END) AS OverdueCount,
                SUM(CASE WHEN i.Status = N'Paid' OR i.Balance <= 0 THEN 1 ELSE 0 END) AS PaidCount
            {InvoiceFromJoin}
            {where}
            """, parameters, cancellationToken: cancellationToken));
    }

    private static InvoiceDto MapListRow(InvoiceRow r, DateTime asOfUtc)
    {
        var status = InvoiceLifecycleHelper.NormalizeStoredStatus(r.Status);
        var paymentStatus = InvoiceLifecycleHelper.ResolvePaymentStatus(
            status, r.Balance, r.DueDate, r.IssuedAt, asOfUtc);
        return new InvoiceDto(
            r.Id, r.BookingId, r.TripId, r.CustomerId, r.InvoiceNumber, r.TotalAmount, r.PaidAmount, r.Balance,
            status, paymentStatus, r.IssuedAt, r.DueDate, r.CustomerName, r.CustomerEmail,
            r.BookingNumber, r.TripNumber, r.CreatedAt, r.LastEmailedAt);
    }

    private static InvoiceDetailDto MapDetailRow(InvoiceDetailRow r, DateTime asOfUtc)
    {
        var status = InvoiceLifecycleHelper.NormalizeStoredStatus(r.Status);
        var paymentStatus = InvoiceLifecycleHelper.ResolvePaymentStatus(
            status, r.Balance, r.DueDate, r.IssuedAt, asOfUtc);
        return new InvoiceDetailDto(
            r.Id, r.BookingId, r.TripId, r.CustomerId, r.InvoiceNumber, r.TotalAmount, r.PaidAmount, r.Balance,
            status, paymentStatus, r.IssuedAt, r.DueDate, r.CustomerName, r.CustomerEmail,
            r.BookingNumber, r.TripNumber, r.RouteName, r.Notes, r.CreatedAt, r.UpdatedAt,
            r.LastEmailedAt, r.EmailSentCount);
    }

    private class InvoiceRow
    {
        public int Id { get; init; }
        public int? BookingId { get; init; }
        public int? TripId { get; init; }
        public int CustomerId { get; init; }
        public string InvoiceNumber { get; init; } = "";
        public decimal TotalAmount { get; init; }
        public decimal PaidAmount { get; init; }
        public decimal Balance { get; init; }
        public string Status { get; init; } = "";
        public DateTime? IssuedAt { get; init; }
        public DateTime? DueDate { get; init; }
        public string? CustomerName { get; init; }
        public string? CustomerEmail { get; init; }
        public string? BookingNumber { get; init; }
        public string? TripNumber { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? LastEmailedAt { get; init; }
    }

    private sealed class InvoiceDetailRow : InvoiceRow
    {
        public string? RouteName { get; init; }
        public string? Notes { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public int EmailSentCount { get; init; }
    }
}
