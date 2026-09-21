using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Website.Commands;
using SheikhTravelSystem.Application.Features.Website.DTOs;
using SheikhTravelSystem.Application.Features.Website.Queries;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for marketing website CMS. SQL lives in Infrastructure.
/// </summary>
public interface IWebsiteRepository
{
    Task<ApiResponse<WebsiteDashboardDto>> GetDashboardAsync(GetWebsiteDashboardQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteSettingsDto>> GetSettingsAsync(GetWebsiteSettingsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteSettingsDto>> UpdateSettingsAsync(UpdateWebsiteSettingsCommand request, CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<WebsitePageDto>>> GetPagesAsync(GetWebsitePagesQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsitePageDto>> CreatePageAsync(CreateWebsitePageCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsitePageDto>> UpdatePageAsync(UpdateWebsitePageCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsitePageDto>> PublishPageAsync(PublishWebsitePageCommand request, CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<WebsiteSectionDto>>> GetPageSectionsAsync(GetWebsitePageSectionsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<WebsiteSectionDto>>> GetHomeSectionsAsync(GetWebsiteHomeSectionsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteSectionDto>> UpsertSectionAsync(UpsertWebsiteSectionCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteSectionAsync(DeleteWebsiteSectionCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteSectionDto>> PublishSectionAsync(PublishWebsiteSectionCommand request, CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<WebsiteFeatureDto>>> GetFeaturesAsync(GetWebsiteFeaturesQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteFeatureDto>> UpsertFeatureAsync(UpsertWebsiteFeatureCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteFeatureAsync(DeleteWebsiteFeatureCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteFeatureDto>> PublishFeatureAsync(PublishWebsiteFeatureCommand request, CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<WebsiteLegalDto>>> GetLegalAsync(GetWebsiteLegalQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteLegalDto>> UpdateLegalAsync(UpdateWebsiteLegalCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteLegalDto>> PublishLegalAsync(PublishWebsiteLegalCommand request, CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<WebsiteMediaDto>>> GetMediaAsync(GetWebsiteMediaQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteMediaDto>> UploadMediaAsync(UploadWebsiteMediaCommand request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteMediaAsync(DeleteWebsiteMediaCommand request, CancellationToken cancellationToken = default);

    Task<ApiResponse<PagedResult<WebsiteContactRequestDto>>> GetContactRequestsAsync(GetWebsiteContactRequestsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteContactRequestDto>> GetContactRequestByIdAsync(GetWebsiteContactRequestByIdQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteContactRequestDto>> UpdateContactRequestStatusAsync(UpdateContactRequestStatusCommand request, CancellationToken cancellationToken = default);

    Task<ApiResponse<PagedResult<WebsiteDemoRequestDto>>> GetDemoRequestsAsync(GetWebsiteDemoRequestsQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteDemoRequestDto>> GetDemoRequestByIdAsync(GetWebsiteDemoRequestByIdQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteDemoRequestDto>> UpdateDemoRequestStatusAsync(UpdateDemoRequestStatusCommand request, CancellationToken cancellationToken = default);

    Task<ApiResponse<WebsitePublicHomeDto>> GetPublicHomeAsync(GetPublicHomeQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsitePublicPageDto>> GetPublicPageAsync(GetPublicPageQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<IReadOnlyList<WebsiteFeatureDto>>> GetPublicFeaturesAsync(GetPublicFeaturesQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteLegalDto>> GetPublicLegalAsync(GetPublicLegalQuery request, CancellationToken cancellationToken = default);
    Task<ApiResponse<WebsiteSettingsDto>> GetPublicSettingsAsync(GetPublicSettingsQuery request, CancellationToken cancellationToken = default);
}
