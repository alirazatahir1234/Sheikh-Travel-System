using Dapper;
using MediatR;
using Microsoft.Extensions.Configuration;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Entities;

namespace SheikhTravelSystem.Application.Features.Auth.Commands;

public class LoginCommandHandler(
    IDbConnectionFactory dbFactory,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IConfiguration configuration) : IRequestHandler<LoginCommand, ApiResponse<LoginResponse>>
{
    public async Task<ApiResponse<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var user = await connection.QuerySingleOrDefaultAsync<User>(
            @"SELECT Id, FullName, Email, PasswordHash, Phone, Role, IsActive,
              RefreshToken, RefreshTokenExpiryTime, CreatedAt, UpdatedAt, IsDeleted
              FROM Users WHERE Email = @Email AND IsDeleted = 0 AND IsActive = 1",
            new { request.Email });

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return ApiResponse<LoginResponse>.FailResponse("Invalid email or password.");

        var accessToken = jwtTokenService.GenerateAccessToken(user);
        var refreshToken = jwtTokenService.GenerateRefreshToken();
        var expiryDays = int.TryParse(configuration["JwtSettings:RefreshTokenExpiryDays"], out var days) ? days : 7;

        await connection.ExecuteAsync(
            "UPDATE Users SET RefreshToken = @RefreshToken, RefreshTokenExpiryTime = @Expiry WHERE Id = @Id",
            new { RefreshToken = refreshToken, Expiry = DateTime.UtcNow.AddDays(expiryDays), user.Id });

        var response = new LoginResponse(accessToken, refreshToken, user.FullName, user.Role.ToString());
        return ApiResponse<LoginResponse>.SuccessResponse(response, "Login successful.");
    }
}
