using SheikhTravelSystem.Application.Features.Tenants;

namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ITenantProvisioningService
{
    Task<int> ProvisionAsync(ProvisionTenantCommand request, int? createdByUserId, CancellationToken cancellationToken = default);
}
