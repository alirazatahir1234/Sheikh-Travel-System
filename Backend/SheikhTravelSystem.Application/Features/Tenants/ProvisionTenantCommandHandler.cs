using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Tenants;

public class ProvisionTenantCommandHandler(
    ITenantRepository tenantRepository,
    IPlatformScope platformScope,
    ICurrentUserService currentUser,
    ITenantProvisioningService provisioningService)
    : IRequestHandler<ProvisionTenantCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(ProvisionTenantCommand request, CancellationToken cancellationToken)
    {
        if (!platformScope.IsSuperAdmin)
            return ApiResponse<int>.FailResponse("Only platform super administrators can provision tenants.");

        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await tenantRepository.SlugExistsAsync(slug, cancellationToken))
            return ApiResponse<int>.FailResponse("Tenant slug already exists.");

        var tenantId = await provisioningService.ProvisionAsync(
            request with { Slug = slug }, currentUser.UserId, cancellationToken);
        return ApiResponse<int>.SuccessResponse(tenantId, "Tenant provisioned.");
    }
}
