using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Website.Commands;
using SheikhTravelSystem.Application.Features.Website.DTOs;
using SheikhTravelSystem.Application.Features.Website.Queries;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class WebsiteRepository(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IFileStorageService fileStorage) : IWebsiteRepository
{
    private int TenantId => WebsiteTenant.Resolve(tenantContext);

    private const string SettingsSql = """
        SELECT Id, SiteName, LogoUrl, FaviconUrl, SupportEmail, SalesEmail, PrivacyEmail,
               Phone, Address, LinkedInUrl, FacebookUrl, XUrl, YouTubeUrl,
               DefaultMetaTitle, DefaultMetaDescription, AnalyticsId
        FROM WebsiteSettings
        WHERE TenantId = @TenantId
        """;

    private const string PageSelect = """
        SELECT Id, Slug, Title, Description, MetaTitle, MetaDescription, OgImage,
               Status, PublishedAt, UpdatedAt
        FROM WebsitePages
        """;

    private const string SectionSelect = """
        SELECT Id, PageId, SectionType, Title, Subtitle, Content, ImageUrl,
               ButtonText, ButtonUrl, SecondaryButtonText, SecondaryButtonUrl,
               DisplayOrder, IsActive, Status
        FROM WebsiteSections
        """;

    private const string FeatureSelect = """
        SELECT Id, Title, Description, IconKey, ImageUrl, LinkUrl, DisplayOrder, IsActive, Status
        FROM WebsiteFeatures
        """;

    private const string LegalSelect = """
        SELECT Id, DocType, Title, Content, Version, Status, PublishedAt, UpdatedAt
        FROM WebsiteLegalDocuments
        """;

    public async Task<ApiResponse<WebsiteDashboardDto>> GetDashboardAsync(GetWebsiteDashboardQuery request, CancellationToken cancellationToken = default)
    {
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
                """, new { TenantId }, cancellationToken: cancellationToken));
        return ApiResponse<WebsiteDashboardDto>.SuccessResponse(dto);
    }

    public async Task<ApiResponse<WebsiteSettingsDto>> GetSettingsAsync(GetWebsiteSettingsQuery request, CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        return settings is null
            ? ApiResponse<WebsiteSettingsDto>.FailResponse("Website settings not found.")
            : ApiResponse<WebsiteSettingsDto>.SuccessResponse(settings);
    }

    public async Task<ApiResponse<WebsiteSettingsDto>> UpdateSettingsAsync(UpdateWebsiteSettingsCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteSettings SET
                SiteName = @SiteName, LogoUrl = @LogoUrl, FaviconUrl = @FaviconUrl,
                SupportEmail = @SupportEmail, SalesEmail = @SalesEmail, PrivacyEmail = @PrivacyEmail,
                Phone = @Phone, Address = @Address,
                LinkedInUrl = @LinkedInUrl, FacebookUrl = @FacebookUrl, XUrl = @XUrl, YouTubeUrl = @YouTubeUrl,
                DefaultMetaTitle = @DefaultMetaTitle, DefaultMetaDescription = @DefaultMetaDescription,
                AnalyticsId = @AnalyticsId, UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
            """, new
        {
            TenantId,
            request.SiteName, request.LogoUrl, request.FaviconUrl,
            request.SupportEmail, request.SalesEmail, request.PrivacyEmail,
            request.Phone, request.Address,
            request.LinkedInUrl, request.FacebookUrl, request.XUrl, request.YouTubeUrl,
            request.DefaultMetaTitle, request.DefaultMetaDescription, request.AnalyticsId
        }, cancellationToken: cancellationToken));

        if (updated == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO WebsiteSettings (TenantId, SiteName, LogoUrl, FaviconUrl, SupportEmail, SalesEmail, PrivacyEmail,
                    Phone, Address, LinkedInUrl, FacebookUrl, XUrl, YouTubeUrl, DefaultMetaTitle, DefaultMetaDescription, AnalyticsId)
                VALUES (@TenantId, @SiteName, @LogoUrl, @FaviconUrl, @SupportEmail, @SalesEmail, @PrivacyEmail,
                    @Phone, @Address, @LinkedInUrl, @FacebookUrl, @XUrl, @YouTubeUrl, @DefaultMetaTitle, @DefaultMetaDescription, @AnalyticsId)
                """, new
            {
                TenantId,
                request.SiteName, request.LogoUrl, request.FaviconUrl,
                request.SupportEmail, request.SalesEmail, request.PrivacyEmail,
                request.Phone, request.Address,
                request.LinkedInUrl, request.FacebookUrl, request.XUrl, request.YouTubeUrl,
                request.DefaultMetaTitle, request.DefaultMetaDescription, request.AnalyticsId
            }, cancellationToken: cancellationToken));
        }

        var settings = await connection.QuerySingleAsync<WebsiteSettingsDto>(
            new CommandDefinition(SettingsSql, new { TenantId }, cancellationToken: cancellationToken));
        return ApiResponse<WebsiteSettingsDto>.SuccessResponse(settings, "Settings updated.");
    }

    public async Task<ApiResponse<IReadOnlyList<WebsitePageDto>>> GetPagesAsync(GetWebsitePagesQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WebsitePageDto>(new CommandDefinition(
            PageSelect + " WHERE TenantId = @TenantId ORDER BY Title",
            new { TenantId }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<IReadOnlyList<WebsitePageDto>>.SuccessResponse(rows);
    }

    public async Task<ApiResponse<WebsitePageDto>> CreatePageAsync(CreateWebsitePageCommand request, CancellationToken cancellationToken = default)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        using var connection = dbFactory.CreateConnection();
        var clash = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM WebsitePages WHERE TenantId = @TenantId AND Slug = @Slug) THEN 1 ELSE 0 END",
            new { TenantId, Slug = slug }, cancellationToken: cancellationToken));
        if (clash)
            return ApiResponse<WebsitePageDto>.FailResponse("A page with this slug already exists.");

        var status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status.Trim();
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO WebsitePages (TenantId, Slug, Title, Description, MetaTitle, MetaDescription, OgImage, Status, PublishedAt)
            OUTPUT INSERTED.Id
            VALUES (@TenantId, @Slug, @Title, @Description, @MetaTitle, @MetaDescription, @OgImage, @Status,
                    CASE WHEN @Status = N'Published' THEN SYSUTCDATETIME() ELSE NULL END)
            """, new
        {
            TenantId, Slug = slug, request.Title, request.Description,
            request.MetaTitle, request.MetaDescription, request.OgImage, Status = status
        }, cancellationToken: cancellationToken));

        var page = await connection.QuerySingleAsync<WebsitePageDto>(new CommandDefinition(
            PageSelect + " WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = id, TenantId }, cancellationToken: cancellationToken));
        return ApiResponse<WebsitePageDto>.SuccessResponse(page, "Page created.");
    }

    public async Task<ApiResponse<WebsitePageDto>> UpdatePageAsync(UpdateWebsitePageCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM WebsitePages WHERE Id = @Id AND TenantId = @TenantId) THEN 1 ELSE 0 END",
            new { request.Id, TenantId }, cancellationToken: cancellationToken));
        if (!exists)
            return ApiResponse<WebsitePageDto>.FailResponse("Page not found.");

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsitePages SET
                Title = @Title, Description = @Description, MetaTitle = @MetaTitle,
                MetaDescription = @MetaDescription, OgImage = @OgImage,
                Status = COALESCE(@Status, Status),
                PublishedAt = CASE
                    WHEN @Status = N'Published' AND (Status <> N'Published' OR PublishedAt IS NULL) THEN SYSUTCDATETIME()
                    WHEN @Status = N'Draft' THEN NULL
                    ELSE PublishedAt END,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            request.Id, TenantId, request.Title, request.Description,
            request.MetaTitle, request.MetaDescription, request.OgImage,
            Status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim()
        }, cancellationToken: cancellationToken));

        var page = await connection.QuerySingleAsync<WebsitePageDto>(new CommandDefinition(
            PageSelect + " WHERE Id = @Id AND TenantId = @TenantId",
            new { request.Id, TenantId }, cancellationToken: cancellationToken));
        return ApiResponse<WebsitePageDto>.SuccessResponse(page, "Page updated.");
    }

    public async Task<ApiResponse<WebsitePageDto>> PublishPageAsync(PublishWebsitePageCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsitePages SET
                Status = N'Published',
                PublishedAt = COALESCE(PublishedAt, SYSUTCDATETIME()),
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId }, cancellationToken: cancellationToken));
        if (updated == 0)
            return ApiResponse<WebsitePageDto>.FailResponse("Page not found.");

        var page = await connection.QuerySingleAsync<WebsitePageDto>(new CommandDefinition(
            PageSelect + " WHERE Id = @Id AND TenantId = @TenantId",
            new { request.Id, TenantId }, cancellationToken: cancellationToken));
        return ApiResponse<WebsitePageDto>.SuccessResponse(page, "Page published.");
    }

    public async Task<ApiResponse<IReadOnlyList<WebsiteSectionDto>>> GetPageSectionsAsync(GetWebsitePageSectionsQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WebsiteSectionDto>(new CommandDefinition(
            SectionSelect + " WHERE TenantId = @TenantId AND PageId = @PageId ORDER BY DisplayOrder, Id",
            new { TenantId, request.PageId }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<IReadOnlyList<WebsiteSectionDto>>.SuccessResponse(rows);
    }

    public async Task<ApiResponse<IReadOnlyList<WebsiteSectionDto>>> GetHomeSectionsAsync(GetWebsiteHomeSectionsQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WebsiteSectionDto>(new CommandDefinition("""
            SELECT s.Id, s.PageId, s.SectionType, s.Title, s.Subtitle, s.Content, s.ImageUrl,
                   s.ButtonText, s.ButtonUrl, s.SecondaryButtonText, s.SecondaryButtonUrl,
                   s.DisplayOrder, s.IsActive, s.Status
            FROM WebsiteSections s
            INNER JOIN WebsitePages p ON p.Id = s.PageId AND p.TenantId = s.TenantId
            WHERE s.TenantId = @TenantId AND p.Slug = N'home'
            ORDER BY s.DisplayOrder, s.Id
            """, new { TenantId }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<IReadOnlyList<WebsiteSectionDto>>.SuccessResponse(rows);
    }

    public async Task<ApiResponse<WebsiteSectionDto>> UpsertSectionAsync(UpsertWebsiteSectionCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var pageExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM WebsitePages WHERE Id = @PageId AND TenantId = @TenantId) THEN 1 ELSE 0 END",
            new { request.PageId, TenantId }, cancellationToken: cancellationToken));
        if (!pageExists)
            return ApiResponse<WebsiteSectionDto>.FailResponse("Page not found.");

        int id;
        if (request.Id is > 0)
        {
            var updated = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE WebsiteSections SET
                    PageId = @PageId, SectionType = @SectionType, Title = @Title, Subtitle = @Subtitle,
                    Content = @Content, ImageUrl = @ImageUrl, ButtonText = @ButtonText, ButtonUrl = @ButtonUrl,
                    SecondaryButtonText = @SecondaryButtonText, SecondaryButtonUrl = @SecondaryButtonUrl,
                    DisplayOrder = @DisplayOrder, IsActive = @IsActive, Status = @Status,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id AND TenantId = @TenantId
                """, new
            {
                request.Id, TenantId, request.PageId, request.SectionType, request.Title, request.Subtitle,
                request.Content, request.ImageUrl, request.ButtonText, request.ButtonUrl,
                request.SecondaryButtonText, request.SecondaryButtonUrl, request.DisplayOrder,
                request.IsActive, request.Status
            }, cancellationToken: cancellationToken));
            if (updated == 0)
                return ApiResponse<WebsiteSectionDto>.FailResponse("Section not found.");
            id = request.Id.Value;
        }
        else
        {
            id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO WebsiteSections (TenantId, PageId, SectionType, Title, Subtitle, Content, ImageUrl,
                    ButtonText, ButtonUrl, SecondaryButtonText, SecondaryButtonUrl, DisplayOrder, IsActive, Status)
                OUTPUT INSERTED.Id
                VALUES (@TenantId, @PageId, @SectionType, @Title, @Subtitle, @Content, @ImageUrl,
                    @ButtonText, @ButtonUrl, @SecondaryButtonText, @SecondaryButtonUrl, @DisplayOrder, @IsActive, @Status)
                """, new
            {
                TenantId, request.PageId, request.SectionType, request.Title, request.Subtitle,
                request.Content, request.ImageUrl, request.ButtonText, request.ButtonUrl,
                request.SecondaryButtonText, request.SecondaryButtonUrl, request.DisplayOrder,
                request.IsActive, request.Status
            }, cancellationToken: cancellationToken));
        }

        var section = await LoadSectionAsync(connection, id, cancellationToken);
        return ApiResponse<WebsiteSectionDto>.SuccessResponse(section!, "Section saved.");
    }

    public async Task<ApiResponse<bool>> DeleteSectionAsync(DeleteWebsiteSectionCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM WebsiteSections WHERE Id = @Id AND TenantId = @TenantId",
            new { request.Id, TenantId }, cancellationToken: cancellationToken));
        return deleted == 0
            ? ApiResponse<bool>.FailResponse("Section not found.")
            : ApiResponse<bool>.SuccessResponse(true, "Section deleted.");
    }

    public async Task<ApiResponse<WebsiteSectionDto>> PublishSectionAsync(PublishWebsiteSectionCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteSections SET Status = N'Published', IsActive = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId }, cancellationToken: cancellationToken));
        if (updated == 0)
            return ApiResponse<WebsiteSectionDto>.FailResponse("Section not found.");
        var section = await LoadSectionAsync(connection, request.Id, cancellationToken);
        return ApiResponse<WebsiteSectionDto>.SuccessResponse(section!, "Section published.");
    }

    public async Task<ApiResponse<IReadOnlyList<WebsiteFeatureDto>>> GetFeaturesAsync(GetWebsiteFeaturesQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WebsiteFeatureDto>(new CommandDefinition(
            FeatureSelect + " WHERE TenantId = @TenantId ORDER BY DisplayOrder, Id",
            new { TenantId }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<IReadOnlyList<WebsiteFeatureDto>>.SuccessResponse(rows);
    }

    public async Task<ApiResponse<WebsiteFeatureDto>> UpsertFeatureAsync(UpsertWebsiteFeatureCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        int id;
        if (request.Id is > 0)
        {
            var updated = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE WebsiteFeatures SET
                    Title = @Title, Description = @Description, IconKey = @IconKey, ImageUrl = @ImageUrl,
                    LinkUrl = @LinkUrl, DisplayOrder = @DisplayOrder, IsActive = @IsActive, Status = @Status,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id AND TenantId = @TenantId
                """, new
            {
                request.Id, TenantId, request.Title, request.Description, request.IconKey, request.ImageUrl,
                request.LinkUrl, request.DisplayOrder, request.IsActive, request.Status
            }, cancellationToken: cancellationToken));
            if (updated == 0)
                return ApiResponse<WebsiteFeatureDto>.FailResponse("Feature not found.");
            id = request.Id.Value;
        }
        else
        {
            id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO WebsiteFeatures (TenantId, Title, Description, IconKey, ImageUrl, LinkUrl, DisplayOrder, IsActive, Status)
                OUTPUT INSERTED.Id
                VALUES (@TenantId, @Title, @Description, @IconKey, @ImageUrl, @LinkUrl, @DisplayOrder, @IsActive, @Status)
                """, new
            {
                TenantId, request.Title, request.Description, request.IconKey, request.ImageUrl,
                request.LinkUrl, request.DisplayOrder, request.IsActive, request.Status
            }, cancellationToken: cancellationToken));
        }

        var feature = await LoadFeatureAsync(connection, id, cancellationToken);
        return ApiResponse<WebsiteFeatureDto>.SuccessResponse(feature!, "Feature saved.");
    }

    public async Task<ApiResponse<bool>> DeleteFeatureAsync(DeleteWebsiteFeatureCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM WebsiteFeatures WHERE Id = @Id AND TenantId = @TenantId",
            new { request.Id, TenantId }, cancellationToken: cancellationToken));
        return deleted == 0
            ? ApiResponse<bool>.FailResponse("Feature not found.")
            : ApiResponse<bool>.SuccessResponse(true, "Feature deleted.");
    }

    public async Task<ApiResponse<WebsiteFeatureDto>> PublishFeatureAsync(PublishWebsiteFeatureCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteFeatures SET Status = N'Published', IsActive = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId }, cancellationToken: cancellationToken));
        if (updated == 0)
            return ApiResponse<WebsiteFeatureDto>.FailResponse("Feature not found.");
        var feature = await LoadFeatureAsync(connection, request.Id, cancellationToken);
        return ApiResponse<WebsiteFeatureDto>.SuccessResponse(feature!, "Feature published.");
    }

    public async Task<ApiResponse<IReadOnlyList<WebsiteLegalDto>>> GetLegalAsync(GetWebsiteLegalQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = LegalSelect + " WHERE TenantId = @TenantId";
        if (!string.IsNullOrWhiteSpace(request.DocType))
            sql += " AND LOWER(DocType) = LOWER(@DocType)";
        sql += " ORDER BY DocType";
        var rows = (await connection.QueryAsync<WebsiteLegalDto>(new CommandDefinition(sql,
            new { TenantId, DocType = request.DocType?.Trim() }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<IReadOnlyList<WebsiteLegalDto>>.SuccessResponse(rows);
    }

    public async Task<ApiResponse<WebsiteLegalDto>> UpdateLegalAsync(UpdateWebsiteLegalCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteLegalDocuments SET
                Title = @Title, Content = @Content, Version = @Version, UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND LOWER(DocType) = LOWER(@DocType)
            """, new { TenantId, request.DocType, request.Title, request.Content, request.Version },
            cancellationToken: cancellationToken));

        if (updated == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO WebsiteLegalDocuments (TenantId, DocType, Title, Content, Version, Status)
                VALUES (@TenantId, @DocType, @Title, @Content, @Version, N'Draft')
                """, new { TenantId, request.DocType, request.Title, request.Content, request.Version },
                cancellationToken: cancellationToken));
        }

        var doc = await connection.QuerySingleAsync<WebsiteLegalDto>(new CommandDefinition(
            LegalSelect + " WHERE TenantId = @TenantId AND LOWER(DocType) = LOWER(@DocType)",
            new { TenantId, request.DocType }, cancellationToken: cancellationToken));
        return ApiResponse<WebsiteLegalDto>.SuccessResponse(doc, "Legal document updated.");
    }

    public async Task<ApiResponse<WebsiteLegalDto>> PublishLegalAsync(PublishWebsiteLegalCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteLegalDocuments SET
                Status = N'Published',
                PublishedAt = COALESCE(PublishedAt, SYSUTCDATETIME()),
                UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND LOWER(DocType) = LOWER(@DocType)
            """, new { TenantId, request.DocType }, cancellationToken: cancellationToken));
        if (updated == 0)
            return ApiResponse<WebsiteLegalDto>.FailResponse("Legal document not found.");

        var doc = await connection.QuerySingleAsync<WebsiteLegalDto>(new CommandDefinition(
            LegalSelect + " WHERE TenantId = @TenantId AND LOWER(DocType) = LOWER(@DocType)",
            new { TenantId, request.DocType }, cancellationToken: cancellationToken));
        return ApiResponse<WebsiteLegalDto>.SuccessResponse(doc, "Legal document published.");
    }

    public async Task<ApiResponse<IReadOnlyList<WebsiteMediaDto>>> GetMediaAsync(GetWebsiteMediaQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WebsiteMediaDto>(new CommandDefinition("""
            SELECT Id, FileName, FileUrl, FileType, AltText, SizeBytes, CreatedAt
            FROM WebsiteMedia
            WHERE TenantId = @TenantId
            ORDER BY CreatedAt DESC, Id DESC
            """, new { TenantId }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<IReadOnlyList<WebsiteMediaDto>>.SuccessResponse(rows);
    }

    public async Task<ApiResponse<WebsiteMediaDto>> UploadMediaAsync(UploadWebsiteMediaCommand request, CancellationToken cancellationToken = default)
    {
        var stored = await fileStorage.SaveAsync(
            request.FileStream, request.FileName, request.ContentType, "website", cancellationToken);

        using var connection = dbFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO WebsiteMedia (TenantId, FileName, FileUrl, StorageKey, FileType, AltText, SizeBytes)
            OUTPUT INSERTED.Id
            VALUES (@TenantId, @FileName, @FileUrl, @StorageKey, @FileType, @AltText, @SizeBytes)
            """, new
        {
            TenantId,
            FileName = stored.FileName,
            FileUrl = stored.ReadUrl,
            StorageKey = stored.StorageKey,
            FileType = request.ContentType,
            request.AltText,
            SizeBytes = request.SizeBytes ?? stored.SizeBytes
        }, cancellationToken: cancellationToken));

        var media = await connection.QuerySingleAsync<WebsiteMediaDto>(new CommandDefinition("""
            SELECT Id, FileName, FileUrl, FileType, AltText, SizeBytes, CreatedAt
            FROM WebsiteMedia WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = id, TenantId }, cancellationToken: cancellationToken));
        return ApiResponse<WebsiteMediaDto>.SuccessResponse(media, "Media uploaded.");
    }

    public async Task<ApiResponse<bool>> DeleteMediaAsync(DeleteWebsiteMediaCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(string? StorageKey, string FileUrl)>(
            new CommandDefinition(
                "SELECT StorageKey, FileUrl FROM WebsiteMedia WHERE Id = @Id AND TenantId = @TenantId",
                new { request.Id, TenantId }, cancellationToken: cancellationToken));

        if (row.FileUrl is null && row.StorageKey is null)
            return ApiResponse<bool>.FailResponse("Media not found.");

        if (!string.IsNullOrWhiteSpace(row.FileUrl))
        {
            var usage = await connection.QuerySingleAsync<(int Features, int Sections, int Pages)>(new CommandDefinition("""
                SELECT
                  (SELECT COUNT(1) FROM WebsiteFeatures WHERE TenantId = @TenantId AND ImageUrl = @Url) AS Features,
                  (SELECT COUNT(1) FROM WebsiteSections WHERE TenantId = @TenantId AND ImageUrl = @Url) AS Sections,
                  (SELECT COUNT(1) FROM WebsitePages WHERE TenantId = @TenantId AND OgImage = @Url) AS Pages
                """, new { TenantId, Url = row.FileUrl }, cancellationToken: cancellationToken));

            var total = usage.Features + usage.Sections + usage.Pages;
            if (total > 0)
            {
                return ApiResponse<bool>.FailResponse(
                    $"Media is in use by {usage.Features} feature(s), {usage.Sections} section(s), and {usage.Pages} page(s). Remove those references first.");
            }
        }

        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM WebsiteMedia WHERE Id = @Id AND TenantId = @TenantId",
            new { request.Id, TenantId }, cancellationToken: cancellationToken));
        if (deleted == 0)
            return ApiResponse<bool>.FailResponse("Media not found.");

        var key = !string.IsNullOrWhiteSpace(row.StorageKey) ? row.StorageKey : row.FileUrl;
        if (!string.IsNullOrWhiteSpace(key))
        {
            try { await fileStorage.DeleteAsync(key, cancellationToken); }
            catch { /* best-effort storage cleanup */ }
        }

        return ApiResponse<bool>.SuccessResponse(true, "Media deleted.");
    }

    public async Task<ApiResponse<PagedResult<WebsiteContactRequestDto>>> GetContactRequestsAsync(GetWebsiteContactRequestsQuery request, CancellationToken cancellationToken = default)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, 100);
        var offset = (page - 1) * pageSize;
        var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim();
        using var connection = dbFactory.CreateConnection();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM WebsiteContactRequests
            WHERE TenantId = @TenantId AND (@Status IS NULL OR Status = @Status)
            """, new { TenantId, Status = status }, cancellationToken: cancellationToken));
        var items = (await connection.QueryAsync<WebsiteContactRequestDto>(new CommandDefinition("""
            SELECT Id, FirstName, LastName, Company, Email, Phone, Country, FleetSize,
                   InterestedIn, Message, Status, CreatedAt,
                   Source, FleetType, MainChallenge, CurrentSystem,
                   WhatsAppConversationId, WhatsAppAccountId
            FROM WebsiteContactRequests
            WHERE TenantId = @TenantId AND (@Status IS NULL OR Status = @Status)
            ORDER BY CreatedAt DESC, Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, new { TenantId, Status = status, Offset = offset, PageSize = pageSize }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<PagedResult<WebsiteContactRequestDto>>.SuccessResponse(new PagedResult<WebsiteContactRequestDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        });
    }

    public async Task<ApiResponse<WebsiteContactRequestDto>> GetContactRequestByIdAsync(GetWebsiteContactRequestByIdQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<WebsiteContactRequestDto>(new CommandDefinition("""
            SELECT Id, FirstName, LastName, Company, Email, Phone, Country, FleetSize,
                   InterestedIn, Message, Status, CreatedAt,
                   Source, FleetType, MainChallenge, CurrentSystem,
                   WhatsAppConversationId, WhatsAppAccountId
            FROM WebsiteContactRequests WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId }, cancellationToken: cancellationToken));
        return row is null
            ? ApiResponse<WebsiteContactRequestDto>.FailResponse("Contact request not found.")
            : ApiResponse<WebsiteContactRequestDto>.SuccessResponse(row);
    }

    public async Task<ApiResponse<WebsiteContactRequestDto>> UpdateContactRequestStatusAsync(UpdateContactRequestStatusCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteContactRequests SET Status = @Status, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId, request.Status }, cancellationToken: cancellationToken));
        if (updated == 0)
            return ApiResponse<WebsiteContactRequestDto>.FailResponse("Contact request not found.");
        var result = await GetContactRequestByIdAsync(new GetWebsiteContactRequestByIdQuery(request.Id), cancellationToken);
        return result.Success
            ? ApiResponse<WebsiteContactRequestDto>.SuccessResponse(result.Data!, "Status updated.")
            : result;
    }

    public async Task<ApiResponse<PagedResult<WebsiteDemoRequestDto>>> GetDemoRequestsAsync(GetWebsiteDemoRequestsQuery request, CancellationToken cancellationToken = default)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, 100);
        var offset = (page - 1) * pageSize;
        var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim();
        using var connection = dbFactory.CreateConnection();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM WebsiteDemoRequests
            WHERE TenantId = @TenantId AND (@Status IS NULL OR Status = @Status)
            """, new { TenantId, Status = status }, cancellationToken: cancellationToken));
        var items = (await connection.QueryAsync<WebsiteDemoRequestDto>(new CommandDefinition("""
            SELECT Id, Name, Company, Email, Phone, Country, VehicleCount, CurrentGpsProvider,
                   InterestedProduct, Message, Status, CreatedAt
            FROM WebsiteDemoRequests
            WHERE TenantId = @TenantId AND (@Status IS NULL OR Status = @Status)
            ORDER BY CreatedAt DESC, Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, new { TenantId, Status = status, Offset = offset, PageSize = pageSize }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<PagedResult<WebsiteDemoRequestDto>>.SuccessResponse(new PagedResult<WebsiteDemoRequestDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        });
    }

    public async Task<ApiResponse<WebsiteDemoRequestDto>> GetDemoRequestByIdAsync(GetWebsiteDemoRequestByIdQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<WebsiteDemoRequestDto>(new CommandDefinition("""
            SELECT Id, Name, Company, Email, Phone, Country, VehicleCount, CurrentGpsProvider,
                   InterestedProduct, Message, Status, CreatedAt
            FROM WebsiteDemoRequests WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId }, cancellationToken: cancellationToken));
        return row is null
            ? ApiResponse<WebsiteDemoRequestDto>.FailResponse("Demo request not found.")
            : ApiResponse<WebsiteDemoRequestDto>.SuccessResponse(row);
    }

    public async Task<ApiResponse<WebsiteDemoRequestDto>> UpdateDemoRequestStatusAsync(UpdateDemoRequestStatusCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteDemoRequests SET Status = @Status, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId, request.Status }, cancellationToken: cancellationToken));
        if (updated == 0)
            return ApiResponse<WebsiteDemoRequestDto>.FailResponse("Demo request not found.");
        var result = await GetDemoRequestByIdAsync(new GetWebsiteDemoRequestByIdQuery(request.Id), cancellationToken);
        return result.Success
            ? ApiResponse<WebsiteDemoRequestDto>.SuccessResponse(result.Data!, "Status updated.")
            : result;
    }

    public async Task<ApiResponse<WebsitePublicHomeDto>> GetPublicHomeAsync(GetPublicHomeQuery request, CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        if (settings is null)
            return ApiResponse<WebsitePublicHomeDto>.FailResponse("Website settings not found.");

        using var connection = dbFactory.CreateConnection();
        var sections = (await connection.QueryAsync<WebsiteSectionDto>(new CommandDefinition("""
            SELECT s.Id, s.PageId, s.SectionType, s.Title, s.Subtitle, s.Content, s.ImageUrl,
                   s.ButtonText, s.ButtonUrl, s.SecondaryButtonText, s.SecondaryButtonUrl,
                   s.DisplayOrder, s.IsActive, s.Status
            FROM WebsiteSections s
            INNER JOIN WebsitePages p ON p.Id = s.PageId AND p.TenantId = s.TenantId
            WHERE s.TenantId = @TenantId AND p.Slug = N'home'
              AND s.IsActive = 1 AND s.Status = N'Published'
            ORDER BY s.DisplayOrder, s.Id
            """, new { TenantId }, cancellationToken: cancellationToken))).ToList();

        var features = (await connection.QueryAsync<WebsiteFeatureDto>(new CommandDefinition(
            FeatureSelect + " " + Sql.WebsitePublicSql.PublishedFeatures,
            new { TenantId }, cancellationToken: cancellationToken))).ToList();

        return ApiResponse<WebsitePublicHomeDto>.SuccessResponse(new WebsitePublicHomeDto(settings, sections, features));
    }

    public async Task<ApiResponse<WebsitePublicPageDto>> GetPublicPageAsync(GetPublicPageQuery request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Slug))
            return ApiResponse<WebsitePublicPageDto>.FailResponse("Slug is required.");

        using var connection = dbFactory.CreateConnection();
        var page = await connection.QuerySingleOrDefaultAsync<WebsitePageDto>(new CommandDefinition(
            PageSelect + " WHERE TenantId = @TenantId AND Slug = @Slug AND Status = N'Published'",
            new { TenantId, Slug = request.Slug.Trim() }, cancellationToken: cancellationToken));
        if (page is null)
            return ApiResponse<WebsitePublicPageDto>.FailResponse("Page not found.");

        var sections = (await connection.QueryAsync<WebsiteSectionDto>(new CommandDefinition(
            SectionSelect + " " + Sql.WebsitePublicSql.PageSections,
            new { TenantId, PageId = page.Id }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<WebsitePublicPageDto>.SuccessResponse(new WebsitePublicPageDto(page, sections));
    }

    public async Task<ApiResponse<IReadOnlyList<WebsiteFeatureDto>>> GetPublicFeaturesAsync(GetPublicFeaturesQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WebsiteFeatureDto>(new CommandDefinition(
            FeatureSelect + " " + Sql.WebsitePublicSql.PublishedFeatures,
            new { TenantId }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<IReadOnlyList<WebsiteFeatureDto>>.SuccessResponse(rows);
    }

    public async Task<ApiResponse<WebsiteLegalDto>> GetPublicLegalAsync(GetPublicLegalQuery request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DocType))
            return ApiResponse<WebsiteLegalDto>.FailResponse("Document type is required.");

        using var connection = dbFactory.CreateConnection();
        var doc = await connection.QuerySingleOrDefaultAsync<WebsiteLegalDto>(new CommandDefinition("""
            SELECT Id, DocType, Title, Content, Version, Status, PublishedAt, UpdatedAt
            FROM WebsiteLegalDocuments
            WHERE TenantId = @TenantId AND Status = N'Published' AND LOWER(DocType) = LOWER(@DocType)
            """, new { TenantId, DocType = request.DocType.Trim() }, cancellationToken: cancellationToken));
        return doc is null
            ? ApiResponse<WebsiteLegalDto>.FailResponse("Legal document not found.")
            : ApiResponse<WebsiteLegalDto>.SuccessResponse(doc);
    }

    public async Task<ApiResponse<WebsiteSettingsDto>> GetPublicSettingsAsync(GetPublicSettingsQuery request, CancellationToken cancellationToken = default)
        => await GetSettingsAsync(new GetWebsiteSettingsQuery(), cancellationToken);

    private async Task<WebsiteSettingsDto?> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<WebsiteSettingsDto>(
            new CommandDefinition(SettingsSql, new { TenantId }, cancellationToken: cancellationToken));
    }

    private async Task<WebsiteSectionDto?> LoadSectionAsync(System.Data.IDbConnection connection, int id, CancellationToken ct) =>
        await connection.QuerySingleOrDefaultAsync<WebsiteSectionDto>(new CommandDefinition(
            SectionSelect + " WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = id, TenantId }, cancellationToken: ct));

    private async Task<WebsiteFeatureDto?> LoadFeatureAsync(System.Data.IDbConnection connection, int id, CancellationToken ct) =>
        await connection.QuerySingleOrDefaultAsync<WebsiteFeatureDto>(new CommandDefinition(
            FeatureSelect + " WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = id, TenantId }, cancellationToken: ct));
}
