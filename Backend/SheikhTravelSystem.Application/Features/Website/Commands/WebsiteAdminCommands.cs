using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Website.DTOs;
using SheikhTravelSystem.Application.Features.Website.Queries;

namespace SheikhTravelSystem.Application.Features.Website.Commands;

internal static class WebsiteStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";

    public static readonly HashSet<string> Content = new(StringComparer.OrdinalIgnoreCase)
    {
        Draft, Published
    };

    public static readonly HashSet<string> Lead = new(StringComparer.OrdinalIgnoreCase)
    {
        "New", "Contacted", "InProgress", "Qualified", "Converted", "Closed"
    };
}

public record UpdateWebsiteSettingsCommand(
    string SiteName,
    string? LogoUrl = null,
    string? FaviconUrl = null,
    string? SupportEmail = null,
    string? SalesEmail = null,
    string? PrivacyEmail = null,
    string? Phone = null,
    string? Address = null,
    string? LinkedInUrl = null,
    string? FacebookUrl = null,
    string? XUrl = null,
    string? YouTubeUrl = null,
    string? DefaultMetaTitle = null,
    string? DefaultMetaDescription = null,
    string? AnalyticsId = null) : IRequest<ApiResponse<WebsiteSettingsDto>>;

public class UpdateWebsiteSettingsCommandValidator : AbstractValidator<UpdateWebsiteSettingsCommand>
{
    public UpdateWebsiteSettingsCommandValidator()
    {
        RuleFor(x => x.SiteName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.SupportEmail).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.SupportEmail));
        RuleFor(x => x.SalesEmail).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.SalesEmail));
        RuleFor(x => x.PrivacyEmail).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.PrivacyEmail));
    }
}

public class UpdateWebsiteSettingsCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpdateWebsiteSettingsCommand, ApiResponse<WebsiteSettingsDto>>
{
    public async Task<ApiResponse<WebsiteSettingsDto>> Handle(
        UpdateWebsiteSettingsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
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
            TenantId = tenantId,
            request.SiteName,
            request.LogoUrl,
            request.FaviconUrl,
            request.SupportEmail,
            request.SalesEmail,
            request.PrivacyEmail,
            request.Phone,
            request.Address,
            request.LinkedInUrl,
            request.FacebookUrl,
            request.XUrl,
            request.YouTubeUrl,
            request.DefaultMetaTitle,
            request.DefaultMetaDescription,
            request.AnalyticsId
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
                TenantId = tenantId,
                request.SiteName,
                request.LogoUrl,
                request.FaviconUrl,
                request.SupportEmail,
                request.SalesEmail,
                request.PrivacyEmail,
                request.Phone,
                request.Address,
                request.LinkedInUrl,
                request.FacebookUrl,
                request.XUrl,
                request.YouTubeUrl,
                request.DefaultMetaTitle,
                request.DefaultMetaDescription,
                request.AnalyticsId
            }, cancellationToken: cancellationToken));
        }

        var settings = await connection.QuerySingleAsync<WebsiteSettingsDto>(
            new CommandDefinition(WebsitePublicSql.Settings, new { TenantId = tenantId },
                cancellationToken: cancellationToken));
        return ApiResponse<WebsiteSettingsDto>.SuccessResponse(settings, "Settings updated.");
    }
}

public record UpdateWebsitePageCommand(
    int Id,
    string Title,
    string? Description = null,
    string? MetaTitle = null,
    string? MetaDescription = null,
    string? OgImage = null,
    string? Status = null) : IRequest<ApiResponse<WebsitePageDto>>;

public class UpdateWebsitePageCommandValidator : AbstractValidator<UpdateWebsitePageCommand>
{
    public UpdateWebsitePageCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Status).Must(s => s is null || WebsiteStatuses.Content.Contains(s))
            .WithMessage("Status must be Draft or Published.");
    }
}

