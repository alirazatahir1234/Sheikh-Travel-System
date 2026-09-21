using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Payments.Commands;
using SheikhTravelSystem.Application.Features.Payments.DTOs;

namespace SheikhTravelSystem.Application.Features.DriverApp.Commands;

public record DriverTripPaymentSummaryDto(
    int TripId,
    int BookingId,
    string BookingNumber,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceDue,
    bool PaymentRequired,
    string PaymentStatus);

public record GetDriverTripPaymentSummaryQuery(int Id) : IRequest<ApiResponse<DriverTripPaymentSummaryDto>>;

public record DriverCollectPaymentCommand(
    int Id,
    decimal AmountReceived,
    string PaymentMethod,
    string? ReferenceNumber,
    string? Notes) : IRequest<ApiResponse<int>>;

public class DriverCollectPaymentCommandValidator : AbstractValidator<DriverCollectPaymentCommand>
{
    public DriverCollectPaymentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.AmountReceived).GreaterThan(0);
        RuleFor(x => x.PaymentMethod).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ReferenceNumber)
            .NotEmpty()
            .When(x => !string.Equals(x.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Reference number is required for non-cash payments.");
    }
}

public class GetDriverTripPaymentSummaryQueryHandler(
    IDriverAppRepository driverAppRepository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverTripPaymentSummaryQuery, ApiResponse<DriverTripPaymentSummaryDto>>
{
    public async Task<ApiResponse<DriverTripPaymentSummaryDto>> Handle(GetDriverTripPaymentSummaryQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverTripPaymentSummaryDto>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await driverAppRepository.ResolveDriverBookingAsync(
            request.Id, driverId.Value, tenantId, cancellationToken);
        if (row is null)
            return ApiResponse<DriverTripPaymentSummaryDto>.FailResponse("Trip not found or not assigned to you.");

        var paidAmount = await driverAppRepository.GetBookingPaidAmountAsync(row.BookingId, cancellationToken);
        var balance = Math.Max(0, row.TotalAmount - paidAmount);
        var status = balance <= 0
            ? "Paid"
            : (paidAmount > 0 ? "PartiallyPaid" : "Pending");

        return ApiResponse<DriverTripPaymentSummaryDto>.SuccessResponse(new DriverTripPaymentSummaryDto(
            request.Id,
            row.BookingId,
            row.BookingNumber,
            row.TotalAmount,
            paidAmount,
            balance,
            balance > 0,
            status));
    }
}

public class DriverCollectPaymentCommandHandler(
    IDriverAppRepository driverAppRepository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    ISender sender)
    : IRequestHandler<DriverCollectPaymentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(DriverCollectPaymentCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<int>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await driverAppRepository.ResolveDriverBookingAsync(
            request.Id, driverId.Value, tenantId, cancellationToken);
        if (row is null)
            return ApiResponse<int>.FailResponse("Trip not found or not assigned to you.");

        var paidAmount = await driverAppRepository.GetBookingPaidAmountAsync(row.BookingId, cancellationToken);
        var balance = Math.Max(0, row.TotalAmount - paidAmount);
        if (balance <= 0)
            return ApiResponse<int>.FailResponse("Payment already settled for this trip.");
        if (request.AmountReceived > balance)
            return ApiResponse<int>.FailResponse($"Amount exceeds balance due ({balance:N2}).");

        var create = await sender.Send(new CreatePaymentCommand(new CreatePaymentDto(
            row.BookingId,
            request.AmountReceived,
            request.PaymentMethod,
            request.ReferenceNumber,
            $"CollectedByDriver:{driverId.Value}" + (string.IsNullOrWhiteSpace(request.Notes) ? "" : $" | {request.Notes}"),
            null
        )), cancellationToken);

        if (!create.Success) return create;
        return ApiResponse<int>.SuccessResponse(create.Data, "Payment collected successfully.");
    }
}
