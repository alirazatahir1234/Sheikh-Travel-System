using System.Text.Json;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
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
    IAuditEngine auditEngine,
    ITenantContext tenantContext)
    : IRequestHandler<GetAdminCompaniesQuery, ApiResponse<IReadOnlyList<AdminCompanyOptionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AdminCompanyOptionDto>>> Handle(
        GetAdminCompaniesQuery request,
        CancellationToken cancellationToken)
    {
        // Gate: controller [RequirePermission(Platform.Tenants.View)] (SG-05 C1).
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

        // Aggregate cross-tenant read: one row, TargetTenantId=NULL, payload lists count + ids.
        var tenantIds = options.Select(o => o.Id).ToList();
        var payload = JsonSerializer.Serialize(new
        {
            mode = "all_companies",
            count = tenantIds.Count,
            tenantIds
        });

        await auditEngine.RecordAsync(new AuditEventWrite(
            EventKey: AuditEventKeys.CrossTenantRead,
            EntityType: "AdminCompanies",
            Action: "ListAllCompanies",
            NewValues: payload,
            Success: true,
            Message: $"Listed {tenantIds.Count} companies (cross-tenant)",
            TenantId: PlatformTenant.Id,
            TargetTenantId: null,
            ActorTenantId: tenantContext.TenantId,
            // Fail-loud (SG-52): throws if audit INSERT fails, but not same-TX as the read.
            FailPolicy: AuditFailPolicy.FailLoud), cancellationToken);

        return ApiResponse<IReadOnlyList<AdminCompanyOptionDto>>.SuccessResponse(options);
    }
}

public record GetAdminVehiclesQuery(int? CompanyId, int Page = 1, int PageSize = 50)
    : IRequest<ApiResponse<AdminVehiclesPageDto>>;

public class GetAdminVehiclesQueryHandler(
    IVehicleRepository vehicleRepository,
    IAuditEngine auditEngine,
    ITenantContext tenantContext)
    : IRequestHandler<GetAdminVehiclesQuery, ApiResponse<AdminVehiclesPageDto>>
{
    public async Task<ApiResponse<AdminVehiclesPageDto>> Handle(
        GetAdminVehiclesQuery request,
        CancellationToken cancellationToken)
    {
        // Gate: controller [RequirePermission(Platform.Tenants.DataRead)] (SG-05 C1).
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 50 : Math.Min(request.PageSize, 200);
        var companyId = request.CompanyId is int cid && cid > 0 ? cid : (int?)null;

        var (items, total) = await vehicleRepository.GetCrossTenantPagedAsync(
            companyId, page, pageSize, cancellationToken);

        if (companyId is int subjectTenantId)
        {
            // Single-target: TenantId = subject company so their SearchAsync finds it.
            await auditEngine.RecordAsync(new AuditEventWrite(
                EventKey: AuditEventKeys.CrossTenantRead,
                EntityType: "AdminVehicles",
                EntityId: subjectTenantId,
                Action: "ListByCompany",
                NewValues: JsonSerializer.Serialize(new
                {
                    mode = "single_tenant",
                    tenantId = subjectTenantId,
                    page,
                    pageSize,
                    total
                }),
                Success: true,
                Message: $"Listed vehicles for company {subjectTenantId}",
                TenantId: subjectTenantId,
                TargetTenantId: subjectTenantId,
                ActorTenantId: tenantContext.TenantId,
                // Fail-loud (SG-52): not same-TX; read already executed.
                FailPolicy: AuditFailPolicy.FailLoud), cancellationToken);
        }
        else
        {
            var distinctTenantIds = items.Select(i => i.CompanyId).Distinct().OrderBy(x => x).ToList();
            await auditEngine.RecordAsync(new AuditEventWrite(
                EventKey: AuditEventKeys.CrossTenantRead,
                EntityType: "AdminVehicles",
                Action: "ListAllCompanies",
                NewValues: JsonSerializer.Serialize(new
                {
                    mode = "all_companies",
                    count = distinctTenantIds.Count,
                    tenantIds = distinctTenantIds,
                    page,
                    pageSize,
                    total
                }),
                Success: true,
                Message: $"Listed vehicles across {distinctTenantIds.Count} companies on page (total {total})",
                TenantId: PlatformTenant.Id,
                TargetTenantId: null,
                ActorTenantId: tenantContext.TenantId,
                // Fail-loud (SG-52): not same-TX; read already executed.
                FailPolicy: AuditFailPolicy.FailLoud), cancellationToken);
        }

        return ApiResponse<AdminVehiclesPageDto>.SuccessResponse(
            new AdminVehiclesPageDto(items, total, page, pageSize, companyId));
    }
}
