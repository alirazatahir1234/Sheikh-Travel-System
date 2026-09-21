using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

public record GetPortalBookingsByPhoneQuery(string Phone, int? CustomerId = null)
    : IRequest<ApiResponse<IReadOnlyList<PortalBookingCardDto>>>;

public class GetPortalBookingsByPhoneQueryValidator : AbstractValidator<GetPortalBookingsByPhoneQuery>
{
    public GetPortalBookingsByPhoneQueryValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}

public class GetPortalBookingsByPhoneQueryHandler(ICustomerPortalRepository portalRepository)
    : IRequestHandler<GetPortalBookingsByPhoneQuery, ApiResponse<IReadOnlyList<PortalBookingCardDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalBookingCardDto>>> Handle(GetPortalBookingsByPhoneQuery request, CancellationToken cancellationToken)
    {
        var customerIds = await portalRepository.ResolvePortalCustomerIdsAsync(
            request.Phone, request.CustomerId, cancellationToken);
        if (customerIds.Count == 0)
            return ApiResponse<IReadOnlyList<PortalBookingCardDto>>.SuccessResponse([], "Bookings loaded.");

        var rows = await portalRepository.GetBookingCardsAsync(customerIds, cancellationToken);

        var list = rows.Select(r =>
        {
            var remaining = r.TotalAmount - r.PaidAmount;
            if (remaining < 0) remaining = 0;
            var payState = PortalPayStateHelper.FromAmounts(r.TotalAmount, r.PaidAmount);
            return new PortalBookingCardDto(
                r.Id,
                r.BookingNumber,
                r.RouteLabel,
                r.PickupTime,
                (BookingStatus)r.Status,
                r.TotalAmount,
                r.PaidAmount,
                remaining,
                payState);
        }).ToList();

        return ApiResponse<IReadOnlyList<PortalBookingCardDto>>.SuccessResponse(list, "Bookings loaded.");
    }
}
