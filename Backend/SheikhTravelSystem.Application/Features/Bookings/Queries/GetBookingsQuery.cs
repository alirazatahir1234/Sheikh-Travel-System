using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Bookings.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Bookings.Queries;

public record GetBookingsQuery(
    int Page = 1,
    int PageSize = 20,
    BookingStatus? Status = null,
    string? Search = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    decimal? AmountMin = null,
    decimal? AmountMax = null
) : IRequest<ApiResponse<PagedResult<BookingDto>>>;

public class GetBookingsQueryHandler(IBookingRepository bookingRepository)
    : IRequestHandler<GetBookingsQuery, ApiResponse<PagedResult<BookingDto>>>
{
    public async Task<ApiResponse<PagedResult<BookingDto>>> Handle(GetBookingsQuery request, CancellationToken cancellationToken)
    {
        var result = await bookingRepository.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.Status,
            request.Search,
            request.DateFrom,
            request.DateTo,
            request.AmountMin,
            request.AmountMax,
            cancellationToken);

        return ApiResponse<PagedResult<BookingDto>>.SuccessResponse(result);
    }
}

public record GetBookingByIdQuery(int Id) : IRequest<ApiResponse<BookingDto>>;

public class GetBookingByIdQueryHandler(IBookingRepository bookingRepository)
    : IRequestHandler<GetBookingByIdQuery, ApiResponse<BookingDto>>
{
    public async Task<ApiResponse<BookingDto>> Handle(GetBookingByIdQuery request, CancellationToken cancellationToken)
    {
        var booking = await bookingRepository.GetByIdAsync(request.Id, cancellationToken);
        return ApiResponse<BookingDto>.SuccessResponse(booking);
    }
}
