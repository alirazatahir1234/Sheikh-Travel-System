using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Website.DTOs;

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

public record UpdateWebsitePageCommand(
    int Id,
    string Title,
    string? Description = null,
    string? MetaTitle = null,
    string? MetaDescription = null,
    string? OgImage = null,
    string? Status = null) : IRequest<ApiResponse<WebsitePageDto>>;

public record CreateWebsitePageCommand(
    string Slug,
    string Title,
    string? Description = null,
    string? MetaTitle = null,
    string? MetaDescription = null,
    string? OgImage = null,
    string? Status = null) : IRequest<ApiResponse<WebsitePageDto>>;

public class CreateWebsitePageCommandValidator : AbstractValidator<CreateWebsitePageCommand>
{
    public CreateWebsitePageCommandValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(120)
            .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase letters, numbers, and hyphens.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Status).Must(s => s is null || WebsiteStatuses.Content.Contains(s))
            .WithMessage("Status must be Draft or Published.");
    }
}

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

public record PublishWebsitePageCommand(int Id) : IRequest<ApiResponse<WebsitePageDto>>;

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

public record DeleteWebsiteSectionCommand(int Id) : IRequest<ApiResponse<bool>>;

public record PublishWebsiteSectionCommand(int Id) : IRequest<ApiResponse<WebsiteSectionDto>>;

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

public record DeleteWebsiteFeatureCommand(int Id) : IRequest<ApiResponse<bool>>;

public record PublishWebsiteFeatureCommand(int Id) : IRequest<ApiResponse<WebsiteFeatureDto>>;

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

public record PublishWebsiteLegalCommand(string DocType) : IRequest<ApiResponse<WebsiteLegalDto>>;

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

public record DeleteWebsiteMediaCommand(int Id) : IRequest<ApiResponse<bool>>;

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

public class UpdateWebsiteSettingsCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<UpdateWebsiteSettingsCommand, ApiResponse<WebsiteSettingsDto>>
{
    public Task<ApiResponse<WebsiteSettingsDto>> Handle(UpdateWebsiteSettingsCommand request, CancellationToken cancellationToken)
        => websiteRepository.UpdateSettingsAsync(request, cancellationToken);
}
public class CreateWebsitePageCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<CreateWebsitePageCommand, ApiResponse<WebsitePageDto>>
{
    public Task<ApiResponse<WebsitePageDto>> Handle(CreateWebsitePageCommand request, CancellationToken cancellationToken)
        => websiteRepository.CreatePageAsync(request, cancellationToken);
}
public class UpdateWebsitePageCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<UpdateWebsitePageCommand, ApiResponse<WebsitePageDto>>
{
    public Task<ApiResponse<WebsitePageDto>> Handle(UpdateWebsitePageCommand request, CancellationToken cancellationToken)
        => websiteRepository.UpdatePageAsync(request, cancellationToken);
}
public class PublishWebsitePageCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<PublishWebsitePageCommand, ApiResponse<WebsitePageDto>>
{
    public Task<ApiResponse<WebsitePageDto>> Handle(PublishWebsitePageCommand request, CancellationToken cancellationToken)
        => websiteRepository.PublishPageAsync(request, cancellationToken);
}
public class UpsertWebsiteSectionCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<UpsertWebsiteSectionCommand, ApiResponse<WebsiteSectionDto>>
{
    public Task<ApiResponse<WebsiteSectionDto>> Handle(UpsertWebsiteSectionCommand request, CancellationToken cancellationToken)
        => websiteRepository.UpsertSectionAsync(request, cancellationToken);
}
public class DeleteWebsiteSectionCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<DeleteWebsiteSectionCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(DeleteWebsiteSectionCommand request, CancellationToken cancellationToken)
        => websiteRepository.DeleteSectionAsync(request, cancellationToken);
}
public class PublishWebsiteSectionCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<PublishWebsiteSectionCommand, ApiResponse<WebsiteSectionDto>>
{
    public Task<ApiResponse<WebsiteSectionDto>> Handle(PublishWebsiteSectionCommand request, CancellationToken cancellationToken)
        => websiteRepository.PublishSectionAsync(request, cancellationToken);
}
public class UpsertWebsiteFeatureCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<UpsertWebsiteFeatureCommand, ApiResponse<WebsiteFeatureDto>>
{
    public Task<ApiResponse<WebsiteFeatureDto>> Handle(UpsertWebsiteFeatureCommand request, CancellationToken cancellationToken)
        => websiteRepository.UpsertFeatureAsync(request, cancellationToken);
}
public class DeleteWebsiteFeatureCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<DeleteWebsiteFeatureCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(DeleteWebsiteFeatureCommand request, CancellationToken cancellationToken)
        => websiteRepository.DeleteFeatureAsync(request, cancellationToken);
}
public class PublishWebsiteFeatureCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<PublishWebsiteFeatureCommand, ApiResponse<WebsiteFeatureDto>>
{
    public Task<ApiResponse<WebsiteFeatureDto>> Handle(PublishWebsiteFeatureCommand request, CancellationToken cancellationToken)
        => websiteRepository.PublishFeatureAsync(request, cancellationToken);
}
public class UpdateWebsiteLegalCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<UpdateWebsiteLegalCommand, ApiResponse<WebsiteLegalDto>>
{
    public Task<ApiResponse<WebsiteLegalDto>> Handle(UpdateWebsiteLegalCommand request, CancellationToken cancellationToken)
        => websiteRepository.UpdateLegalAsync(request, cancellationToken);
}
public class PublishWebsiteLegalCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<PublishWebsiteLegalCommand, ApiResponse<WebsiteLegalDto>>
{
    public Task<ApiResponse<WebsiteLegalDto>> Handle(PublishWebsiteLegalCommand request, CancellationToken cancellationToken)
        => websiteRepository.PublishLegalAsync(request, cancellationToken);
}
public class UploadWebsiteMediaCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<UploadWebsiteMediaCommand, ApiResponse<WebsiteMediaDto>>
{
    public Task<ApiResponse<WebsiteMediaDto>> Handle(UploadWebsiteMediaCommand request, CancellationToken cancellationToken)
        => websiteRepository.UploadMediaAsync(request, cancellationToken);
}
public class DeleteWebsiteMediaCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<DeleteWebsiteMediaCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(DeleteWebsiteMediaCommand request, CancellationToken cancellationToken)
        => websiteRepository.DeleteMediaAsync(request, cancellationToken);
}
public class UpdateContactRequestStatusCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<UpdateContactRequestStatusCommand, ApiResponse<WebsiteContactRequestDto>>
{
    public Task<ApiResponse<WebsiteContactRequestDto>> Handle(UpdateContactRequestStatusCommand request, CancellationToken cancellationToken)
        => websiteRepository.UpdateContactRequestStatusAsync(request, cancellationToken);
}
public class UpdateDemoRequestStatusCommandHandler(IWebsiteRepository websiteRepository)
    : IRequestHandler<UpdateDemoRequestStatusCommand, ApiResponse<WebsiteDemoRequestDto>>
{
    public Task<ApiResponse<WebsiteDemoRequestDto>> Handle(UpdateDemoRequestStatusCommand request, CancellationToken cancellationToken)
        => websiteRepository.UpdateDemoRequestStatusAsync(request, cancellationToken);
}
