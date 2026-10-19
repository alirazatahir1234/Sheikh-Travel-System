using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Invoices.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class CustomerInvoiceRepository(IDbConnectionFactory dbFactory) : ICustomerInvoiceRepository
{
    public async Task<PagedResult<InvoiceDto>> GetPagedAsync(
        int tenantId,
        int page,
        int pageSize,
        string? status,
        int? bookingId,
        int? customerId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        var clauses = new List<string> { "i.IsDeleted = 0", "i.TenantId = @TenantId" };
        if (!string.IsNullOrWhiteSpace(status)) clauses.Add("i.Status = @Status");
        if (bookingId.HasValue) clauses.Add("i.BookingId = @BookingId");
        if (customerId.HasValue) clauses.Add("i.CustomerId = @CustomerId");

        var where = "WHERE " + string.Join(" AND ", clauses);
        var parameters = new
        {
            TenantId = tenantId,
            Status = status,
            BookingId = bookingId,
            CustomerId = customerId,
            Offset = offset,
            PageSize = pageSize
        };

        var items = (await connection.QueryAsync<InvoiceDto>(new CommandDefinition($@"
            SELECT i.Id, i.BookingId, i.CustomerId, i.InvoiceNumber, i.TotalAmount, i.PaidAmount, i.Balance,
                   i.Status, i.IssuedAt, i.CustomerName, i.BookingNumber, i.CreatedAt
            FROM CustomerInvoices i
            {where}
            ORDER BY i.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            parameters, cancellationToken: cancellationToken))).ToList();

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM CustomerInvoices i {where}",
            parameters, cancellationToken: cancellationToken));

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
        return await connection.QuerySingleOrDefaultAsync<InvoiceDetailDto>(new CommandDefinition("""
            SELECT Id, BookingId, CustomerId, InvoiceNumber, TotalAmount, PaidAmount, Balance,
                   Status, IssuedAt, CustomerName, BookingNumber, RouteName, Notes, CreatedAt, UpdatedAt
            FROM CustomerInvoices
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<InvoiceBookingSnapshot?> GetBookingSnapshotAsync(
        int bookingId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<InvoiceBookingSnapshot>(new CommandDefinition("""
            SELECT b.Id AS BookingId, b.CustomerId, b.TotalAmount, b.BookingNumber, b.Status,
                   c.FullName AS CustomerName,
                   CASE WHEN r.Id IS NULL THEN NULL ELSE r.Source + N' -> ' + r.Destination END AS RouteName
            FROM Bookings b
            LEFT JOIN Customers c ON c.Id = b.CustomerId AND c.TenantId = @TenantId
            LEFT JOIN Routes r ON r.Id = b.RouteId AND r.TenantId = @TenantId
            WHERE b.Id = @BookingId AND b.TenantId = @TenantId AND b.IsDeleted = 0
            """, new { BookingId = bookingId, TenantId = tenantId }, cancellationToken: cancellationToken));
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

    public async Task<string> AllocateInvoiceNumberAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var prefix = await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            SELECT TOP 1 Value FROM PlatformSettings
            WHERE TenantId = @TenantId AND Category = N'Numbering' AND [Key] = N'InvoicePrefix'
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(prefix))
            prefix = "INV-";

        // Prefer PlatformSettings counter when present; otherwise MAX+1 from existing invoices.
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
        InvoiceBookingSnapshot booking,
        string invoiceNumber,
        decimal paidAmount,
        decimal balance,
        InvoiceStatus status,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO CustomerInvoices
                (TenantId, BookingId, CustomerId, InvoiceNumber, TotalAmount, PaidAmount, Balance, Status,
                 IssuedAt, CustomerName, BookingNumber, RouteName, CreatedAt, CreatedBy, IsDeleted)
            VALUES
                (@TenantId, @BookingId, @CustomerId, @InvoiceNumber, @TotalAmount, @PaidAmount, @Balance, @Status,
                 @IssuedAt, @CustomerName, @BookingNumber, @RouteName, SYSUTCDATETIME(), @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
            {
                TenantId = tenantId,
                booking.BookingId,
                booking.CustomerId,
                InvoiceNumber = invoiceNumber,
                booking.TotalAmount,
                PaidAmount = paidAmount,
                Balance = balance,
                Status = status.ToString(),
                IssuedAt = DateTime.UtcNow,
                booking.CustomerName,
                booking.BookingNumber,
                booking.RouteName,
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
}
