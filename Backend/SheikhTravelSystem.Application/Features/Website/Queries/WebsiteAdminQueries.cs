using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
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

public class GetWebsiteDashboardQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWebsiteDashboardQuery, ApiResponse<WebsiteDashboardDto>>
{
    public async Task<ApiResponse<WebsiteDashboardDto>> Handle(
        GetWebsiteDashboardQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();

        var dto = await connection.QuerySingleAsync<WebsiteDashboardDto>(
            new CommandDefinition("""
                SELECT
                    (SELECT COUNT(1) FROM WebsitePages WHERE TenantId = @TenantId) AS PageCount,
                    (SELECT COUNT(1) FROM WebsitePages WHERE TenantId = @TenantId AND Status = N'Published') AS PublishedPages,
                    (SELECT COUNT(1) FROM WebsitePages WHERE TenantId = @TenantId AND Status = N'Draft') AS DraftPages,
                    (SELECT COUNT(1) FROM WebsiteFeatures WHERE TenantId = @TenantId) AS FeatureCount,
                    (SELECT COUNT(1) FROM WebsiteContactRequests WHERE TenantId = @TenantId) AS ContactRequests,
                    (SELECT COUNT(1) FROM WebsiteDemoRequests WHERE TenantId = @TenantId) AS DemoRequests,
                    (SELECT COUNT(1) FROM WebsiteContactRequests WHERE TenantId = @TenantId AND Status = N'New') AS NewContactRequests,
                    (SELECT COUNT(1) FROM WebsiteDemoRequests WHERE TenantId = @TenantId AND Status = N'New') AS NewDemoRequests,
                    (SELECT COUNT(1) FROM WebsiteMedia WHERE TenantId = @TenantId) AS MediaCount,
                    (SELECT MAX(PublishedAt) FROM (
                        SELECT PublishedAt FROM WebsitePages WHERE TenantId = @TenantId AND PublishedAt IS NOT NULL
                        UNION ALL
                        SELECT PublishedAt FROM WebsiteLegalDocuments WHERE TenantId = @TenantId AND PublishedAt IS NOT NULL
                    ) x) AS LastPublishedAt
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<WebsiteDashboardDto>.SuccessResponse(dto);
    }
}

public class GetWebsiteSettingsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWebsiteSettingsQuery, ApiResponse<WebsiteSettingsDto>>
{
    public async Task<ApiResponse<WebsiteSettingsDto>> Handle(
        GetWebsiteSettingsQuery request, CancellationToken cancellationToken)
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

public class GetWebsitePagesQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWebsitePagesQuery, ApiResponse<IReadOnlyList<WebsitePageDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<WebsitePageDto>>> Handle(
        GetWebsitePagesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WebsitePageDto>(
            new CommandDefinition("""
                SELECT Id, Slug, Title, Description, MetaTitle, MetaDescription, OgImage,
                       Status, PublishedAt, UpdatedAt
                FROM WebsitePages
                WHERE TenantId = @TenantId
                ORDER BY Title
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<IReadOnlyList<WebsitePageDto>>.SuccessResponse(rows);
    }
}

public class GetWebsitePageSectionsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWebsitePageSectionsQuery, ApiResponse<IReadOnlyList<WebsiteSectionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<WebsiteSectionDto>>> Handle(
        GetWebsitePageSectionsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WebsiteSectionDto>(
            new CommandDefinition("""
                SELECT Id, PageId, SectionType, Title, Subtitle, Content, ImageUrl,
                       ButtonText, ButtonUrl, SecondaryButtonText, SecondaryButtonUrl,
                       DisplayOrder, IsActive, Status
                FROM WebsiteSections
                WHERE TenantId = @TenantId AND PageId = @PageId
                ORDER BY DisplayOrder, Id
                """, new { TenantId = tenantId, request.PageId }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<IReadOnlyList<WebsiteSectionDto>>.SuccessResponse(rows);
    }
}

public class GetWebsiteHomeSectionsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWebsiteHomeSectionsQuery, ApiResponse<IReadOnlyList<WebsiteSectionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<WebsiteSectionDto>>> Handle(
        GetWebsiteHomeSectionsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WebsiteSectionDto>(
            new CommandDefinition("""
                SELECT s.Id, s.PageId, s.SectionType, s.Title, s.Subtitle, s.Content, s.ImageUrl,
                       s.ButtonText, s.ButtonUrl, s.SecondaryButtonText, s.SecondaryButtonUrl,
                       s.DisplayOrder, s.IsActive, s.Status
                FROM WebsiteSections s
                INNER JOIN WebsitePages p ON p.Id = s.PageId AND p.TenantId = s.TenantId
                WHERE s.TenantId = @TenantId AND p.Slug = N'home'
                ORDER BY s.DisplayOrder, s.Id
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<IReadOnlyList<WebsiteSectionDto>>.SuccessResponse(rows);
    }
}

public class GetWebsiteFeaturesQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWebsiteFeaturesQuery, ApiResponse<IReadOnlyList<WebsiteFeatureDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<WebsiteFeatureDto>>> Handle(
        GetWebsiteFeaturesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WebsiteFeatureDto>(
            new CommandDefinition("""
                SELECT Id, Title, Description, IconKey, ImageUrl, LinkUrl, DisplayOrder, IsActive, Status
                FROM WebsiteFeatures
                WHERE TenantId = @TenantId
                ORDER BY DisplayOrder, Id
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<IReadOnlyList<WebsiteFeatureDto>>.SuccessResponse(rows);
    }
}

public class GetWebsiteLegalQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWebsiteLegalQuery, ApiResponse<IReadOnlyList<WebsiteLegalDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<WebsiteLegalDto>>> Handle(
        GetWebsiteLegalQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();

        var sql = """
            SELECT Id, DocType, Title, Content, Version, Status, PublishedAt, UpdatedAt
            FROM WebsiteLegalDocuments
            WHERE TenantId = @TenantId
            """;
        if (!string.IsNullOrWhiteSpace(request.DocType))
            sql += " AND LOWER(DocType) = LOWER(@DocType)";
        sql += " ORDER BY DocType";

        var rows = (await connection.QueryAsync<WebsiteLegalDto>(
            new CommandDefinition(sql,
                new { TenantId = tenantId, DocType = request.DocType?.Trim() },
                cancellationToken: cancellationToken))).ToList();

        return ApiResponse<IReadOnlyList<WebsiteLegalDto>>.SuccessResponse(rows);
    }
}

public class GetWebsiteMediaQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWebsiteMediaQuery, ApiResponse<IReadOnlyList<WebsiteMediaDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<WebsiteMediaDto>>> Handle(
        GetWebsiteMediaQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WebsiteMediaDto>(
            new CommandDefinition("""
                SELECT Id, FileName, FileUrl, FileType, AltText, SizeBytes, CreatedAt
                FROM WebsiteMedia
                WHERE TenantId = @TenantId
                ORDER BY CreatedAt DESC, Id DESC
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<IReadOnlyList<WebsiteMediaDto>>.SuccessResponse(rows);
    }
}

public class GetWebsiteContactRequestsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWebsiteContactRequestsQuery, ApiResponse<PagedResult<WebsiteContactRequestDto>>>
{
    public async Task<ApiResponse<PagedResult<WebsiteContactRequestDto>>> Handle(
        GetWebsiteContactRequestsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, 100);
        var offset = (page - 1) * pageSize;
        var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim();

        using var connection = dbFactory.CreateConnection();

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("""
                SELECT COUNT(1) FROM WebsiteContactRequests
                WHERE TenantId = @TenantId AND (@Status IS NULL OR Status = @Status)
                """, new { TenantId = tenantId, Status = status }, cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<WebsiteContactRequestDto>(
            new CommandDefinition("""
                SELECT Id, FirstName, LastName, Company, Email, Phone, Country, FleetSize,
                       InterestedIn, Message, Status, CreatedAt
                FROM WebsiteContactRequests
                WHERE TenantId = @TenantId AND (@Status IS NULL OR Status = @Status)
                ORDER BY CreatedAt DESC, Id DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                """,
                new { TenantId = tenantId, Status = status, Offset = offset, PageSize = pageSize },
                cancellationToken: cancellationToken))).ToList();

        return ApiResponse<PagedResult<WebsiteContactRequestDto>>.SuccessResponse(new PagedResult<WebsiteContactRequestDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }
}

public class GetWebsiteDemoRequestsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWebsiteDemoRequestsQuery, ApiResponse<PagedResult<WebsiteDemoRequestDto>>>
{
    public async Task<ApiResponse<PagedResult<WebsiteDemoRequestDto>>> Handle(
        GetWebsiteDemoRequestsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, 100);
        var offset = (page - 1) * pageSize;
        var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim();

        using var connection = dbFactory.CreateConnection();

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("""
                SELECT COUNT(1) FROM WebsiteDemoRequests
                WHERE TenantId = @TenantId AND (@Status IS NULL OR Status = @Status)
                """, new { TenantId = tenantId, Status = status }, cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<WebsiteDemoRequestDto>(
            new CommandDefinition("""
                SELECT Id, Name, Company, Email, Phone, Country, VehicleCount, CurrentGpsProvider,
                       InterestedProduct, Message, Status, CreatedAt
                FROM WebsiteDemoRequests
                WHERE TenantId = @TenantId AND (@Status IS NULL OR Status = @Status)
                ORDER BY CreatedAt DESC, Id DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                """,
                new { TenantId = tenantId, Status = status, Offset = offset, PageSize = pageSize },
                cancellationToken: cancellationToken))).ToList();

        return ApiResponse<PagedResult<WebsiteDemoRequestDto>>.SuccessResponse(new PagedResult<WebsiteDemoRequestDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }
}

public class GetWebsiteContactRequestByIdQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWebsiteContactRequestByIdQuery, ApiResponse<WebsiteContactRequestDto>>
{
    public async Task<ApiResponse<WebsiteContactRequestDto>> Handle(
        GetWebsiteContactRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<WebsiteContactRequestDto>(
            new CommandDefinition("""
                SELECT Id, FirstName, LastName, Company, Email, Phone, Country, FleetSize,
                       InterestedIn, Message, Status, CreatedAt
                FROM WebsiteContactRequests
                WHERE Id = @Id AND TenantId = @TenantId
                """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row is null)
            return ApiResponse<WebsiteContactRequestDto>.FailResponse("Contact request not found.");

        return ApiResponse<WebsiteContactRequestDto>.SuccessResponse(row);
    }
}

public class GetWebsiteDemoRequestByIdQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetWebsiteDemoRequestByIdQuery, ApiResponse<WebsiteDemoRequestDto>>
{
    public async Task<ApiResponse<WebsiteDemoRequestDto>> Handle(
        GetWebsiteDemoRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<WebsiteDemoRequestDto>(
            new CommandDefinition("""
                SELECT Id, Name, Company, Email, Phone, Country, VehicleCount, CurrentGpsProvider,
                       InterestedProduct, Message, Status, CreatedAt
                FROM WebsiteDemoRequests
                WHERE Id = @Id AND TenantId = @TenantId
                """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row is null)
            return ApiResponse<WebsiteDemoRequestDto>.FailResponse("Demo request not found.");

        return ApiResponse<WebsiteDemoRequestDto>.SuccessResponse(row);
    }
}
