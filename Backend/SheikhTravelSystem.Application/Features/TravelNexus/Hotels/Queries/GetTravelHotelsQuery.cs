using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Hotels.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Hotels.Queries;

public record GetTravelHotelsQuery(string? City = null, bool? IsActive = null) : IRequest<ApiResponse<IReadOnlyList<TravelHotelDto>>>;

public class GetTravelHotelsQueryHandler(ITravelHotelRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelHotelsQuery, ApiResponse<IReadOnlyList<TravelHotelDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelHotelDto>>> Handle(GetTravelHotelsQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<TravelHotelDto>>.SuccessResponse(await repository.GetListAsync(tenantContext.GetRequiredTenantId(),
            TravelValues.IsOneOf(request.City, TravelValues.HotelCities) ? request.City : null, request.IsActive, cancellationToken));
}
