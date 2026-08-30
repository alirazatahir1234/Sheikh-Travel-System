namespace SheikhTravelSystem.Application.Features.Website.DTOs;

public record WebsiteSettingsDto(
    int Id,
    string SiteName,
    string? LogoUrl,
    string? FaviconUrl,
    string? SupportEmail,
    string? SalesEmail,
    string? PrivacyEmail,
    string? Phone,
    string? Address,
    string? LinkedInUrl,
    string? FacebookUrl,
    string? XUrl,
    string? YouTubeUrl,
    string? DefaultMetaTitle,
    string? DefaultMetaDescription,
    string? AnalyticsId);

public record WebsitePageDto(
    int Id,
    string Slug,
    string Title,
    string? Description,
    string? MetaTitle,
    string? MetaDescription,
    string? OgImage,
    string Status,
    DateTime? PublishedAt,
    DateTime UpdatedAt);

public record WebsiteSectionDto(
    int Id,
    int PageId,
    string SectionType,
    string? Title,
    string? Subtitle,
    string? Content,
    string? ImageUrl,
    string? ButtonText,
    string? ButtonUrl,
    string? SecondaryButtonText,
    string? SecondaryButtonUrl,
    int DisplayOrder,
    bool IsActive,
    string Status);

public record WebsiteFeatureDto(
    int Id,
    string Title,
    string? Description,
    string? IconKey,
    string? ImageUrl,
    string? LinkUrl,
    int DisplayOrder,
    bool IsActive,
    string Status);

public record WebsiteLegalDto(
    int Id,
    string DocType,
    string Title,
    string Content,
    string? Version,
    string Status,
    DateTime? PublishedAt,
    DateTime UpdatedAt);

public record WebsiteMediaDto(
    int Id,
    string FileName,
    string FileUrl,
    string? FileType,
    string? AltText,
    long? SizeBytes,
    DateTime CreatedAt);

public record WebsiteContactRequestDto(
    int Id,
    string FirstName,
    string LastName,
    string Company,
    string Email,
    string? Phone,
    string? Country,
    string? FleetSize,
    string? InterestedIn,
    string Message,
    string Status,
    DateTime CreatedAt);

public record WebsiteDemoRequestDto(
    int Id,
    string Name,
    string Company,
    string Email,
    string? Phone,
    string? Country,
    string? VehicleCount,
    string? CurrentGpsProvider,
    string? InterestedProduct,
    string? Message,
    string Status,
    DateTime CreatedAt);

public record WebsiteDashboardDto(
    int PageCount,
    int PublishedPages,
    int DraftPages,
    int FeatureCount,
    int ContactRequests,
    int DemoRequests,
    int NewContactRequests,
    int NewDemoRequests,
    int MediaCount,
    DateTime? LastPublishedAt);

public record WebsitePublicHomeDto(
    WebsiteSettingsDto Settings,
    IReadOnlyList<WebsiteSectionDto> Sections,
    IReadOnlyList<WebsiteFeatureDto> Features);

public record WebsitePublicPageDto(
    WebsitePageDto Page,
    IReadOnlyList<WebsiteSectionDto> Sections);
