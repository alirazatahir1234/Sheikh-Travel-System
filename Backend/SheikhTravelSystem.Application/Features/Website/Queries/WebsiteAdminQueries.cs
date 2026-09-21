using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Website.DTOs;

namespace SheikhTravelSystem.Application.Features.Website.Queries;

public record GetWebsiteDashboardQuery : IRequest<ApiResponse<WebsiteDashboardDto>>;
public record GetWebsiteSettingsQuery : IRequest<ApiResponse<WebsiteSettingsDto>>;
public record GetWebsitePagesQuery : IRequest<ApiResponse<IReadOnlyList<WebsitePageDto>>>;
public record GetWebsitePageSectionsQuery(int PageId) : IRequest<ApiResponse<IReadOnlyList<WebsiteSectionDto>>>;
public record GetWebsiteHomeSectionsQuery : IRequest<ApiResponse<IReadOnlyList<WebsiteSectionDto>>>;
public record GetWebsiteFeaturesQuery : IRequest<ApiResponse<IReadOnlyList<WebsiteFeatureDto>>>;
public record GetWebsiteLegalQuery(string? DocType = null) : IRequest<ApiResponse<IReadOnlyList<WebsiteLegalDto>>>;
public record GetWebsiteMediaQuery : IRequest<ApiResponse<IReadOnlyList<WebsiteMediaDto>>>;
public record GetWebsiteContactRequestsQuery(string? Status = null, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<WebsiteContactRequestDto>>>;
public record GetWebsiteDemoRequestsQuery(string? Status = null, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<WebsiteDemoRequestDto>>>;
public record GetWebsiteContactRequestByIdQuery(int Id) : IRequest<ApiResponse<WebsiteContactRequestDto>>;
public record GetWebsiteDemoRequestByIdQuery(int Id) : IRequest<ApiResponse<WebsiteDemoRequestDto>>;

public class GetWebsiteDashboardQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetWebsiteDashboardQuery, ApiResponse<WebsiteDashboardDto>>
{
    public Task<ApiResponse<WebsiteDashboardDto>> Handle(GetWebsiteDashboardQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetDashboardAsync(request, cancellationToken);
}
public class GetWebsiteSettingsQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetWebsiteSettingsQuery, ApiResponse<WebsiteSettingsDto>>
{
    public Task<ApiResponse<WebsiteSettingsDto>> Handle(GetWebsiteSettingsQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetSettingsAsync(request, cancellationToken);
}
public class GetWebsitePagesQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetWebsitePagesQuery, ApiResponse<IReadOnlyList<WebsitePageDto>>>
{
    public Task<ApiResponse<IReadOnlyList<WebsitePageDto>>> Handle(GetWebsitePagesQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetPagesAsync(request, cancellationToken);
}
public class GetWebsitePageSectionsQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetWebsitePageSectionsQuery, ApiResponse<IReadOnlyList<WebsiteSectionDto>>>
{
    public Task<ApiResponse<IReadOnlyList<WebsiteSectionDto>>> Handle(GetWebsitePageSectionsQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetPageSectionsAsync(request, cancellationToken);
}
public class GetWebsiteHomeSectionsQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetWebsiteHomeSectionsQuery, ApiResponse<IReadOnlyList<WebsiteSectionDto>>>
{
    public Task<ApiResponse<IReadOnlyList<WebsiteSectionDto>>> Handle(GetWebsiteHomeSectionsQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetHomeSectionsAsync(request, cancellationToken);
}
public class GetWebsiteFeaturesQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetWebsiteFeaturesQuery, ApiResponse<IReadOnlyList<WebsiteFeatureDto>>>
{
    public Task<ApiResponse<IReadOnlyList<WebsiteFeatureDto>>> Handle(GetWebsiteFeaturesQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetFeaturesAsync(request, cancellationToken);
}
public class GetWebsiteLegalQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetWebsiteLegalQuery, ApiResponse<IReadOnlyList<WebsiteLegalDto>>>
{
    public Task<ApiResponse<IReadOnlyList<WebsiteLegalDto>>> Handle(GetWebsiteLegalQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetLegalAsync(request, cancellationToken);
}
public class GetWebsiteMediaQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetWebsiteMediaQuery, ApiResponse<IReadOnlyList<WebsiteMediaDto>>>
{
    public Task<ApiResponse<IReadOnlyList<WebsiteMediaDto>>> Handle(GetWebsiteMediaQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetMediaAsync(request, cancellationToken);
}
public class GetWebsiteContactRequestsQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetWebsiteContactRequestsQuery, ApiResponse<PagedResult<WebsiteContactRequestDto>>>
{
    public Task<ApiResponse<PagedResult<WebsiteContactRequestDto>>> Handle(GetWebsiteContactRequestsQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetContactRequestsAsync(request, cancellationToken);
}
public class GetWebsiteDemoRequestsQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetWebsiteDemoRequestsQuery, ApiResponse<PagedResult<WebsiteDemoRequestDto>>>
{
    public Task<ApiResponse<PagedResult<WebsiteDemoRequestDto>>> Handle(GetWebsiteDemoRequestsQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetDemoRequestsAsync(request, cancellationToken);
}
public class GetWebsiteContactRequestByIdQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetWebsiteContactRequestByIdQuery, ApiResponse<WebsiteContactRequestDto>>
{
    public Task<ApiResponse<WebsiteContactRequestDto>> Handle(GetWebsiteContactRequestByIdQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetContactRequestByIdAsync(request, cancellationToken);
}
public class GetWebsiteDemoRequestByIdQueryHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<GetWebsiteDemoRequestByIdQuery, ApiResponse<WebsiteDemoRequestDto>>
{
    public Task<ApiResponse<WebsiteDemoRequestDto>> Handle(GetWebsiteDemoRequestByIdQuery request, CancellationToken cancellationToken)
        => websiteRepository.GetDemoRequestByIdAsync(request, cancellationToken);
}
