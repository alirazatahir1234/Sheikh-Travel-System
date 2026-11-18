using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Queries;

public record GetUserByIdQuery(int Id) : IRequest<ApiResponse<UserDto>>;

public class GetUserByIdQueryHandler(
    IUserRepository userRepository,
    IPlatformScope platformScope) : IRequestHandler<GetUserByIdQuery, ApiResponse<UserDto>>
{
    public async Task<ApiResponse<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
            throw new NotFoundException("User", request.Id);

        if (user.CompanyId is > 0 and var tenantId)
            platformScope.EnsureTenantAccess(tenantId);
        else if (!platformScope.HasPermission(PlatformPermissions.TenantsDataRead))
            // SG-01: a user with no company belongs to no tenant, so only a platform-level reader may
            // see it. Previously this branch skipped the check and returned the user to anyone.
            throw new ForbiddenException("Cross-tenant access is not allowed.");

        return ApiResponse<UserDto>.SuccessResponse(user);
    }
}