public class UpdateWebsitePageCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpdateWebsitePageCommand, ApiResponse<WebsitePageDto>>
{
    public async Task<ApiResponse<WebsitePageDto>> Handle(
        UpdateWebsitePageCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM WebsitePages WHERE Id = @Id AND TenantId = @TenantId) THEN 1 ELSE 0 END",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (!exists)
            return ApiResponse<WebsitePageDto>.FailResponse("Page not found.");

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsitePages SET
                Title = @Title,
                Description = @Description,
                MetaTitle = @MetaTitle,
                MetaDescription = @MetaDescription,
                OgImage = @OgImage,
                Status = COALESCE(@Status, Status),
                PublishedAt = CASE
                    WHEN @Status = N'Published' AND (Status <> N'Published' OR PublishedAt IS NULL) THEN SYSUTCDATETIME()
                    WHEN @Status = N'Draft' THEN NULL
                    ELSE PublishedAt END,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            request.Id,
            TenantId = tenantId,
            request.Title,
            request.Description,
            request.MetaTitle,
            request.MetaDescription,
            request.OgImage,
            Status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim()
        }, cancellationToken: cancellationToken));

        var page = await connection.QuerySingleAsync<WebsitePageDto>(
            new CommandDefinition("""
                SELECT Id, Slug, Title, Description, MetaTitle, MetaDescription, OgImage,
                       Status, PublishedAt, UpdatedAt
                FROM WebsitePages WHERE Id = @Id AND TenantId = @TenantId
                """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<WebsitePageDto>.SuccessResponse(page, "Page updated.");
    }
}

public record PublishWebsitePageCommand(int Id) : IRequest<ApiResponse<WebsitePageDto>>;

