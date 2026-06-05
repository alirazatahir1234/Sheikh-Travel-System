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
    string GenerateAccessToken(User user, int? driverId = null);
    /// <summary>
    /// Generates a cryptographically secure refresh token.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Generates a JWT for the public customer portal (role PortalCustomer).
    /// </summary>
    string GeneratePortalAccessToken(string phone, string fullName, int tenantId, int? customerId = null);

    string GenerateDriverAccessToken(int driverId, int userId, int tenantId, string fullName, string phone);
}
