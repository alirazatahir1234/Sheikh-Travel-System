using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Queries;

public record GetWhatsAppAccountsQuery(bool IncludeInactive = false)
    : IRequest<ApiResponse<IReadOnlyList<WhatsAppAccountDto>>>;

public class GetWhatsAppAccountsQueryHandler(
    IWhatsAppInboxRepository repository,
    IWhatsAppAccountConfig accountConfig,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetWhatsAppAccountsQuery, ApiResponse<IReadOnlyList<WhatsAppAccountDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<WhatsAppAccountDto>>> Handle(
        GetWhatsAppAccountsQuery request, CancellationToken cancellationToken)
    {
        var includeInactive = request.IncludeInactive;
        if (includeInactive &&
            !currentUser.HasPermission(WhatsAppPermissions.Manage) &&
            !currentUser.HasPermission(WhatsAppPermissions.ManageAccounts))
            return ApiResponse<IReadOnlyList<WhatsAppAccountDto>>.FailResponse(
                "WhatsApp.ManageAccounts permission is required to list inactive accounts.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var accounts = await repository.GetAccountsAsync(tenantId, includeInactive, cancellationToken);
        var enriched = accounts.Select(accountConfig.Enrich).ToList();
        return ApiResponse<IReadOnlyList<WhatsAppAccountDto>>.SuccessResponse(enriched);
    }
}

public record GetWhatsAppConversationsQuery(
    int? AccountId = null,
    string? Search = null,
    string? Filter = null,
    int Page = 1,
    int PageSize = 30)
    : IRequest<ApiResponse<PagedResult<WhatsAppConversationDto>>>;

public class GetWhatsAppConversationsQueryHandler(
    IWhatsAppInboxRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetWhatsAppConversationsQuery, ApiResponse<PagedResult<WhatsAppConversationDto>>>
{
    public async Task<ApiResponse<PagedResult<WhatsAppConversationDto>>> Handle(
        GetWhatsAppConversationsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var (items, total) = await repository.GetConversationsAsync(
            tenantId,
            request.AccountId,
            request.Search,
            request.Filter,
            currentUser.UserId,
            request.Page,
            request.PageSize,
            cancellationToken);
        var now = DateTime.UtcNow;
        var enriched = items.Select(c => WhatsAppMessagingWindow.WithWindowFlag(c, now)).ToList();
        return ApiResponse<PagedResult<WhatsAppConversationDto>>.SuccessResponse(
            new PagedResult<WhatsAppConversationDto>
            {
                Items = enriched,
                TotalCount = total,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }
}

public record GetWhatsAppTemplatesQuery(int? AccountId = null, string? Status = null)
    : IRequest<ApiResponse<IReadOnlyList<WhatsAppTemplateDto>>>;

public class GetWhatsAppTemplatesQueryHandler(
    IWhatsAppTemplateRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<GetWhatsAppTemplatesQuery, ApiResponse<IReadOnlyList<WhatsAppTemplateDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<WhatsAppTemplateDto>>> Handle(
        GetWhatsAppTemplatesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var items = await repository.ListAsync(tenantId, request.AccountId, request.Status, cancellationToken);
        return ApiResponse<IReadOnlyList<WhatsAppTemplateDto>>.SuccessResponse(items);
    }
}

public record GetWhatsAppMessagesQuery(int ConversationId, int Page = 1, int PageSize = 100)
    : IRequest<ApiResponse<PagedResult<WhatsAppMessageDto>>>;

public class GetWhatsAppMessagesQueryHandler(
    IWhatsAppInboxRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<GetWhatsAppMessagesQuery, ApiResponse<PagedResult<WhatsAppMessageDto>>>
{
    public async Task<ApiResponse<PagedResult<WhatsAppMessageDto>>> Handle(
        GetWhatsAppMessagesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var conversation = await repository.GetConversationAsync(tenantId, request.ConversationId, cancellationToken);
        if (conversation is null)
            return ApiResponse<PagedResult<WhatsAppMessageDto>>.FailResponse("Conversation not found.");

        var (items, total) = await repository.GetMessagesAsync(
            tenantId, request.ConversationId, request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PagedResult<WhatsAppMessageDto>>.SuccessResponse(
            new PagedResult<WhatsAppMessageDto>
            {
                Items = items.ToList(),
                TotalCount = total,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }
}

public record GetWhatsAppContactQuery(int ContactId) : IRequest<ApiResponse<WhatsAppContactDto>>;

public class GetWhatsAppContactQueryHandler(
    IWhatsAppInboxRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<GetWhatsAppContactQuery, ApiResponse<WhatsAppContactDto>>
{
    public async Task<ApiResponse<WhatsAppContactDto>> Handle(
        GetWhatsAppContactQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var contact = await repository.GetContactAsync(tenantId, request.ContactId, cancellationToken);
        return contact is null
            ? ApiResponse<WhatsAppContactDto>.FailResponse("Contact not found.")
            : ApiResponse<WhatsAppContactDto>.SuccessResponse(contact);
    }
}

public record GetWhatsAppMediaQuery(string MediaId, int AccountId)
    : IRequest<ApiResponse<WhatsAppMediaProxyResult>>;

public class GetWhatsAppMediaQueryHandler(
    IWhatsAppInboxRepository repository,
    IWhatsAppCloudApiService cloudApi,
    IWhatsAppAccountConfig accountConfig,
    ITenantContext tenantContext)
    : IRequestHandler<GetWhatsAppMediaQuery, ApiResponse<WhatsAppMediaProxyResult>>
{
    public async Task<ApiResponse<WhatsAppMediaProxyResult>> Handle(
        GetWhatsAppMediaQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var account = await repository.GetAccountByIdAsync(tenantId, request.AccountId, cancellationToken);
        if (account is null)
            return ApiResponse<WhatsAppMediaProxyResult>.FailResponse("Account not found.");

        if (accountConfig.ResolveCredentials(account) is null)
            return ApiResponse<WhatsAppMediaProxyResult>.FailResponse("WhatsApp credentials are not configured.");

        var media = await cloudApi.DownloadMediaAsync(account, request.MediaId, cancellationToken);
        return media is null
            ? ApiResponse<WhatsAppMediaProxyResult>.FailResponse("Media not available.")
            : ApiResponse<WhatsAppMediaProxyResult>.SuccessResponse(media);
    }
}
