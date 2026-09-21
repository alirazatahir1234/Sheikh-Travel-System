using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Queries;

public record GetCurrentUserProfileQuery : IRequest<ApiResponse<UserProfileDto>>;

public class GetCurrentUserProfileQueryHandler(
    IUserRepository userRepository,
    ICurrentUserService currentUser,
    IPermissionEngine permissionEngine)
    : IRequestHandler<GetCurrentUserProfileQuery, ApiResponse<UserProfileDto>>
{
    public async Task<ApiResponse<UserProfileDto>> Handle(
        GetCurrentUserProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not int userId)
            return ApiResponse<UserProfileDto>.FailResponse("Not authenticated.");

        var profile = await userRepository.GetProfileAsync(userId, cancellationToken);
        if (profile is null)
            throw new NotFoundException("User", userId);

        IReadOnlyList<EffectivePermissionDto>? effective = null;
        try
        {
            var tenantId = profile.CompanyId ?? 0;
            if (tenantId > 0)
            {
                var eval = await permissionEngine.EvaluateAsync(userId, tenantId, cancellationToken);
                effective = eval.EffectivePermissions;
            }
        }
        catch
        {
            // optional
        }

        return ApiResponse<UserProfileDto>.SuccessResponse(
            profile with { EffectivePermissions = effective });
    }
}

public record GetCompanyUserSummaryQuery(int? TenantId = null)
    : IRequest<ApiResponse<CompanyUserSummaryDto>>;

public class GetCompanyUserSummaryQueryHandler(
    IUserRepository userRepository,
    IPlatformScope platformScope)
    : IRequestHandler<GetCompanyUserSummaryQuery, ApiResponse<CompanyUserSummaryDto>>
{
    public async Task<ApiResponse<CompanyUserSummaryDto>> Handle(
        GetCompanyUserSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? platformScope.TenantId;
        platformScope.EnsureTenantAccess(tenantId);

        var summary = await userRepository.GetCompanyUserSummaryAsync(tenantId, cancellationToken);
        return ApiResponse<CompanyUserSummaryDto>.SuccessResponse(summary);
    }
}
