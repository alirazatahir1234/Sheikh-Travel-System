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

        var tenantId = user.CompanyId ?? 0;
        if (tenantId > 0)
            platformScope.EnsureTenantAccess(tenantId);

        return ApiResponse<UserDto>.SuccessResponse(user);
    }
}
