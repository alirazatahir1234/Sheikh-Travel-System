using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Commands;

public record SetWhatsAppAccountActiveCommand(int AccountId, bool IsActive)
    : IRequest<ApiResponse<WhatsAppAccountDto>>;

public class SetWhatsAppAccountActiveCommandHandler(
    IWhatsAppInboxRepository repository,
    IWhatsAppAccountConfig accountConfig,
    ITenantContext tenantContext)
    : IRequestHandler<SetWhatsAppAccountActiveCommand, ApiResponse<WhatsAppAccountDto>>
{
    public async Task<ApiResponse<WhatsAppAccountDto>> Handle(
        SetWhatsAppAccountActiveCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var existing = await repository.GetAccountByIdAsync(tenantId, request.AccountId, cancellationToken);
        if (existing is null)
            return ApiResponse<WhatsAppAccountDto>.FailResponse("WhatsApp account not found.");

        var updated = await repository.SetAccountActiveAsync(
            tenantId, request.AccountId, request.IsActive, cancellationToken);
        if (!updated)
            return ApiResponse<WhatsAppAccountDto>.FailResponse("WhatsApp account not found.");

        var accounts = await repository.GetAccountsAsync(tenantId, includeInactive: true, cancellationToken);
        var dto = accounts.FirstOrDefault(a => a.Id == request.AccountId);
        if (dto is null)
            return ApiResponse<WhatsAppAccountDto>.FailResponse("WhatsApp account not found after update.");

        return ApiResponse<WhatsAppAccountDto>.SuccessResponse(accountConfig.Enrich(dto));
    }
}

public record CheckWhatsAppAccountHealthCommand(int AccountId)
    : IRequest<ApiResponse<WhatsAppAccountHealthResultDto>>;

public class CheckWhatsAppAccountHealthCommandHandler(
    IWhatsAppInboxRepository repository,
    IWhatsAppAccountConfig accountConfig,
    IWhatsAppCloudApiService cloudApi,
    ITenantContext tenantContext)
    : IRequestHandler<CheckWhatsAppAccountHealthCommand, ApiResponse<WhatsAppAccountHealthResultDto>>
{
    public async Task<ApiResponse<WhatsAppAccountHealthResultDto>> Handle(
        CheckWhatsAppAccountHealthCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var account = await repository.GetAccountByIdAsync(tenantId, request.AccountId, cancellationToken);
        if (account is null)
            return ApiResponse<WhatsAppAccountHealthResultDto>.FailResponse("WhatsApp account not found.");

        var checkedAt = DateTime.UtcNow;
        var credentials = accountConfig.ResolveCredentials(account);
        if (credentials is null)
        {
            const string status = "Misconfigured";
            const string message = "PhoneNumberId or AccessToken is not configured for this account.";
            await repository.UpdateAccountHealthAsync(
                tenantId, request.AccountId, status, message, checkedAt, cancellationToken);
            return ApiResponse<WhatsAppAccountHealthResultDto>.SuccessResponse(
                new WhatsAppAccountHealthResultDto(request.AccountId, status, message, checkedAt));
        }

        var result = await cloudApi.CheckPhoneNumberAsync(account, cancellationToken);
        string healthStatus;
        string? healthMessage;

        if (result.Success)
        {
            healthStatus = "Healthy";
            healthMessage = result.ErrorMessage ?? "Phone number credentials verified with Meta.";
        }
        else if (result.ErrorKind is WhatsAppCloudErrorKind.InvalidConfiguration
                 or WhatsAppCloudErrorKind.InvalidToken
                 or WhatsAppCloudErrorKind.InvalidPhoneNumberId)
        {
            healthStatus = result.ErrorKind == WhatsAppCloudErrorKind.InvalidConfiguration
                ? "Misconfigured"
                : "Unhealthy";
            healthMessage = result.ErrorMessage ?? "Credential check failed.";
        }
        else
        {
            healthStatus = "Unhealthy";
            healthMessage = result.ErrorMessage ?? "Health check failed.";
        }

        await repository.UpdateAccountHealthAsync(
            tenantId, request.AccountId, healthStatus, healthMessage, checkedAt, cancellationToken);

        var dto = new WhatsAppAccountHealthResultDto(
            request.AccountId, healthStatus, healthMessage, checkedAt);

        return new ApiResponse<WhatsAppAccountHealthResultDto>
        {
            Success = result.Success,
            Message = healthMessage ?? (result.Success ? "Healthy" : "Health check failed."),
            Data = dto
        };
    }
}
