namespace SheikhTravelSystem.Application.Common.Interfaces;

public enum DataScopeMode
{
    Company = 0,
    Branch = 1,
    Department = 2
}

public record DataScopeResult(
    int UserId,
    int TenantId,
    DataScopeMode Mode,
    bool IsCompanyWide,
    IReadOnlyList<int> BranchIds,
    IReadOnlyList<int> DepartmentIds,
    int? HomeBranchId,
    int? HomeDepartmentId,
    string Source);

/// <summary>
/// Stage 12 Data Scope Engine: resolves company / branch / department clamp within a tenant.
/// </summary>
public interface IDataScopeEngine
{
    Task<DataScopeResult> ResolveAsync(
        int userId,
        int tenantId,
        CancellationToken cancellationToken = default);
}