public class PublishWebsitePageCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<PublishWebsitePageCommand, ApiResponse<WebsitePageDto>>
{
    public async Task<ApiResponse<WebsitePageDto>> Handle(
        PublishWebsitePageCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();

        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsitePages SET
                Status = N'Published',
                PublishedAt = COALESCE(PublishedAt, SYSUTCDATETIME()),
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (updated == 0)
            return ApiResponse<WebsitePageDto>.FailResponse("Page not found.");

        var page = await connection.QuerySingleAsync<WebsitePageDto>(
            new CommandDefinition("""
                SELECT Id, Slug, Title, Description, MetaTitle, MetaDescription, OgImage,
                       Status, PublishedAt, UpdatedAt
                FROM WebsitePages WHERE Id = @Id AND TenantId = @TenantId
                """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<WebsitePageDto>.SuccessResponse(page, "Page published.");
    }
}

public record UpsertWebsiteSectionCommand(
    int? Id,
    int PageId,
    string SectionType,
    string? Title = null,
    string? Subtitle = null,
    string? Content = null,
    string? ImageUrl = null,
    string? ButtonText = null,
    string? ButtonUrl = null,
    string? SecondaryButtonText = null,
    string? SecondaryButtonUrl = null,
    int DisplayOrder = 0,
    bool IsActive = true,
    string Status = WebsiteStatuses.Draft) : IRequest<ApiResponse<WebsiteSectionDto>>;

public class UpsertWebsiteSectionCommandValidator : AbstractValidator<UpsertWebsiteSectionCommand>
{
    public UpsertWebsiteSectionCommandValidator()
    {
        RuleFor(x => x.PageId).GreaterThan(0);
        RuleFor(x => x.SectionType).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Status).Must(s => WebsiteStatuses.Content.Contains(s))
            .WithMessage("Status must be Draft or Published.");
    }
}

public class UpsertWebsiteSectionCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpsertWebsiteSectionCommand, ApiResponse<WebsiteSectionDto>>
{
    public async Task<ApiResponse<WebsiteSectionDto>> Handle(
        UpsertWebsiteSectionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();

        var pageExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM WebsitePages WHERE Id = @PageId AND TenantId = @TenantId) THEN 1 ELSE 0 END",
            new { request.PageId, TenantId = tenantId }, cancellationToken: cancellationToken));
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
                request.Id,
                TenantId = tenantId,
                request.PageId,
                request.SectionType,
                request.Title,
                request.Subtitle,
                request.Content,
                request.ImageUrl,
                request.ButtonText,
                request.ButtonUrl,
                request.SecondaryButtonText,
                request.SecondaryButtonUrl,
                request.DisplayOrder,
                request.IsActive,
                request.Status
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
                TenantId = tenantId,
                request.PageId,
                request.SectionType,
                request.Title,
                request.Subtitle,
                request.Content,
                request.ImageUrl,
                request.ButtonText,
                request.ButtonUrl,
                request.SecondaryButtonText,
                request.SecondaryButtonUrl,
                request.DisplayOrder,
                request.IsActive,
                request.Status
            }, cancellationToken: cancellationToken));
        }

        var section = await LoadSectionAsync(connection, tenantId, id, cancellationToken);
        return ApiResponse<WebsiteSectionDto>.SuccessResponse(section!, "Section saved.");
    }

    internal static async Task<WebsiteSectionDto?> LoadSectionAsync(
        System.Data.IDbConnection connection, int tenantId, int id, CancellationToken ct) =>
        await connection.QuerySingleOrDefaultAsync<WebsiteSectionDto>(new CommandDefinition("""
            SELECT Id, PageId, SectionType, Title, Subtitle, Content, ImageUrl,
                   ButtonText, ButtonUrl, SecondaryButtonText, SecondaryButtonUrl,
                   DisplayOrder, IsActive, Status
            FROM WebsiteSections WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = id, TenantId = tenantId }, cancellationToken: ct));
}

public record DeleteWebsiteSectionCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteWebsiteSectionCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<DeleteWebsiteSectionCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteWebsiteSectionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();
        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM WebsiteSections WHERE Id = @Id AND TenantId = @TenantId",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (deleted == 0)
            return ApiResponse<bool>.FailResponse("Section not found.");
        return ApiResponse<bool>.SuccessResponse(true, "Section deleted.");
    }
}

public record PublishWebsiteSectionCommand(int Id) : IRequest<ApiResponse<WebsiteSectionDto>>;

public class PublishWebsiteSectionCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<PublishWebsiteSectionCommand, ApiResponse<WebsiteSectionDto>>
{
    public async Task<ApiResponse<WebsiteSectionDto>> Handle(
        PublishWebsiteSectionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteSections SET Status = N'Published', IsActive = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (updated == 0)
            return ApiResponse<WebsiteSectionDto>.FailResponse("Section not found.");

        var section = await UpsertWebsiteSectionCommandHandler.LoadSectionAsync(
            connection, tenantId, request.Id, cancellationToken);
        return ApiResponse<WebsiteSectionDto>.SuccessResponse(section!, "Section published.");
    }
}

public record UpsertWebsiteFeatureCommand(
    int? Id,
    string Title,
    string? Description = null,
    string? IconKey = null,
    string? ImageUrl = null,
    string? LinkUrl = null,
    int DisplayOrder = 0,
    bool IsActive = true,
    string Status = WebsiteStatuses.Draft) : IRequest<ApiResponse<WebsiteFeatureDto>>;

public class UpsertWebsiteFeatureCommandValidator : AbstractValidator<UpsertWebsiteFeatureCommand>
{
    public UpsertWebsiteFeatureCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Status).Must(s => WebsiteStatuses.Content.Contains(s))
            .WithMessage("Status must be Draft or Published.");
    }
}

public class UpsertWebsiteFeatureCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpsertWebsiteFeatureCommand, ApiResponse<WebsiteFeatureDto>>
{
    public async Task<ApiResponse<WebsiteFeatureDto>> Handle(
        UpsertWebsiteFeatureCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
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
                request.Id,
                TenantId = tenantId,
                request.Title,
                request.Description,
                request.IconKey,
                request.ImageUrl,
                request.LinkUrl,
                request.DisplayOrder,
                request.IsActive,
                request.Status
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
                TenantId = tenantId,
                request.Title,
                request.Description,
                request.IconKey,
                request.ImageUrl,
                request.LinkUrl,
                request.DisplayOrder,
                request.IsActive,
                request.Status
            }, cancellationToken: cancellationToken));
        }

        var feature = await LoadFeatureAsync(connection, tenantId, id, cancellationToken);
        return ApiResponse<WebsiteFeatureDto>.SuccessResponse(feature!, "Feature saved.");
    }

    internal static async Task<WebsiteFeatureDto?> LoadFeatureAsync(
        System.Data.IDbConnection connection, int tenantId, int id, CancellationToken ct) =>
        await connection.QuerySingleOrDefaultAsync<WebsiteFeatureDto>(new CommandDefinition("""
            SELECT Id, Title, Description, IconKey, ImageUrl, LinkUrl, DisplayOrder, IsActive, Status
            FROM WebsiteFeatures WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = id, TenantId = tenantId }, cancellationToken: ct));
}

public record DeleteWebsiteFeatureCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteWebsiteFeatureCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<DeleteWebsiteFeatureCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteWebsiteFeatureCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();
        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM WebsiteFeatures WHERE Id = @Id AND TenantId = @TenantId",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (deleted == 0)
            return ApiResponse<bool>.FailResponse("Feature not found.");
        return ApiResponse<bool>.SuccessResponse(true, "Feature deleted.");
    }
}

public record PublishWebsiteFeatureCommand(int Id) : IRequest<ApiResponse<WebsiteFeatureDto>>;

public class PublishWebsiteFeatureCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<PublishWebsiteFeatureCommand, ApiResponse<WebsiteFeatureDto>>
{
    public async Task<ApiResponse<WebsiteFeatureDto>> Handle(
        PublishWebsiteFeatureCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteFeatures SET Status = N'Published', IsActive = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (updated == 0)
            return ApiResponse<WebsiteFeatureDto>.FailResponse("Feature not found.");

        var feature = await UpsertWebsiteFeatureCommandHandler.LoadFeatureAsync(
            connection, tenantId, request.Id, cancellationToken);
        return ApiResponse<WebsiteFeatureDto>.SuccessResponse(feature!, "Feature published.");
    }
}

