using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Settlements.DTOs;

namespace SheikhTravelSystem.Application.Features.Settlements.Queries;

public record GetSettlementsQuery(
    int Page = 1,
    int PageSize = 20,
    int? DriverId = null,
    string? Status = null
) : IRequest<ApiResponse<PagedResult<SettlementDto>>>;

public class GetSettlementsQueryHandler(
    IDriverSettlementRepository settlementRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetSettlementsQuery, ApiResponse<PagedResult<SettlementDto>>>
{
    public async Task<ApiResponse<PagedResult<SettlementDto>>> Handle(
        GetSettlementsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var result = await settlementRepository.GetPagedAsync(
            tenantId, request.Page, request.PageSize, request.DriverId, request.Status, cancellationToken);
        return ApiResponse<PagedResult<SettlementDto>>.SuccessResponse(result);
    }
}

public record GetSettlementByIdQuery(int Id) : IRequest<ApiResponse<SettlementDetailDto>>;

public class GetSettlementByIdQueryHandler(
    IDriverSettlementRepository settlementRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetSettlementByIdQuery, ApiResponse<SettlementDetailDto>>
{
    public async Task<ApiResponse<SettlementDetailDto>> Handle(
        GetSettlementByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var settlement = await settlementRepository.GetByIdAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("Settlement", request.Id);
        return ApiResponse<SettlementDetailDto>.SuccessResponse(settlement);
    }
}

public record GetMyDriverSettlementsQuery(int Page = 1, int PageSize = 50)
    : IRequest<ApiResponse<PagedResult<SettlementDto>>>;

public class GetMyDriverSettlementsQueryHandler(
    IDriverSettlementRepository settlementRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMyDriverSettlementsQuery, ApiResponse<PagedResult<SettlementDto>>>
{
    public async Task<ApiResponse<PagedResult<SettlementDto>>> Handle(
        GetMyDriverSettlementsQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<PagedResult<SettlementDto>>.FailResponse("Driver context required.");

        var result = await settlementRepository.GetPagedAsync(
            tenantContext.GetRequiredTenantId(),
            request.Page,
            request.PageSize,
            driverId.Value,
            null,
            cancellationToken);
        return ApiResponse<PagedResult<SettlementDto>>.SuccessResponse(result);
    }
}
