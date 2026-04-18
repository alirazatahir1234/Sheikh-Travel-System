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
    string GenerateAccessToken(User user);
    /// <summary>
    /// Generates a cryptographically secure refresh token.
    /// </summary>
    string GenerateRefreshToken();
}
