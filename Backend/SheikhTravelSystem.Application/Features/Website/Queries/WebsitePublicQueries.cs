using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Website.DTOs;

namespace SheikhTravelSystem.Application.Features.Website.Queries;

public record GetPublicHomeQuery : IRequest<ApiResponse<WebsitePublicHomeDto>>;

public record GetPublicPageQuery(string Slug) : IRequest<ApiResponse<WebsitePublicPageDto>>;

public record GetPublicFeaturesQuery : IRequest<ApiResponse<IReadOnlyList<WebsiteFeatureDto>>>;

public record GetPublicLegalQuery(string DocType) : IRequest<ApiResponse<WebsiteLegalDto>>;

public record GetPublicSettingsQuery : IRequest<ApiResponse<WebsiteSettingsDto>>;

internal static class WebsitePublicSql
{
    public const string Settings = """
        SELECT Id, SiteName, LogoUrl, FaviconUrl, SupportEmail, SalesEmail, PrivacyEmail,
               Phone, Address, LinkedInUrl, FacebookUrl, XUrl, YouTubeUrl,
               DefaultMetaTitle, DefaultMetaDescription, AnalyticsId
        FROM WebsiteSettings
        WHERE TenantId = @TenantId
        """;

    public const string PublishedFeatures = """
        SELECT Id, Title, Description, IconKey, ImageUrl, LinkUrl, DisplayOrder, IsActive, Status
        FROM WebsiteFeatures
        WHERE TenantId = @TenantId AND IsActive = 1 AND Status = N'Published'
        ORDER BY DisplayOrder, Id
        """;

    public const string PageSections = """
        SELECT Id, PageId, SectionType, Title, Subtitle, Content, ImageUrl,
               ButtonText, ButtonUrl, SecondaryButtonText, SecondaryButtonUrl,
               DisplayOrder, IsActive, Status
        FROM WebsiteSections
        WHERE TenantId = @TenantId AND PageId = @PageId AND IsActive = 1 AND Status = N'Published'
        ORDER BY DisplayOrder, Id
        """;
}

public class GetPublicHomeQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetPublicHomeQuery, ApiResponse<WebsitePublicHomeDto>>
{
    public async Task<ApiResponse<WebsitePublicHomeDto>> Handle(GetPublicHomeQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();

        var settings = await connection.QuerySingleOrDefaultAsync<WebsiteSettingsDto>(
            new CommandDefinition(WebsitePublicSql.Settings, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        if (settings is null)
            return ApiResponse<WebsitePublicHomeDto>.FailResponse("Website settings not found.");

        var sections = (await connection.QueryAsync<WebsiteSectionDto>(
            new CommandDefinition("""
                SELECT s.Id, s.PageId, s.SectionType, s.Title, s.Subtitle, s.Content, s.ImageUrl,
                       s.ButtonText, s.ButtonUrl, s.SecondaryButtonText, s.SecondaryButtonUrl,
                       s.DisplayOrder, s.IsActive, s.Status
                FROM WebsiteSections s
                INNER JOIN WebsitePages p ON p.Id = s.PageId AND p.TenantId = s.TenantId
                WHERE s.TenantId = @TenantId AND p.Slug = N'home'
                  AND s.IsActive = 1 AND s.Status = N'Published'
                ORDER BY s.DisplayOrder, s.Id
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

        var features = (await connection.QueryAsync<WebsiteFeatureDto>(
            new CommandDefinition(WebsitePublicSql.PublishedFeatures, new { TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        return ApiResponse<WebsitePublicHomeDto>.SuccessResponse(
            new WebsitePublicHomeDto(settings, sections, features));
    }
}

public class GetPublicPageQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetPublicPageQuery, ApiResponse<WebsitePublicPageDto>>
{
    public async Task<ApiResponse<WebsitePublicPageDto>> Handle(GetPublicPageQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Slug))
            return ApiResponse<WebsitePublicPageDto>.FailResponse("Slug is required.");

        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();

        var page = await connection.QuerySingleOrDefaultAsync<WebsitePageDto>(
            new CommandDefinition("""
                SELECT Id, Slug, Title, Description, MetaTitle, MetaDescription, OgImage,
                       Status, PublishedAt, UpdatedAt
                FROM WebsitePages
                WHERE TenantId = @TenantId AND Slug = @Slug AND Status = N'Published'
                """,
                new { TenantId = tenantId, Slug = request.Slug.Trim() },
                cancellationToken: cancellationToken));

        if (page is null)
            return ApiResponse<WebsitePublicPageDto>.FailResponse("Page not found.");

        var sections = (await connection.QueryAsync<WebsiteSectionDto>(
            new CommandDefinition(WebsitePublicSql.PageSections,
                new { TenantId = tenantId, PageId = page.Id },
                cancellationToken: cancellationToken))).ToList();

        return ApiResponse<WebsitePublicPageDto>.SuccessResponse(new WebsitePublicPageDto(page, sections));
    }
}

public class GetPublicFeaturesQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetPublicFeaturesQuery, ApiResponse<IReadOnlyList<WebsiteFeatureDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<WebsiteFeatureDto>>> Handle(
        GetPublicFeaturesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WebsiteFeatureDto>(
            new CommandDefinition(WebsitePublicSql.PublishedFeatures, new { TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();
        return ApiResponse<IReadOnlyList<WebsiteFeatureDto>>.SuccessResponse(rows);
    }
}

public class GetPublicLegalQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetPublicLegalQuery, ApiResponse<WebsiteLegalDto>>
{
    public async Task<ApiResponse<WebsiteLegalDto>> Handle(GetPublicLegalQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DocType))
            return ApiResponse<WebsiteLegalDto>.FailResponse("Document type is required.");

        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();
        var doc = await connection.QuerySingleOrDefaultAsync<WebsiteLegalDto>(
            new CommandDefinition("""
                SELECT Id, DocType, Title, Content, Version, Status, PublishedAt, UpdatedAt
                FROM WebsiteLegalDocuments
                WHERE TenantId = @TenantId AND Status = N'Published'
                  AND LOWER(DocType) = LOWER(@DocType)
                """,
                new { TenantId = tenantId, DocType = request.DocType.Trim() },
                cancellationToken: cancellationToken));

        if (doc is null)
            return ApiResponse<WebsiteLegalDto>.FailResponse("Legal document not found.");

        return ApiResponse<WebsiteLegalDto>.SuccessResponse(doc);
    }
}

public class GetPublicSettingsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetPublicSettingsQuery, ApiResponse<WebsiteSettingsDto>>
{
    public async Task<ApiResponse<WebsiteSettingsDto>> Handle(
        GetPublicSettingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();
        var settings = await connection.QuerySingleOrDefaultAsync<WebsiteSettingsDto>(
            new CommandDefinition(WebsitePublicSql.Settings, new { TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (settings is null)
            return ApiResponse<WebsiteSettingsDto>.FailResponse("Website settings not found.");

        return ApiResponse<WebsiteSettingsDto>.SuccessResponse(settings);
    }
}

internal static class WebsiteTenant
{
    public const int MarketingTenantId = 1;

    public static int Resolve(ITenantContext? tenantContext)
    {
        var id = tenantContext?.TenantId ?? 0;
        return id > 0 ? id : MarketingTenantId;
    }
}
