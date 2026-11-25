using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Queries;

/// <summary>Platform reference list of countries (not tenant data).</summary>
public record GetTravelCountriesQuery : IRequest<ApiResponse<IReadOnlyList<TravelCountryDto>>>;

public class GetTravelCountriesQueryHandler(ITravelConfigRepository repository)
    : IRequestHandler<GetTravelCountriesQuery, ApiResponse<IReadOnlyList<TravelCountryDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelCountryDto>>> Handle(GetTravelCountriesQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<TravelCountryDto>>.SuccessResponse(await repository.GetCountriesAsync(cancellationToken));
}
