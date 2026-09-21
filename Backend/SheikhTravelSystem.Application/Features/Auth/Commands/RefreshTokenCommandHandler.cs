using MediatR;
using Microsoft.Extensions.Configuration;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Auth.Commands;

/// <summary>
/// Rotates refresh tokens and returns a new access token pair.
/// </summary>
public class RefreshTokenCommandHandler(
    IAuthRepository authRepository,
    IJwtTokenService jwtTokenService,
    IUserAccessService userAccessService,
    IConfiguration configuration) : IRequestHandler<RefreshTokenCommand, ApiResponse<LoginResponse>>
{
    /// <summary>
    /// Validates the provided refresh token and issues replacement tokens.
    /// </summary>
    public async Task<ApiResponse<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await authRepository.FindUserByValidRefreshTokenAsync(request.RefreshToken, cancellationToken);

        if (user is null)
            return ApiResponse<LoginResponse>.FailResponse("Invalid or expired refresh token.");

        int? driverId = null;
        if (user.Role == UserRole.Driver)
        {
            driverId = await authRepository.GetDriverIdByUserIdAsync(user.Id, cancellationToken);
        }

        var access = await userAccessService.ResolveAsync(user.Id, user.TenantId, cancellationToken);
        var accessToken = jwtTokenService.GenerateAccessToken(user, driverId, access);
        var newRefreshToken = jwtTokenService.GenerateRefreshToken();
        var expiryDays = int.TryParse(configuration["JwtSettings:RefreshTokenExpiryDays"], out var days) ? days : 7;

        await authRepository.UpdateRefreshTokenAsync(
            user.Id,
            newRefreshToken,
            DateTime.UtcNow.AddDays(expiryDays),
            cancellationToken);

        var primaryRole = access.RoleCodes.FirstOrDefault() ?? user.Role.ToString();
        var response = new LoginResponse(
            accessToken,
            newRefreshToken,
            user.FullName,
            primaryRole,
            user.Email,
            user.Phone,
            user.TenantId,
            user.Id,
            access.RoleCodes,
            access.Permissions);
        return ApiResponse<LoginResponse>.SuccessResponse(response, "Token refreshed successfully.");
    }
}
