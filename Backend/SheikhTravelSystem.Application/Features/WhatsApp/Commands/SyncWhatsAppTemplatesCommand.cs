using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Commands;

public record SyncWhatsAppTemplatesCommand(int AccountId)
    : IRequest<ApiResponse<object>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "WhatsAppTemplate";
    public int? AuditEntityId => AccountId;
}

public class SyncWhatsAppTemplatesCommandHandler(
    IWhatsAppInboxRepository inboxRepository,
    IWhatsAppTemplateRepository templateRepository,
    IWhatsAppCloudApiExtended cloudApi,
    IWhatsAppAccountConfig accountConfig,
    ITenantContext tenantContext)
    : IRequestHandler<SyncWhatsAppTemplatesCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(
        SyncWhatsAppTemplatesCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var account = await inboxRepository.GetAccountByIdAsync(tenantId, request.AccountId, cancellationToken);
        if (account is null)
            return ApiResponse<object>.FailResponse("WhatsApp account not found.");
        if (accountConfig.ResolveCredentials(account) is null)
            return ApiResponse<object>.FailResponse("WhatsApp credentials are not configured for this account.");

        var remote = await cloudApi.ListMessageTemplatesAsync(account, cancellationToken);
        var upserted = 0;
        foreach (var t in remote)
        {
            await templateRepository.UpsertAsync(
                tenantId,
                account.Id,
                t.Name,
                t.Language,
                t.Category,
                t.Status,
                t.MetaTemplateId,
                t.BodyPreview,
                cancellationToken);
            upserted++;
        }

        return ApiResponse<object>.SuccessResponse(new
        {
            accountId = account.Id,
            synced = upserted
        });
    }
}
