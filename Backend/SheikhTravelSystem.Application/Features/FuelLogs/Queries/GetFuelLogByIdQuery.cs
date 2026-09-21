using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;

namespace SheikhTravelSystem.Application.Features.FuelLogs.Queries;

public record GetFuelLogByIdQuery(int Id) : IRequest<ApiResponse<FuelLogDto>>;

public class GetFuelLogByIdQueryHandler(IFuelLogRepository fuelLogRepository)
    : IRequestHandler<GetFuelLogByIdQuery, ApiResponse<FuelLogDto>>
{
    public async Task<ApiResponse<FuelLogDto>> Handle(GetFuelLogByIdQuery request, CancellationToken cancellationToken)
    {
        var fuelLog = await fuelLogRepository.GetByIdAsync(request.Id, cancellationToken);

        if (fuelLog == null)
            throw new NotFoundException("FuelLog", request.Id);

        return ApiResponse<FuelLogDto>.SuccessResponse(fuelLog);
    }
}
