using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Payments.DTOs;
using SheikhTravelSystem.Application.Features.Notifications;
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

public class CreatePaymentCommandHandler(IPaymentRepository paymentRepository, INotificationDecisionEngine decisionEngine)
    : IRequestHandler<CreatePaymentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Payment;

        var booking = await paymentRepository.GetBookingForPaymentAsync(dto.BookingId, cancellationToken);
        if (booking is null)
            throw new NotFoundException("Booking", dto.BookingId);

        var totalPaid = await paymentRepository.GetTotalPaidForBookingAsync(dto.BookingId, cancellationToken);
        var remaining = booking.TotalAmount - totalPaid;

        if (dto.Amount > remaining)
            return ApiResponse<int>.FailResponse($"Payment amount exceeds remaining balance of {remaining:F2}.");

        var paymentStatus = (totalPaid + dto.Amount) >= booking.TotalAmount
            ? PaymentStatus.Paid
            : PaymentStatus.PartiallyPaid;

        var id = await paymentRepository.CreateAsync(dto, paymentStatus, cancellationToken);

        var bookingNumber = booking.BookingNumber ?? $"#{dto.BookingId}";
        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            "payment_received",
            $"Payment Received: {bookingNumber}",
            $"PKR {dto.Amount:N0} received via {dto.PaymentMethod}. Status: {paymentStatus}",
            NotificationType.PaymentReceived,
            ReferenceId: id,
            SuggestedPriority: 2,
            RequestedChannels:
            [
                NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
            ]), cancellationToken);

        return ApiResponse<int>.SuccessResponse(id, "Payment recorded successfully.");
    }
}
