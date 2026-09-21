using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Application.Features.Users.Queries;

public class GetUserPermissionsQueryHandler(
    IUserRepository userRepository,
    IPlatformScope platformScope,
    IPermissionEngine permissionEngine)
    : IRequestHandler<GetUserPermissionsQuery, ApiResponse<IReadOnlyList<EffectivePermissionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<EffectivePermissionDto>>> Handle(
        GetUserPermissionsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = await userRepository.GetTenantIdAsync(request.UserId, cancellationToken);
        if (!tenantId.HasValue)
            throw new NotFoundException("User", request.UserId);

        platformScope.EnsureTenantAccess(tenantId.Value);
        var result = await permissionEngine.EvaluateAsync(request.UserId, tenantId.Value, cancellationToken);
        return ApiResponse<IReadOnlyList<EffectivePermissionDto>>.SuccessResponse(result.EffectivePermissions);
    }
}
