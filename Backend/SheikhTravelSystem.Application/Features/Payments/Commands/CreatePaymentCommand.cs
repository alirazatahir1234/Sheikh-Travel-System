using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Payments.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Payments.Commands;

public record CreatePaymentCommand(CreatePaymentDto Payment) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Payment";
    public int? AuditEntityId => null;
}

public class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.Payment.BookingId).GreaterThan(0);
        RuleFor(x => x.Payment.Amount).GreaterThan(0);
        RuleFor(x => x.Payment.PaymentMethod).NotEmpty();
    }
}

public class CreatePaymentCommandHandler(IDbConnectionFactory dbFactory, INotificationService notificationService)
    : IRequestHandler<CreatePaymentCommand, ApiResponse<int>>
{
    /// <summary>
    /// Dapper maps to a POCO reliably. Do not use nullable ValueTuple here — it can deserialize as null
    /// even when a row exists, causing false NotFoundException on valid booking ids.
    /// </summary>
    private sealed class BookingRowForPayment
    {
        public decimal TotalAmount { get; set; }
        public int Status { get; set; }
        public string? BookingNumber { get; set; }
    }

    public async Task<ApiResponse<int>> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Payment;

        var booking = await connection.QuerySingleOrDefaultAsync<BookingRowForPayment>(
            new CommandDefinition(
                "SELECT TotalAmount, Status, BookingNumber FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
                new { Id = dto.BookingId },
                cancellationToken: cancellationToken));

        if (booking is null)
            throw new NotFoundException("Booking", dto.BookingId);

        // Calculate total already paid
        var totalPaid = await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                @"SELECT ISNULL(SUM(Amount), 0) FROM Payments
                  WHERE BookingId = @BookingId AND Status IN (@Paid, @Partial) AND IsDeleted = 0",
                new { dto.BookingId, Paid = (int)PaymentStatus.Paid, Partial = (int)PaymentStatus.PartiallyPaid },
                cancellationToken: cancellationToken));

        var remaining = booking.TotalAmount - totalPaid;

        if (dto.Amount > remaining)
            return ApiResponse<int>.FailResponse($"Payment amount exceeds remaining balance of {remaining:F2}.");

        var paymentStatus = (totalPaid + dto.Amount) >= booking.TotalAmount
            ? PaymentStatus.Paid
            : PaymentStatus.PartiallyPaid;

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"IF COL_LENGTH('Payments', 'ReceiptImageData') IS NOT NULL
                  BEGIN
                    INSERT INTO Payments (BookingId, Amount, PaymentMethod, Status, PaymentDate, TransactionReference, Notes, ReceiptImageData, CreatedAt, IsDeleted)
                    VALUES (@BookingId, @Amount, @PaymentMethod, @Status, @PaymentDate, @TransactionReference, @Notes, @ReceiptImageData, @CreatedAt, 0);
                  END
                  ELSE
                  BEGIN
                    INSERT INTO Payments (BookingId, Amount, PaymentMethod, Status, PaymentDate, TransactionReference, Notes, CreatedAt, IsDeleted)
                    VALUES (@BookingId, @Amount, @PaymentMethod, @Status, @PaymentDate, @TransactionReference, @Notes, @CreatedAt, 0);
                  END
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    dto.BookingId, dto.Amount, dto.PaymentMethod,
                    Status = (int)paymentStatus, PaymentDate = DateTime.UtcNow,
                    dto.TransactionReference, dto.Notes,
                    dto.ReceiptImageData,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        // Create notification for payment received
        var bookingNumber = booking.BookingNumber ?? $"#{dto.BookingId}";
        await notificationService.CreateForAllAsync(
            $"Payment Received: {bookingNumber}",
            $"PKR {dto.Amount:N0} received via {dto.PaymentMethod}. Status: {paymentStatus}",
            NotificationType.PaymentReceived,
            id,
            cancellationToken);

        return ApiResponse<int>.SuccessResponse(id, "Payment recorded successfully.");
    }
}
