using MediatR;
using Microsoft.Extensions.Configuration;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Platform;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Auth.Commands;

/// <summary>
/// Rotates refresh tokens and returns a new access token pair for the same auth session.
/// </summary>
public class RefreshTokenCommandHandler(
    IAuthRepository authRepository,
    IAuthSessionService authSessions,
    IJwtTokenService jwtTokenService,
    IUserAccessService userAccessService,
    IPlatformRepository platformRepository,
    IConfiguration configuration) : IRequestHandler<RefreshTokenCommand, ApiResponse<LoginResponse>>
{
    public async Task<ApiResponse<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var session = await authSessions.FindActiveByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        Domain.Entities.User? user = null;
        Guid? sessionId = null;

        if (session is not null)
        {
            sessionId = session.Id;
            user = await authRepository.GetActiveUserByIdAsync(session.UserId, cancellationToken);
        }
        else
        {
            // Legacy path: refresh token only on Users (pre-AuthSessions).
            user = await authRepository.FindUserByValidRefreshTokenAsync(request.RefreshToken, cancellationToken);
        }

        if (user is null)
            return ApiResponse<LoginResponse>.FailResponse("Session is no longer valid. Please log in again.");

        int? driverId = null;
        if (user.Role == UserRole.Driver)
        {
            driverId = await authRepository.GetDriverIdByUserIdAsync(user.Id, cancellationToken);
        }

        var access = await userAccessService.ResolveAsync(user.Id, user.TenantId, cancellationToken);
        var isPlatformSuperAdmin = access.RoleCodes.Any(c =>
            string.Equals(c, PlatformRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase));
        if (!isPlatformSuperAdmin)
        {
            var gate = await platformRepository.GetTenantSubscriptionAccessAsync(user.TenantId, cancellationToken);
            if (gate is not null)
            {
                if (!gate.Value.TenantIsActive)
                {
                    return ApiResponse<LoginResponse>.FailResponse(
                        "This company account is deactivated. Contact SheikhGo billing to restore access.",
                        code: "tenant_deactivated");
                }

                if (string.Equals(gate.Value.SubscriptionStatus, SubscriptionBillingLifecycle.StatusExpired,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return ApiResponse<LoginResponse>.FailResponse(
                        "Your subscription has expired. Contact SheikhGo billing to renew.",
                        code: "subscription_expired");
                }
            }
        }

        var newRefreshToken = jwtTokenService.GenerateRefreshToken();
        var expiryDays = int.TryParse(configuration["JwtSettings:RefreshTokenExpiryDays"], out var days) ? days : 7;
        var refreshExpiry = DateTime.UtcNow.AddDays(expiryDays);

        if (sessionId is Guid sid)
        {
            await authSessions.RotateRefreshTokenAsync(sid, user.Id, newRefreshToken, refreshExpiry, cancellationToken);
        }
        else
        {
            // Promote legacy token into AuthSessions (counts toward tenant concurrency).
            var created = await authSessions.CreateSessionAsync(
                user.TenantId,
                user.Id,
                newRefreshToken,
                refreshExpiry,
                AuthSessionLimits.ClientKindWeb,
                cancellationToken);
            if (created.LimitReached)
            {
                return ApiResponse<LoginResponse>.FailResponse(
                    AuthSessionLimits.LimitReachedMessage,
                    code: AuthSessionLimits.LimitReachedCode);
            }

            sessionId = created.SessionId;
        }

        var accessToken = jwtTokenService.GenerateAccessToken(user, driverId, access, sessionId);

        var orderedRoles = RoleCodePriority.OrderByPrivilege(access.RoleCodes);
        var primaryRole = RoleCodePriority.SelectPrimary(orderedRoles) ?? user.Role.ToString();
        var response = new LoginResponse(
            accessToken,
            newRefreshToken,
            user.FullName,
            primaryRole,
            user.Email,
            user.Phone,
            user.TenantId,
            user.Id,
            orderedRoles,
            access.Permissions);
        return ApiResponse<LoginResponse>.SuccessResponse(response, "Token refreshed successfully.");
    }
}
