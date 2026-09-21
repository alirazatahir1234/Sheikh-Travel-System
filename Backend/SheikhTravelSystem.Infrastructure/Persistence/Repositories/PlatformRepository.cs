using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class PlatformRepository(IDbConnectionFactory dbFactory) : IPlatformRepository
{
}
