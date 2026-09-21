using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;

namespace SheikhTravelSystem.Application.Features.FuelLogs.Queries;

public record GetFuelLogsQuery(int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PagedResult<FuelLogDto>>>;

public class GetFuelLogsQueryHandler(IFuelLogRepository fuelLogRepository)
    : IRequestHandler<GetFuelLogsQuery, ApiResponse<PagedResult<FuelLogDto>>>
{
    public async Task<ApiResponse<PagedResult<FuelLogDto>>> Handle(GetFuelLogsQuery request, CancellationToken cancellationToken)
    {
        var result = await fuelLogRepository.GetPagedAsync(request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PagedResult<FuelLogDto>>.SuccessResponse(result);
    }
}
