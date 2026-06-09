using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Tenants;

public class ProvisionTenantCommandHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope,
    ICurrentUserService currentUser,
    ITenantProvisioningService provisioningService)
    : IRequestHandler<ProvisionTenantCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(ProvisionTenantCommand request, CancellationToken cancellationToken)
    {
        if (!platformScope.IsSuperAdmin)
            return ApiResponse<int>.FailResponse("Only platform super administrators can provision tenants.");

        using var connection = dbFactory.CreateConnection();
        var slug = request.Slug.Trim().ToLowerInvariant();
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Tenants WHERE Slug = @Slug) THEN 1 ELSE 0 END",
            new { Slug = slug }, cancellationToken: cancellationToken));

        if (exists)
            return ApiResponse<int>.FailResponse("Tenant slug already exists.");

        var tenantId = await provisioningService.ProvisionAsync(
            request with { Slug = slug }, currentUser.UserId, cancellationToken);
        return ApiResponse<int>.SuccessResponse(tenantId, "Tenant provisioned.");
    }
}
