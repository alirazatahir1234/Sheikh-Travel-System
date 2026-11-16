using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Platform;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;

namespace SheikhTravelSystem.Application.Features.Admin;

public record AdminCompanyOptionDto(int Id, string Name, string? Slug, int ActiveVehicleCount, bool IsActive);

public record AdminVehiclesPageDto(
    IReadOnlyList<AdminCrossTenantVehicleDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int? CompanyId);

public record GetAdminCompaniesQuery : IRequest<ApiResponse<IReadOnlyList<AdminCompanyOptionDto>>>;

public class GetAdminCompaniesQueryHandler(
    IPlatformRepository platformRepository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetAdminCompaniesQuery, ApiResponse<IReadOnlyList<AdminCompanyOptionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AdminCompanyOptionDto>>> Handle(
        GetAdminCompaniesQuery request,
        CancellationToken cancellationToken)
    {
        EnsurePlatformCrossTenantReader(currentUser, tenantContext);

        var tenants = await platformRepository.GetAllTenantsAsync(cancellationToken);
        var options = tenants
            .OrderBy(t => t.Name)
            .Select(t => new AdminCompanyOptionDto(
                t.Id,
                t.Name,
                t.Slug,
                t.ActiveVehicleCount,
                t.IsActive))
            .ToList();

        return ApiResponse<IReadOnlyList<AdminCompanyOptionDto>>.SuccessResponse(options);
    }

    internal static void EnsurePlatformCrossTenantReader(
        ICurrentUserService currentUser,
        ITenantContext tenantContext)
    {
        if (!currentUser.IsPlatformSuperAdmin && !tenantContext.IsCrossTenantBypass)
            throw new ForbiddenException("Only platform Super Admin can access cross-company vehicle data.");
    }
}

public record GetAdminVehiclesQuery(int? CompanyId, int Page = 1, int PageSize = 50)
    : IRequest<ApiResponse<AdminVehiclesPageDto>>;

public class GetAdminVehiclesQueryHandler(
    IVehicleRepository vehicleRepository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IAuditService auditService)
    : IRequestHandler<GetAdminVehiclesQuery, ApiResponse<AdminVehiclesPageDto>>
{
    public async Task<ApiResponse<AdminVehiclesPageDto>> Handle(
        GetAdminVehiclesQuery request,
        CancellationToken cancellationToken)
    {
        GetAdminCompaniesQueryHandler.EnsurePlatformCrossTenantReader(currentUser, tenantContext);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 50 : Math.Min(request.PageSize, 200);
        var companyId = request.CompanyId is int cid && cid > 0 ? cid : (int?)null;

        var (items, total) = await vehicleRepository.GetCrossTenantPagedAsync(
            companyId, page, pageSize, cancellationToken);

        await auditService.LogAsync(
            action: companyId is null ? "ListAllCompanies" : "ListByCompany",
            entityName: "AdminVehicles",
            entityId: companyId,
            cancellationToken);

        return ApiResponse<AdminVehiclesPageDto>.SuccessResponse(
            new AdminVehiclesPageDto(items, total, page, pageSize, companyId));
    }
}