public record UpdateWebsiteLegalCommand(
    string DocType,
    string Title,
    string Content,
    string? Version = null) : IRequest<ApiResponse<WebsiteLegalDto>>;

public class UpdateWebsiteLegalCommandValidator : AbstractValidator<UpdateWebsiteLegalCommand>
{
    public UpdateWebsiteLegalCommandValidator()
    {
        RuleFor(x => x.DocType).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotEmpty();
        RuleFor(x => x.Version).MaximumLength(40);
    }
}

public class UpdateWebsiteLegalCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpdateWebsiteLegalCommand, ApiResponse<WebsiteLegalDto>>
{
    public async Task<ApiResponse<WebsiteLegalDto>> Handle(
        UpdateWebsiteLegalCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();

        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteLegalDocuments SET
                Title = @Title, Content = @Content, Version = @Version, UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND LOWER(DocType) = LOWER(@DocType)
            """, new
        {
            TenantId = tenantId,
            request.DocType,
            request.Title,
            request.Content,
            request.Version
        }, cancellationToken: cancellationToken));

        if (updated == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO WebsiteLegalDocuments (TenantId, DocType, Title, Content, Version, Status)
                VALUES (@TenantId, @DocType, @Title, @Content, @Version, N'Draft')
                """, new
            {
                TenantId = tenantId,
                request.DocType,
                request.Title,
                request.Content,
                request.Version
            }, cancellationToken: cancellationToken));
        }

        var doc = await connection.QuerySingleAsync<WebsiteLegalDto>(new CommandDefinition("""
            SELECT Id, DocType, Title, Content, Version, Status, PublishedAt, UpdatedAt
            FROM WebsiteLegalDocuments
            WHERE TenantId = @TenantId AND LOWER(DocType) = LOWER(@DocType)
            """, new { TenantId = tenantId, request.DocType }, cancellationToken: cancellationToken));

        return ApiResponse<WebsiteLegalDto>.SuccessResponse(doc, "Legal document updated.");
    }
}

public record PublishWebsiteLegalCommand(string DocType) : IRequest<ApiResponse<WebsiteLegalDto>>;

public class PublishWebsiteLegalCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<PublishWebsiteLegalCommand, ApiResponse<WebsiteLegalDto>>
{
    public async Task<ApiResponse<WebsiteLegalDto>> Handle(
        PublishWebsiteLegalCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();

        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteLegalDocuments SET
                Status = N'Published',
                PublishedAt = COALESCE(PublishedAt, SYSUTCDATETIME()),
                UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND LOWER(DocType) = LOWER(@DocType)
            """, new { TenantId = tenantId, request.DocType }, cancellationToken: cancellationToken));

        if (updated == 0)
            return ApiResponse<WebsiteLegalDto>.FailResponse("Legal document not found.");

        var doc = await connection.QuerySingleAsync<WebsiteLegalDto>(new CommandDefinition("""
            SELECT Id, DocType, Title, Content, Version, Status, PublishedAt, UpdatedAt
            FROM WebsiteLegalDocuments
            WHERE TenantId = @TenantId AND LOWER(DocType) = LOWER(@DocType)
            """, new { TenantId = tenantId, request.DocType }, cancellationToken: cancellationToken));

        return ApiResponse<WebsiteLegalDto>.SuccessResponse(doc, "Legal document published.");
    }
}

public record UploadWebsiteMediaCommand(
    Stream FileStream,
    string FileName,
    string ContentType,
    string? AltText = null,
    long? SizeBytes = null) : IRequest<ApiResponse<WebsiteMediaDto>>;

public class UploadWebsiteMediaCommandValidator : AbstractValidator<UploadWebsiteMediaCommand>
{
    public UploadWebsiteMediaCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(260);
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(80);
        RuleFor(x => x.FileStream).NotNull();
        RuleFor(x => x.AltText).MaximumLength(200);
    }
}

public class UploadWebsiteMediaCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<UploadWebsiteMediaCommand, ApiResponse<WebsiteMediaDto>>
{
    public async Task<ApiResponse<WebsiteMediaDto>> Handle(
        UploadWebsiteMediaCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        var stored = await fileStorage.SaveAsync(
            request.FileStream,
            request.FileName,
            request.ContentType,
            "website",
            cancellationToken);

        using var connection = dbFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO WebsiteMedia (TenantId, FileName, FileUrl, StorageKey, FileType, AltText, SizeBytes)
            OUTPUT INSERTED.Id
            VALUES (@TenantId, @FileName, @FileUrl, @StorageKey, @FileType, @AltText, @SizeBytes)
            """, new
        {
            TenantId = tenantId,
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
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<WebsiteMediaDto>.SuccessResponse(media, "Media uploaded.");
    }
}

public record DeleteWebsiteMediaCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteWebsiteMediaCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<DeleteWebsiteMediaCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteWebsiteMediaCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<(string? StorageKey, string FileUrl)>(
            new CommandDefinition(
                "SELECT StorageKey, FileUrl FROM WebsiteMedia WHERE Id = @Id AND TenantId = @TenantId",
                new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row.FileUrl is null && row.StorageKey is null)
            return ApiResponse<bool>.FailResponse("Media not found.");

        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM WebsiteMedia WHERE Id = @Id AND TenantId = @TenantId",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (deleted == 0)
            return ApiResponse<bool>.FailResponse("Media not found.");

        var key = !string.IsNullOrWhiteSpace(row.StorageKey) ? row.StorageKey : row.FileUrl;
        if (!string.IsNullOrWhiteSpace(key))
        {
            try
            {
                await fileStorage.DeleteAsync(key, cancellationToken);
            }
            catch
            {
                // DB row removed; storage cleanup best-effort
            }
        }

        return ApiResponse<bool>.SuccessResponse(true, "Media deleted.");
    }
}

