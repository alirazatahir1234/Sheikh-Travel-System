using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Tenants.Queries;

public record GetTenantBrandingQuery : IRequest<ApiResponse<TenantBrandingDto>>;

public record UpdateTenantBrandingCommand(
    int TenantId,
    string? LogoUrl,
    string? PrimaryColor,
    string? Website,
    string? SupportEmail,
    string? Country,
    string? CurrencyCode,
    string? TimeZone) : IRequest<ApiResponse<bool>>;

public record TenantBrandingDto(
    int Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string? PrimaryColor,
    IReadOnlyList<string> EnabledModules)
{
    /// <summary>Company alias for product language (persistence remains Tenant).</summary>
    public int CompanyId => Id;
    public string CompanyName => Name;
    public int TenantId => Id;
}

public class GetTenantBrandingQueryHandler(
    ITenantRepository tenantRepository,
    ITenantContext tenantContext,
    ITenantModuleService tenantModuleService)
    : IRequestHandler<GetTenantBrandingQuery, ApiResponse<TenantBrandingDto>>
{
    public async Task<ApiResponse<TenantBrandingDto>> Handle(GetTenantBrandingQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await tenantRepository.GetBrandingAsync(tenantId, cancellationToken);

        if (row is null)
            return ApiResponse<TenantBrandingDto>.FailResponse("Tenant not found.");

        var modules = await tenantModuleService.GetLegacyModuleKeysAsync(tenantId, cancellationToken);
        return ApiResponse<TenantBrandingDto>.SuccessResponse(
            new TenantBrandingDto(row.Id, row.Name, row.Slug, row.LogoUrl, row.PrimaryColor, modules));
    }
}

public class UpdateTenantBrandingCommandHandler(
    ITenantRepository tenantRepository,
    IPlatformScope platformScope)
    : IRequestHandler<UpdateTenantBrandingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTenantBrandingCommand request, CancellationToken cancellationToken)
    {
        platformScope.EnsureTenantAccess(request.TenantId);

        await tenantRepository.UpdateBrandingAsync(
            request.TenantId,
            request.LogoUrl,
            request.PrimaryColor,
            request.Website,
            request.SupportEmail,
            request.Country,
            request.CurrencyCode,
            request.TimeZone,
            cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Tenant branding updated.");
    }
}
