using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Auth.Commands;

public record LogoutCommand : IRequest<ApiResponse<bool>>;

public class LogoutCommandHandler(IDbConnectionFactory dbFactory, ICurrentUserService currentUser)
    : IRequestHandler<LogoutCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(
            "UPDATE Users SET RefreshToken = NULL, RefreshTokenExpiryTime = NULL WHERE Id = @UserId",
            new { UserId = userId });

        return ApiResponse<bool>.SuccessResponse(true, "Logged out successfully.");
    }
}
