using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

public record GetPortalRoutesQuery : IRequest<ApiResponse<IReadOnlyList<PortalRouteDto>>>;

public class GetPortalRoutesQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPortalRoutesQuery, ApiResponse<IReadOnlyList<PortalRouteDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalRouteDto>>> Handle(GetPortalRoutesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PortalRouteDto>(
            new CommandDefinition(
                @"SELECT Id,
                         (COALESCE(Source, N'') + N' → ' + COALESCE(Destination, N'')) AS Label,
                         Distance AS DistanceKm,
                         BasePrice,
                         COALESCE(Source, N'') AS Source,
                         COALESCE(Destination, N'') AS Destination,
                         Name
                  FROM Routes
                  WHERE IsDeleted = 0 AND IsActive = 1
                  ORDER BY Source, Destination",
                cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<PortalRouteDto>>.SuccessResponse(rows.AsList(), "Routes loaded.");
    }
}
