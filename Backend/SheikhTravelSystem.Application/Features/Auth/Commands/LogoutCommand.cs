using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Auth.Commands;

/// <summary>
/// Invalidates the current authenticated session.
/// </summary>
public record LogoutCommand : IRequest<ApiResponse<bool>>;

/// <summary>
/// Handles logout token cleanup for the current user/session.
/// </summary>
public class LogoutCommandHandler(
    IAuthSessionService authSessions,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IAuditEngine auditEngine)
    : IRequestHandler<LogoutCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        await authSessions.RevokeCurrentAsync(userId, currentUser.SessionId, cancellationToken);

        try
        {
            int? tenantId = null;
            try { tenantId = tenantContext.TenantId; } catch { /* optional */ }
            await auditEngine.RecordAsync(new AuditEventWrite(
                EventKey: AuditEventKeys.Logout,
                EntityType: "User",
                EntityId: userId,
                Action: "Logout",
                Success: true,
                Message: "Logged out",
                TenantId: tenantId,
                UserId: userId), cancellationToken);
        }
        catch
        {
            // never block logout
        }

        return ApiResponse<bool>.SuccessResponse(true, "Logged out successfully.");
    }
}
