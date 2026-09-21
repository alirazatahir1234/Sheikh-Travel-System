using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Trips.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Trips.Queries;

public record GetTripDashboardQuery : IRequest<ApiResponse<TripDashboardDto>>;

public class GetTripDashboardQueryHandler(ITripRepository tripRepository)
    : IRequestHandler<GetTripDashboardQuery, ApiResponse<TripDashboardDto>>
{
    public async Task<ApiResponse<TripDashboardDto>> Handle(GetTripDashboardQuery request, CancellationToken cancellationToken)
    {
        var dto = await tripRepository.GetDashboardAsync(cancellationToken);
        return ApiResponse<TripDashboardDto>.SuccessResponse(dto);
    }
}

public record GetTripsQuery(
    int Page = 1,
    int PageSize = 20,
    TripStatus? Status = null,
    int? DriverId = null,
    int? VehicleId = null,
    int? RouteId = null,
    int? CustomerId = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? Search = null,
    bool TodayOnly = false,
    bool TomorrowOnly = false,
    bool UpcomingOnly = false
) : IRequest<ApiResponse<PagedResult<TripListItemDto>>>;

public class GetTripsQueryHandler(ITripRepository tripRepository)
    : IRequestHandler<GetTripsQuery, ApiResponse<PagedResult<TripListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<TripListItemDto>>> Handle(GetTripsQuery request, CancellationToken cancellationToken)
    {
        var result = await tripRepository.GetPagedAsync(
            request.Page, request.PageSize, request.Status, request.DriverId, request.VehicleId,
            request.RouteId, request.CustomerId, request.DateFrom, request.DateTo, request.Search,
            request.TodayOnly, request.TomorrowOnly, request.UpcomingOnly, cancellationToken);
        return ApiResponse<PagedResult<TripListItemDto>>.SuccessResponse(result);
    }
}

public record GetTripByIdQuery(int Id) : IRequest<ApiResponse<TripDetailDto>>;

public class GetTripByIdQueryHandler(ITripRepository tripRepository, IFileStorageService fileStorage)
    : IRequestHandler<GetTripByIdQuery, ApiResponse<TripDetailDto>>
{
    public async Task<ApiResponse<TripDetailDto>> Handle(GetTripByIdQuery request, CancellationToken cancellationToken)
    {
        var load = await tripRepository.GetDetailAsync(request.Id, cancellationToken);
        var docs = load.Documents.Select(d => new TripDocumentDto(
            d.Id, d.DocumentType, d.FileName, fileStorage.ResolveReadUrl(d.StorageKey), d.UploadedBy, d.CreatedAt)).ToList();
        var detail = load.Detail with { Documents = docs };
        return ApiResponse<TripDetailDto>.SuccessResponse(detail);
    }
}
