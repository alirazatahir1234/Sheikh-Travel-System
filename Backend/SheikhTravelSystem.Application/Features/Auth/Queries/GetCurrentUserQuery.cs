using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Auth.Queries;

/// <summary>
/// Returns the authenticated user's profile from persistence.
/// </summary>
public record GetCurrentUserQuery : IRequest<ApiResponse<UserDto>>;

/// <summary>
/// Loads the current user row by id from the JWT context.
/// </summary>
public class GetCurrentUserQueryHandler(IAuthRepository authRepository, ICurrentUserService currentUser)
    : IRequestHandler<GetCurrentUserQuery, ApiResponse<UserDto>>
{
    /// <summary>
    /// Resolves the caller's user id and returns the matching non-deleted user.
    /// </summary>
    public async Task<ApiResponse<UserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var user = await authRepository.GetCurrentUserAsync(userId, cancellationToken);
        return ApiResponse<UserDto>.SuccessResponse(user, "Current user retrieved successfully.");
    }
}
