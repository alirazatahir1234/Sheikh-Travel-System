using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Common;

/// <summary>
/// Branch clamp for TravelNexus records (customers own the BranchId; travelers inherit it).
/// Company-wide users see everything in the tenant; branch-scoped users see their branches
/// plus records with no branch.
/// </summary>
public sealed record TravelBranchScope(bool IsCompanyWide, IReadOnlyList<int> BranchIds)
{
    public static readonly TravelBranchScope CompanyWide = new(true, []);

    public bool Allows(int? branchId)
        => IsCompanyWide || branchId is null || BranchIds.Contains(branchId.Value);
}

public interface ITravelBranchScopeResolver
{
    Task<TravelBranchScope> ResolveAsync(int tenantId, CancellationToken cancellationToken);
}

public sealed class TravelBranchScopeResolver(
    IDataScopeEngine dataScopeEngine,
    ICurrentUserService currentUser) : ITravelBranchScopeResolver
{
    public async Task<TravelBranchScope> ResolveAsync(int tenantId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("An authenticated user is required.");
        var scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);
        return scope.IsCompanyWide
            ? TravelBranchScope.CompanyWide
            : new TravelBranchScope(false, scope.BranchIds);
    }
}
