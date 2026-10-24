using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Entities;

namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Provides JWT and refresh token generation for authenticated users.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a signed access token for the specified user.
    /// </summary>
    /// <param name="sessionId">Optional auth session id (<c>sid</c> claim) for concurrency enforcement.</param>
    string GenerateAccessToken(User user, int? driverId = null, UserAccessContext? access = null, Guid? sessionId = null);
    /// <summary>
    /// Generates a cryptographically secure refresh token.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Generates a JWT for the public customer portal (role PortalCustomer).
    /// </summary>
    string GeneratePortalAccessToken(string phone, string fullName, int tenantId, int? customerId = null);

    string GenerateDriverAccessToken(int driverId, int userId, int tenantId, string fullName, string phone, Guid? sessionId = null);
}
