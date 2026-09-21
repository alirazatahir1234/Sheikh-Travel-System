using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

public record GetPortalBookingDetailQuery(int BookingId, string Phone, int? CustomerId = null)
    : IRequest<ApiResponse<PortalBookingDetailDto>>;

public class GetPortalBookingDetailQueryValidator : AbstractValidator<GetPortalBookingDetailQuery>
{
    public GetPortalBookingDetailQueryValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}

public class GetPortalBookingDetailQueryHandler(ICustomerPortalRepository portalRepository)
    : IRequestHandler<GetPortalBookingDetailQuery, ApiResponse<PortalBookingDetailDto>>
{
    public async Task<ApiResponse<PortalBookingDetailDto>> Handle(GetPortalBookingDetailQuery request, CancellationToken cancellationToken)
    {
        var customerIds = await portalRepository.ResolvePortalCustomerIdsAsync(
            request.Phone, request.CustomerId, cancellationToken);
        if (customerIds.Count == 0)
            return ApiResponse<PortalBookingDetailDto>.FailResponse("Booking not found for this phone number.");

        var head = await portalRepository.GetBookingDetailHeadAsync(
            request.BookingId, customerIds, cancellationToken);

        if (head is null)
            return ApiResponse<PortalBookingDetailDto>.FailResponse("Booking not found for this phone number.");

        var remaining = head.TotalAmount - head.PaidAmount;
        if (remaining < 0) remaining = 0;
        var payState = PortalPayStateHelper.FromAmounts(head.TotalAmount, head.PaidAmount);

        var payments = await portalRepository.GetBookingPaymentsAsync(request.BookingId, cancellationToken);
        var seats = await portalRepository.GetBookingSeatsAsync(request.BookingId, cancellationToken);

        PortalDriverPreviewDto? driver = null;
        if (head.DriverId.HasValue && !string.IsNullOrWhiteSpace(head.DriverName))
        {
            driver = new PortalDriverPreviewDto(head.DriverName, head.DriverRating, head.DriverYears, true);
        }

        var dto = new PortalBookingDetailDto(
            head.Id,
            head.BookingNumber,
            head.RouteLabel,
            head.PickupTime,
            head.PassengerCount,
            head.VehicleName,
            (BookingStatus)head.Status,
            head.TotalAmount,
            head.PaidAmount,
            remaining,
            payState,
            payments.ToList(),
            head.PickupAddress,
            head.DropoffAddress,
            driver,
            seats.ToList());

        return ApiResponse<PortalBookingDetailDto>.SuccessResponse(dto, "Booking loaded.");
    }
}
