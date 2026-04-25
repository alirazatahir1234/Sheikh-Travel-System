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

public class CreatePaymentCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CreatePaymentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Payment;

        var booking = await connection.QuerySingleOrDefaultAsync<(decimal TotalAmount, int Status)?>
            (new CommandDefinition(
                "SELECT TotalAmount, Status FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
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

        var remaining = booking.Value.TotalAmount - totalPaid;

        if (dto.Amount > remaining)
            return ApiResponse<int>.FailResponse($"Payment amount exceeds remaining balance of {remaining:F2}.");

        var paymentStatus = (totalPaid + dto.Amount) >= booking.Value.TotalAmount
            ? PaymentStatus.Paid
            : PaymentStatus.PartiallyPaid;

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO Payments (BookingId, Amount, PaymentMethod, Status, PaymentDate, TransactionReference, Notes, CreatedAt, IsDeleted)
                  VALUES (@BookingId, @Amount, @PaymentMethod, @Status, @PaymentDate, @TransactionReference, @Notes, @CreatedAt, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    dto.BookingId, dto.Amount, dto.PaymentMethod,
                    Status = (int)paymentStatus, PaymentDate = DateTime.UtcNow,
                    dto.TransactionReference, dto.Notes, CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Payment recorded successfully.");
    }
}
