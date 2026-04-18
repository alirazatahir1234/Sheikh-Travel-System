using SheikhTravelSystem.Domain.Entities;

namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}
