using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

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
        IDbConnectionFactory dbFactory,
        DataScopeResult scope,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var branchLabels = Array.Empty<string>();
        var departmentLabels = Array.Empty<string>();

        if (scope.BranchIds.Count > 0)
        {
            branchLabels = (await connection.QueryAsync<string>(new CommandDefinition("""
                SELECT Name FROM Branches
                WHERE TenantId = @TenantId AND Id IN @Ids
                ORDER BY Name
                """,
                new { scope.TenantId, Ids = scope.BranchIds.ToArray() },
                cancellationToken: cancellationToken))).ToArray();
        }

        if (scope.DepartmentIds.Count > 0)
        {
            departmentLabels = (await connection.QueryAsync<string>(new CommandDefinition("""
                SELECT Name FROM Departments
                WHERE TenantId = @TenantId AND Id IN @Ids
                ORDER BY Name
                """,
                new { scope.TenantId, Ids = scope.DepartmentIds.ToArray() },
                cancellationToken: cancellationToken))).ToArray();
        }

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
    IDbConnectionFactory dbFactory)
    : IRequestHandler<GetMyDataScopeQuery, ApiResponse<CompanyDataScopeDto>>
{
    public async Task<ApiResponse<CompanyDataScopeDto>> Handle(
        GetMyDataScopeQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);
        var dto = await DataScopeDtoMapper.ToDtoAsync(dbFactory, scope, cancellationToken);
        return ApiResponse<CompanyDataScopeDto>.SuccessResponse(dto);
    }
}

public class GetUserDataScopeQueryHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope,
    IDataScopeEngine dataScopeEngine)
    : IRequestHandler<GetUserDataScopeQuery, ApiResponse<CompanyDataScopeDto>>
{
    public async Task<ApiResponse<CompanyDataScopeDto>> Handle(
        GetUserDataScopeQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TenantId FROM Users WHERE Id = @Id AND IsDeleted = 0",
            new { Id = request.UserId },
            cancellationToken: cancellationToken));
        if (!tenantId.HasValue)
            throw new NotFoundException("User", request.UserId);

        platformScope.EnsureTenantAccess(tenantId.Value);
        var scope = await dataScopeEngine.ResolveAsync(request.UserId, tenantId.Value, cancellationToken);
        var dto = await DataScopeDtoMapper.ToDtoAsync(dbFactory, scope, cancellationToken);
        return ApiResponse<CompanyDataScopeDto>.SuccessResponse(dto);
    }
}
