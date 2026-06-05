using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Payments;

internal sealed class PaymentGatewayPaymentRecorder(IDbConnectionFactory dbFactory)
{
    public async Task<bool> RecordAsync(
        int bookingId,
        decimal amount,
        string paymentMethod,
        string transactionReference,
        string notes,
        CancellationToken cancellationToken)
    {
        if (bookingId <= 0 || amount <= 0 || string.IsNullOrWhiteSpace(transactionReference))
        {
            return false;
        }

        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Payments
                    WHERE TransactionReference = @TransactionReference AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { TransactionReference = transactionReference },
                cancellationToken: cancellationToken));

        if (exists)
        {
            return true;
        }

        var booking = await connection.QuerySingleOrDefaultAsync<BookingPaymentState>(
            new CommandDefinition(
                @"SELECT b.TotalAmount,
                         ISNULL(SUM(CASE WHEN p.Status IN (@Paid, @Partial) THEN p.Amount ELSE 0 END), 0) AS PaidAmount
                  FROM Bookings b
                  LEFT JOIN Payments p ON p.BookingId = b.Id AND p.IsDeleted = 0
                  WHERE b.Id = @BookingId AND b.IsDeleted = 0
                  GROUP BY b.TotalAmount",
                new
                {
                    BookingId = bookingId,
                    Paid = (int)PaymentStatus.Paid,
                    Partial = (int)PaymentStatus.PartiallyPaid
                },
                cancellationToken: cancellationToken));

        if (booking is null)
        {
            return false;
        }

        var remaining = booking.TotalAmount - booking.PaidAmount;
        if (amount > remaining)
        {
            return false;
        }

        var status = booking.PaidAmount + amount >= booking.TotalAmount
            ? PaymentStatus.Paid
            : PaymentStatus.PartiallyPaid;

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO Payments (BookingId, Amount, PaymentMethod, Status, PaymentDate, TransactionReference, Notes, ReceiptImageData, CreatedAt, IsDeleted)
                  VALUES (@BookingId, @Amount, @PaymentMethod, @Status, @PaymentDate, @TransactionReference, @Notes, NULL, @CreatedAt, 0)",
                new
                {
                    BookingId = bookingId,
                    Amount = amount,
                    PaymentMethod = paymentMethod,
                    Status = (int)status,
                    PaymentDate = DateTime.UtcNow,
                    TransactionReference = transactionReference,
                    Notes = notes,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        return true;
    }

    private sealed class BookingPaymentState
    {
        public decimal TotalAmount { get; init; }
        public decimal PaidAmount { get; init; }
    }
}
