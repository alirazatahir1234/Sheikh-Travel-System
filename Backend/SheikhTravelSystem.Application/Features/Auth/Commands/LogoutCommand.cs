using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Auth.Commands;

/// <summary>
/// Invalidates the current authenticated user's refresh token.
/// </summary>
public record LogoutCommand : IRequest<ApiResponse<bool>>;

/// <summary>
/// Handles logout token cleanup for the current user.
/// </summary>
public class LogoutCommandHandler(IDbConnectionFactory dbFactory, ICurrentUserService currentUser)
    : IRequestHandler<LogoutCommand, ApiResponse<bool>>
{
    /// <summary>
    /// Clears persisted refresh token fields for the current user.
    /// </summary>
    public async Task<ApiResponse<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET RefreshToken = NULL, RefreshTokenExpiryTime = NULL WHERE Id = @UserId",
                new { UserId = userId },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Logged out successfully.");
    }
}
