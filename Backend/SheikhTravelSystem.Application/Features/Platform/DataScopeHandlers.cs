using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Platform;

public record CompanyDataScopeDto(
    string Mode,
    bool IsCompanyWide,
    IReadOnlyList<int> BranchIds,
    IReadOnlyList<int> DepartmentIds,
    IReadOnlyList<string> BranchLabels,
    IReadOnlyList<string> DepartmentLabels,
    string Source,
    int? HomeBranchId = null,
    int? HomeDepartmentId = null);

public record GetMyDataScopeQuery : IRequest<ApiResponse<CompanyDataScopeDto>>;

public record GetUserDataScopeQuery(int UserId) : IRequest<ApiResponse<CompanyDataScopeDto>>;

public static class DataScopeDtoMapper
{
    public static async Task<CompanyDataScopeDto> ToDtoAsync(
        IPlatformRepository platformRepository,
        DataScopeResult scope,
        CancellationToken cancellationToken)
    {
        var branchLabels = scope.BranchIds.Count > 0
            ? await platformRepository.GetBranchLabelsAsync(scope.TenantId, scope.BranchIds, cancellationToken)
            : Array.Empty<string>();

        var departmentLabels = scope.DepartmentIds.Count > 0
            ? await platformRepository.GetDepartmentLabelsAsync(scope.TenantId, scope.DepartmentIds, cancellationToken)
            : Array.Empty<string>();

        return new CompanyDataScopeDto(
            Mode: scope.Mode.ToString(),
            IsCompanyWide: scope.IsCompanyWide,
            BranchIds: scope.BranchIds,
            DepartmentIds: scope.DepartmentIds,
            BranchLabels: branchLabels,
            DepartmentLabels: departmentLabels,
            Source: scope.Source,
            HomeBranchId: scope.HomeBranchId,
            HomeDepartmentId: scope.HomeDepartmentId);
    }
}

public class GetMyDataScopeQueryHandler(
    IDataScopeEngine dataScopeEngine,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IPlatformRepository platformRepository)
    : IRequestHandler<GetMyDataScopeQuery, ApiResponse<CompanyDataScopeDto>>
{
    public async Task<ApiResponse<CompanyDataScopeDto>> Handle(
        GetMyDataScopeQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);
        var dto = await DataScopeDtoMapper.ToDtoAsync(platformRepository, scope, cancellationToken);
        return ApiResponse<CompanyDataScopeDto>.SuccessResponse(dto);
    }
}

public class GetUserDataScopeQueryHandler(
    IPlatformRepository platformRepository,
    IPlatformScope platformScope,
    IDataScopeEngine dataScopeEngine)
    : IRequestHandler<GetUserDataScopeQuery, ApiResponse<CompanyDataScopeDto>>
{
    public async Task<ApiResponse<CompanyDataScopeDto>> Handle(
        GetUserDataScopeQuery request, CancellationToken cancellationToken)
    {
        var tenantId = await platformRepository.GetUserTenantIdAsync(request.UserId, cancellationToken);
        if (!tenantId.HasValue)
            throw new NotFoundException("User", request.UserId);

        platformScope.EnsureTenantAccess(tenantId.Value);
        var scope = await dataScopeEngine.ResolveAsync(request.UserId, tenantId.Value, cancellationToken);
        var dto = await DataScopeDtoMapper.ToDtoAsync(platformRepository, scope, cancellationToken);
        return ApiResponse<CompanyDataScopeDto>.SuccessResponse(dto);
    }
}