public record UpdateContactRequestStatusCommand(int Id, string Status) : IRequest<ApiResponse<WebsiteContactRequestDto>>;

public class UpdateContactRequestStatusCommandValidator : AbstractValidator<UpdateContactRequestStatusCommand>
{
    public UpdateContactRequestStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Status).Must(s => WebsiteStatuses.Lead.Contains(s))
            .WithMessage("Status must be New, Contacted, InProgress, Qualified, Converted, or Closed.");
    }
}

public class UpdateContactRequestStatusCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpdateContactRequestStatusCommand, ApiResponse<WebsiteContactRequestDto>>
{
    public async Task<ApiResponse<WebsiteContactRequestDto>> Handle(
        UpdateContactRequestStatusCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();

        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteContactRequests SET Status = @Status, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId = tenantId, request.Status }, cancellationToken: cancellationToken));

        if (updated == 0)
            return ApiResponse<WebsiteContactRequestDto>.FailResponse("Contact request not found.");

        var row = await connection.QuerySingleAsync<WebsiteContactRequestDto>(new CommandDefinition("""
            SELECT Id, FirstName, LastName, Company, Email, Phone, Country, FleetSize,
                   InterestedIn, Message, Status, CreatedAt
            FROM WebsiteContactRequests WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<WebsiteContactRequestDto>.SuccessResponse(row, "Status updated.");
    }
}

public record UpdateDemoRequestStatusCommand(int Id, string Status) : IRequest<ApiResponse<WebsiteDemoRequestDto>>;

public class UpdateDemoRequestStatusCommandValidator : AbstractValidator<UpdateDemoRequestStatusCommand>
{
    public UpdateDemoRequestStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Status).Must(s => WebsiteStatuses.Lead.Contains(s))
            .WithMessage("Status must be New, Contacted, InProgress, Qualified, Converted, or Closed.");
    }
}

public class UpdateDemoRequestStatusCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpdateDemoRequestStatusCommand, ApiResponse<WebsiteDemoRequestDto>>
{
    public async Task<ApiResponse<WebsiteDemoRequestDto>> Handle(
        UpdateDemoRequestStatusCommand request, CancellationToken cancellationToken)
    {
        var tenantId = WebsiteTenant.Resolve(tenantContext);
        using var connection = dbFactory.CreateConnection();

        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WebsiteDemoRequests SET Status = @Status, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId = tenantId, request.Status }, cancellationToken: cancellationToken));

        if (updated == 0)
            return ApiResponse<WebsiteDemoRequestDto>.FailResponse("Demo request not found.");

        var row = await connection.QuerySingleAsync<WebsiteDemoRequestDto>(new CommandDefinition("""
            SELECT Id, Name, Company, Email, Phone, Country, VehicleCount, CurrentGpsProvider,
                   InterestedProduct, Message, Status, CreatedAt
            FROM WebsiteDemoRequests WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<WebsiteDemoRequestDto>.SuccessResponse(row, "Status updated.");
    }
}
