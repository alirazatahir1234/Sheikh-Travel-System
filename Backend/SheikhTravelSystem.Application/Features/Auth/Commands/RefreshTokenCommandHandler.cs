using Dapper;
using MediatR;
using Microsoft.Extensions.Configuration;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Entities;

namespace SheikhTravelSystem.Application.Features.Auth.Commands;

/// <summary>
/// Rotates refresh tokens and returns a new access token pair.
/// </summary>
public class RefreshTokenCommandHandler(
    IDbConnectionFactory dbFactory,
    IJwtTokenService jwtTokenService,
    IConfiguration configuration) : IRequestHandler<RefreshTokenCommand, ApiResponse<LoginResponse>>
{
    /// <summary>
    /// Validates the provided refresh token and issues replacement tokens.
    /// </summary>
    public async Task<ApiResponse<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var user = await connection.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                @"SELECT Id, FullName, Email, PasswordHash, Phone, Role, IsActive,
                  RefreshToken, RefreshTokenExpiryTime, CreatedAt, UpdatedAt, IsDeleted
                  FROM Users WHERE RefreshToken = @RefreshToken AND RefreshTokenExpiryTime > @Now AND IsDeleted = 0",
                new { request.RefreshToken, Now = DateTime.UtcNow },
                cancellationToken: cancellationToken));

        if (user is null)
            return ApiResponse<LoginResponse>.FailResponse("Invalid or expired refresh token.");

        var accessToken = jwtTokenService.GenerateAccessToken(user);
        var newRefreshToken = jwtTokenService.GenerateRefreshToken();
        var expiryDays = int.TryParse(configuration["JwtSettings:RefreshTokenExpiryDays"], out var days) ? days : 7;

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET RefreshToken = @RefreshToken, RefreshTokenExpiryTime = @Expiry WHERE Id = @Id",
                new { RefreshToken = newRefreshToken, Expiry = DateTime.UtcNow.AddDays(expiryDays), user.Id },
                cancellationToken: cancellationToken));

        var response = new LoginResponse(accessToken, newRefreshToken, user.FullName, user.Role.ToString());
        return ApiResponse<LoginResponse>.SuccessResponse(response, "Token refreshed successfully.");
    }
}
