using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Auth.Queries;

/// <summary>
/// Returns the authenticated user's profile from persistence.
/// </summary>
public record GetCurrentUserQuery : IRequest<ApiResponse<UserDto>>;

/// <summary>
/// Loads the current user row by id from the JWT context.
/// </summary>
public class GetCurrentUserQueryHandler(IDbConnectionFactory dbFactory, ICurrentUserService currentUser)
    : IRequestHandler<GetCurrentUserQuery, ApiResponse<UserDto>>
{
    /// <summary>
    /// Resolves the caller's user id and returns the matching non-deleted user.
    /// </summary>
    public async Task<ApiResponse<UserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        using var connection = dbFactory.CreateConnection();

        var user = await connection.QuerySingleOrDefaultAsync<UserDto>(
            new CommandDefinition(
                @"SELECT Id, FullName, Email, Phone, Role, IsActive, CreatedAt
                  FROM Users WHERE Id = @Id AND IsDeleted = 0",
                new { Id = userId },
                cancellationToken: cancellationToken));

        if (user is null)
        {
            throw new NotFoundException("User", userId);
        }

        return ApiResponse<UserDto>.SuccessResponse(user, "Current user retrieved successfully.");
    }
}
