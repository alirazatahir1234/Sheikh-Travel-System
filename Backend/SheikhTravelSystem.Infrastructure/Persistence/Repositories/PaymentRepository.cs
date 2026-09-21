using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Infrastructure.Persistence;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Payments.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(IDbConnectionFactory dbFactory) : IPaymentRepository
{
    public async Task<PagedResult<PaymentDto>> GetPagedAsync(
        int page,
        int pageSize,
        int tenantId,
        int? bookingId,
        PaymentStatus? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        DataScopeResult? scope,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        var clauses = new List<string> { "p.IsDeleted = 0", "p.TenantId = @TenantId" };
        if (bookingId.HasValue) clauses.Add("p.BookingId = @BookingId");
        if (status.HasValue) clauses.Add("p.Status = @Status");
        if (dateFrom.HasValue) clauses.Add("p.PaymentDate >= @DateFrom");
        if (dateTo.HasValue) clauses.Add("p.PaymentDate < @DateTo");

        var parameters = new DynamicParameters(new
        {
            BookingId = bookingId,
            Status = status.HasValue ? (int?)status.Value : null,
            DateFrom = dateFrom,
            DateTo = dateTo.HasValue ? (DateTime?)dateTo.Value.Date.AddDays(1) : null,
            Offset = offset,
            PageSize = pageSize,
            TenantId = tenantId
        });

        if (scope is not null)
            DataScopeSqlBuilder.ApplyLinkedFleetScope(parameters, scope, clauses, "v", "d");

        var where = "WHERE " + string.Join(" AND ", clauses);

        var sql = $@"SELECT p.Id, p.BookingId, p.Amount, p.PaymentMethod, p.Status, p.PaymentDate,
                     p.TransactionReference, p.Notes, p.CreatedAt
                     FROM Payments p
                     LEFT JOIN Bookings b ON b.Id = p.BookingId
                     LEFT JOIN Vehicles v ON v.Id = b.VehicleId
                     LEFT JOIN Drivers d ON d.Id = b.DriverId
                     {where}
                     ORDER BY p.CreatedAt DESC
                     OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        var countSql = $@"SELECT COUNT(*) FROM Payments p
                     LEFT JOIN Bookings b ON b.Id = p.BookingId
                     LEFT JOIN Vehicles v ON v.Id = b.VehicleId
                     LEFT JOIN Drivers d ON d.Id = b.DriverId
                     {where}";

        var payments = await connection.QueryAsync<PaymentDto>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

        return new PagedResult<PaymentDto>
        {
            Items = payments.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PaymentDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<PaymentDetailDto>(
            new CommandDefinition(
                @"SELECT p.Id, p.BookingId, b.BookingNumber, c.FullName AS CustomerName,
                  r.Source + ' -> ' + r.Destination AS RouteName,
                  p.Amount, p.PaymentMethod, p.Status, p.PaymentDate,
                  p.TransactionReference, p.Notes, p.CreatedAt, b.TotalAmount AS TotalBookingAmount,
                  p.ReceiptImageData
                  FROM Payments p
                  INNER JOIN Bookings b ON p.BookingId = b.Id
                  LEFT JOIN Customers c ON b.CustomerId = c.Id
                  LEFT JOIN Routes r ON b.RouteId = r.Id
                  WHERE p.Id = @Id AND p.IsDeleted = 0",
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<List<PaymentDto>> GetByBookingIdAsync(
        int bookingId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var payments = await connection.QueryAsync<PaymentDto>(
            new CommandDefinition(
                @"SELECT Id, BookingId, Amount, PaymentMethod, Status, PaymentDate,
                  TransactionReference, Notes, CreatedAt
                  FROM Payments 
                  WHERE BookingId = @BookingId AND IsDeleted = 0
                  ORDER BY PaymentDate DESC",
                new { BookingId = bookingId },
                cancellationToken: cancellationToken));

        return payments.ToList();
    }

    public async Task<PaymentReportDto> GetReportAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var totalReceived = await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                @"SELECT ISNULL(SUM(Amount), 0) FROM Payments
                  WHERE Status = @PaidStatus AND PaymentDate BETWEEN @FromDate AND @ToDate AND IsDeleted = 0",
                new { PaidStatus = (int)PaymentStatus.Paid, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken));

        var totalPending = await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                @"SELECT ISNULL(SUM(Amount), 0) FROM Payments
                  WHERE Status IN (@Pending, @Partial) AND PaymentDate BETWEEN @FromDate AND @ToDate AND IsDeleted = 0",
                new { Pending = (int)PaymentStatus.Pending, Partial = (int)PaymentStatus.PartiallyPaid, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken));

        var totalTransactions = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Payments WHERE PaymentDate BETWEEN @FromDate AND @ToDate AND IsDeleted = 0",
                new { FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken));

        var recentPayments = await connection.QueryAsync<PaymentDto>(
            new CommandDefinition(
                @"SELECT TOP 10 Id, BookingId, Amount, PaymentMethod, Status, PaymentDate,
                  TransactionReference, Notes, CreatedAt
                  FROM Payments WHERE PaymentDate BETWEEN @FromDate AND @ToDate AND IsDeleted = 0
                  ORDER BY CreatedAt DESC",
                new { FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken));

        return new PaymentReportDto(totalReceived, totalPending, totalTransactions, recentPayments.ToList());
    }

    public async Task<PaymentBookingInfo?> GetBookingForPaymentAsync(
        int bookingId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // Dapper maps to a POCO reliably. Do not use nullable ValueTuple here — it can deserialize as null
        // even when a row exists, causing false NotFoundException on valid booking ids.
        var booking = await connection.QuerySingleOrDefaultAsync<BookingRowForPayment>(
            new CommandDefinition(
                "SELECT TotalAmount, Status, BookingNumber FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
                new { Id = bookingId },
                cancellationToken: cancellationToken));

        return booking is null
            ? null
            : new PaymentBookingInfo(booking.TotalAmount, booking.Status, booking.BookingNumber);
    }

    public async Task<decimal> GetTotalPaidForBookingAsync(
        int bookingId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                @"SELECT ISNULL(SUM(Amount), 0) FROM Payments
                  WHERE BookingId = @BookingId AND Status IN (@Paid, @Partial) AND IsDeleted = 0",
                new { BookingId = bookingId, Paid = (int)PaymentStatus.Paid, Partial = (int)PaymentStatus.PartiallyPaid },
                cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(
        CreatePaymentDto dto,
        PaymentStatus status,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO Payments (BookingId, Amount, PaymentMethod, Status, PaymentDate, TransactionReference, Notes, ReceiptImageData, CreatedAt, IsDeleted)
                  VALUES (@BookingId, @Amount, @PaymentMethod, @Status, @PaymentDate, @TransactionReference, @Notes, @ReceiptImageData, @CreatedAt, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    dto.BookingId, dto.Amount, dto.PaymentMethod,
                    Status = (int)status, PaymentDate = DateTime.UtcNow,
                    dto.TransactionReference, dto.Notes,
                    dto.ReceiptImageData,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(
        int id,
        PaymentStatus status,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Payments SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { Status = (int)status, UpdatedAt = DateTime.UtcNow, Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Payments WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    private sealed class BookingRowForPayment
    {
        public decimal TotalAmount { get; set; }
        public int Status { get; set; }
        public string? BookingNumber { get; set; }
    }
}
