using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Trips.DTOs;

namespace SheikhTravelSystem.Application.Features.Trips.Queries;

public record GetTripInsightsQuery(int Id) : IRequest<ApiResponse<TripInsightsDto>>;

public class GetTripInsightsQueryHandler(ITripEnhancementsRepository repo)
    : IRequestHandler<GetTripInsightsQuery, ApiResponse<TripInsightsDto>>
{
    public async Task<ApiResponse<TripInsightsDto>> Handle(GetTripInsightsQuery request, CancellationToken cancellationToken)
    {
        var dto = await repo.GetInsightsAsync(request.Id, cancellationToken);
        return ApiResponse<TripInsightsDto>.SuccessResponse(dto);
    }
}

public record EstimateTripCostQuery(TripCostEstimateRequestDto Request) : IRequest<ApiResponse<TripCostEstimateDto>>;

public class EstimateTripCostQueryHandler(ITripEnhancementsRepository repo)
    : IRequestHandler<EstimateTripCostQuery, ApiResponse<TripCostEstimateDto>>
{
    public async Task<ApiResponse<TripCostEstimateDto>> Handle(
        EstimateTripCostQuery request, CancellationToken cancellationToken)
    {
        var dto = await repo.EstimateCostAsync(request.Request, cancellationToken);
        return ApiResponse<TripCostEstimateDto>.SuccessResponse(dto);
    }
}

public record GetTripTemplatesQuery : IRequest<ApiResponse<IReadOnlyList<TripTemplateDto>>>;

public class GetTripTemplatesQueryHandler(ITripEnhancementsRepository repo)
    : IRequestHandler<GetTripTemplatesQuery, ApiResponse<IReadOnlyList<TripTemplateDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TripTemplateDto>>> Handle(
        GetTripTemplatesQuery request, CancellationToken cancellationToken)
    {
        var items = await repo.ListTemplatesAsync(cancellationToken);
        return ApiResponse<IReadOnlyList<TripTemplateDto>>.SuccessResponse(items);
    }
}

public record GetTripAssignmentConflictQuery(int TripId, int? DriverId, int? VehicleId)
    : IRequest<ApiResponse<TripAssignmentConflictDto>>;

public class GetTripAssignmentConflictQueryHandler(ITripEnhancementsRepository repo)
    : IRequestHandler<GetTripAssignmentConflictQuery, ApiResponse<TripAssignmentConflictDto>>
{
    public async Task<ApiResponse<TripAssignmentConflictDto>> Handle(
        GetTripAssignmentConflictQuery request, CancellationToken cancellationToken)
    {
        var dto = await repo.GetAssignmentConflictAsync(
            request.TripId, request.DriverId, request.VehicleId, cancellationToken)
            ?? new TripAssignmentConflictDto(false, null, null, null);
        return ApiResponse<TripAssignmentConflictDto>.SuccessResponse(dto);
    }
}
