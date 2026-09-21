using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Website.DTOs;

namespace SheikhTravelSystem.Application.Features.Website.Queries;

public record GetPublicHomeQuery : IRequest<ApiResponse<WebsitePublicHomeDto>>;
public record GetPublicPageQuery(string Slug) : IRequest<ApiResponse<WebsitePublicPageDto>>;
public record GetPublicFeaturesQuery : IRequest<ApiResponse<IReadOnlyList<WebsiteFeatureDto>>>;
public record GetPublicLegalQuery(string DocType) : IRequest<ApiResponse<WebsiteLegalDto>>;
public record GetPublicSettingsQuery : IRequest<ApiResponse<WebsiteSettingsDto>>;

public class GetPublicHomeQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetPublicHomeQuery, ApiResponse<WebsitePublicHomeDto>>
{
    public Task<ApiResponse<WebsitePublicHomeDto>> Handle(GetPublicHomeQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetPublicHomeAsync(request, cancellationToken);
}
public class GetPublicPageQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetPublicPageQuery, ApiResponse<WebsitePublicPageDto>>
{
    public Task<ApiResponse<WebsitePublicPageDto>> Handle(GetPublicPageQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetPublicPageAsync(request, cancellationToken);
}
public class GetPublicFeaturesQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetPublicFeaturesQuery, ApiResponse<IReadOnlyList<WebsiteFeatureDto>>>
{
    public Task<ApiResponse<IReadOnlyList<WebsiteFeatureDto>>> Handle(GetPublicFeaturesQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetPublicFeaturesAsync(request, cancellationToken);
}
public class GetPublicLegalQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetPublicLegalQuery, ApiResponse<WebsiteLegalDto>>
{
    public Task<ApiResponse<WebsiteLegalDto>> Handle(GetPublicLegalQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetPublicLegalAsync(request, cancellationToken);
}
public class GetPublicSettingsQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetPublicSettingsQuery, ApiResponse<WebsiteSettingsDto>>
{
    public Task<ApiResponse<WebsiteSettingsDto>> Handle(GetPublicSettingsQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetPublicSettingsAsync(request, cancellationToken);
}

public static class WebsiteTenant
{
    public const int MarketingTenantId = 1;

    public static int Resolve(ITenantContext? tenantContext)
    {
        var id = tenantContext?.TenantId ?? 0;
        return id > 0 ? id : MarketingTenantId;
    }
}
