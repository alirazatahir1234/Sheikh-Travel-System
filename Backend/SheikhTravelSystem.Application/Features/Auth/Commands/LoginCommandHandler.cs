using Dapper;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Entities;

namespace SheikhTravelSystem.Application.Features.Auth.Commands;

/// <summary>
/// Authenticates users and issues access/refresh tokens.
/// </summary>
public class LoginCommandHandler(
    IDbConnectionFactory dbFactory,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IConfiguration configuration,
    ILogger<LoginCommandHandler> logger) : IRequestHandler<LoginCommand, ApiResponse<LoginResponse>>
{
    /// <summary>
    /// Validates credentials and persists a new refresh token for the user.
    /// </summary>
    public async Task<ApiResponse<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var user = await connection.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                @"SELECT Id, FullName, Email, PasswordHash, Phone, Role, IsActive,
                  RefreshToken, RefreshTokenExpiryTime, CreatedAt, UpdatedAt, IsDeleted
                  FROM Users WHERE Email = @Email AND IsDeleted = 0 AND IsActive = 1",
                new { request.Email },
                cancellationToken: cancellationToken));

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for email {Email}", request.Email);
            return ApiResponse<LoginResponse>.FailResponse("Invalid email or password.");
        }

        var accessToken = jwtTokenService.GenerateAccessToken(user);
        var refreshToken = jwtTokenService.GenerateRefreshToken();
        var expiryDays = int.TryParse(configuration["JwtSettings:RefreshTokenExpiryDays"], out var days) ? days : 7;

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET RefreshToken = @RefreshToken, RefreshTokenExpiryTime = @Expiry WHERE Id = @Id",
                new { RefreshToken = refreshToken, Expiry = DateTime.UtcNow.AddDays(expiryDays), user.Id },
                cancellationToken: cancellationToken));

        logger.LogInformation("User {Email} logged in successfully", request.Email);
        var response = new LoginResponse(accessToken, refreshToken, user.FullName, user.Role.ToString());
        return ApiResponse<LoginResponse>.SuccessResponse(response, "Login successful.");
    }
}
