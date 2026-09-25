using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Commands;

public record GetWhatsAppAutomationRulesQuery : IRequest<ApiResponse<IReadOnlyList<WhatsAppAutomationRuleDto>>>;

public class GetWhatsAppAutomationRulesQueryHandler(
    IWhatsAppAutomationRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<GetWhatsAppAutomationRulesQuery, ApiResponse<IReadOnlyList<WhatsAppAutomationRuleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<WhatsAppAutomationRuleDto>>> Handle(
        GetWhatsAppAutomationRulesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rules = await repository.GetRulesAsync(tenantId, cancellationToken);
        // Ensure all 10 event types appear
        var byType = rules.ToDictionary(r => r.EventType, StringComparer.OrdinalIgnoreCase);
        var merged = WaAutomationEventType.All.Select(et =>
            byType.TryGetValue(et, out var r)
                ? r
                : new WhatsAppAutomationRuleDto(0, tenantId, et, false, null, null, 0, false, AutomationPolicy.IsUrgentDefault(et))
        ).ToList();
        return ApiResponse<IReadOnlyList<WhatsAppAutomationRuleDto>>.SuccessResponse(merged);
    }
}

public record UpsertWhatsAppAutomationRuleCommand(
    string EventType,
    bool IsEnabled,
    int? AccountId,
    string? TemplateName,
    int OffsetMinutes,
    bool NotifyBooker,
    bool IsUrgent)
    : IRequest<ApiResponse<WhatsAppAutomationRuleDto>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "WhatsAppAutomationRule";
    public int? AuditEntityId => null;
}

public class UpsertWhatsAppAutomationRuleCommandHandler(
    IWhatsAppAutomationRepository repository,
    IWhatsAppTemplateRepository templates,
    ITenantContext tenantContext)
    : IRequestHandler<UpsertWhatsAppAutomationRuleCommand, ApiResponse<WhatsAppAutomationRuleDto>>
{
    public async Task<ApiResponse<WhatsAppAutomationRuleDto>> Handle(
        UpsertWhatsAppAutomationRuleCommand request, CancellationToken cancellationToken)
    {
        if (!WaAutomationEventType.All.Contains(request.EventType, StringComparer.OrdinalIgnoreCase))
            return ApiResponse<WhatsAppAutomationRuleDto>.FailResponse("Unknown event type.");

        var tenantId = tenantContext.GetRequiredTenantId();
        if (request.IsEnabled)
        {
            var name = request.TemplateName ?? request.EventType;
            if (request.AccountId is > 0)
            {
                var t = await templates.GetByNameAsync(tenantId, request.AccountId.Value, name, "en", cancellationToken);
                if (t is null || !string.Equals(t.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                    return ApiResponse<WhatsAppAutomationRuleDto>.FailResponse(
                        "Approved English template required to enable this rule.",
                        code: "TEMPLATE_NOT_APPROVED");
            }
            else
            {
                return ApiResponse<WhatsAppAutomationRuleDto>.FailResponse(
                    "AccountId and approved en template required to enable.",
                    code: "TEMPLATE_NOT_APPROVED");
            }
        }

        var saved = await repository.UpsertRuleAsync(new WhatsAppAutomationRuleDto(
            0, tenantId, request.EventType, request.IsEnabled, request.AccountId,
            request.TemplateName, request.OffsetMinutes, request.NotifyBooker, request.IsUrgent), cancellationToken);

        return ApiResponse<WhatsAppAutomationRuleDto>.SuccessResponse(saved);
    }
}

public record PreviewWhatsAppAutomationQuery(string EventType, int BookingId)
    : IRequest<ApiResponse<AutomationPreviewDto>>;

public class PreviewWhatsAppAutomationQueryHandler(
    IWhatsAppAutomationRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<PreviewWhatsAppAutomationQuery, ApiResponse<AutomationPreviewDto>>
{
    private readonly AutomationMessageComposer _composer = new();

    public async Task<ApiResponse<AutomationPreviewDto>> Handle(
        PreviewWhatsAppAutomationQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rule = await repository.GetRuleAsync(tenantId, request.EventType, cancellationToken);
        var booking = await repository.GetBookingSnapshotAsync(tenantId, request.BookingId, cancellationToken);
        if (booking is null)
            return ApiResponse<AutomationPreviewDto>.FailResponse("Booking not found.");

        var recipient = AutomationMessageComposer.ResolveRecipient(booking, null) ?? "—";
        var lang = AutomationMessageComposer.ResolveLanguage(booking, "en");
        var templateName = rule?.TemplateName ?? request.EventType;
        var composed = _composer.Compose(request.EventType, booking, null, lang, "PREVIEW_TOKEN", templateName);
        if (composed is null)
            return ApiResponse<AutomationPreviewDto>.FailResponse("Cannot compose preview.");

        return ApiResponse<AutomationPreviewDto>.SuccessResponse(new AutomationPreviewDto(
            request.EventType,
            composed.TemplateName,
            composed.Language,
            recipient,
            composed.BodyParameters,
            composed.BodyPreview,
            composed.UrlButtonSuffix is null ? null : $"https://track.sheikhgo.com/t/{composed.UrlButtonSuffix}"));
    }
}

public record GetBookingWhatsAppTimelineQuery(int BookingId)
    : IRequest<ApiResponse<IReadOnlyList<WhatsAppAutomationTimelineItemDto>>>;

public class GetBookingWhatsAppTimelineQueryHandler(
    IWhatsAppAutomationRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<GetBookingWhatsAppTimelineQuery, ApiResponse<IReadOnlyList<WhatsAppAutomationTimelineItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<WhatsAppAutomationTimelineItemDto>>> Handle(
        GetBookingWhatsAppTimelineQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var items = await repository.GetBookingTimelineAsync(tenantId, request.BookingId, cancellationToken);
        return ApiResponse<IReadOnlyList<WhatsAppAutomationTimelineItemDto>>.SuccessResponse(items);
    }
}

public record ResendWhatsAppAutomationEventCommand(long EventId)
    : IRequest<ApiResponse<object>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "WhatsAppAutomationEvent";
    public int? AuditEntityId => null;
}

public class ResendWhatsAppAutomationEventCommandHandler(
    IWhatsAppAutomationRepository repository,
    IWhatsAppAutomationTrigger trigger,
    ITenantContext tenantContext)
    : IRequestHandler<ResendWhatsAppAutomationEventCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(
        ResendWhatsAppAutomationEventCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var evt = await repository.GetEventAsync(request.EventId, cancellationToken);
        if (evt is null || evt.TenantId != tenantId)
            return ApiResponse<object>.FailResponse("Event not found.");

        var resendCount = 0;
        var baseKey = evt.DedupeKey;
        if (baseKey.Contains(":resend:", StringComparison.Ordinal))
        {
            var parts = baseKey.Split(":resend:");
            _ = int.TryParse(parts[^1], out resendCount);
            baseKey = parts[0];
        }

        if (resendCount >= AutomationPolicy.MaxResend)
            return ApiResponse<object>.FailResponse("Resend limit reached.");

        var newId = await trigger.RaiseAsync(new AutomationEventRequest(
            tenantId,
            evt.EventType,
            evt.BookingId,
            evt.TripId,
            $"{baseKey}:resend:{resendCount + 1}",
            DateTime.UtcNow,
            evt.PayloadJson), ct: cancellationToken);

        return ApiResponse<object>.SuccessResponse(new { eventId = newId });
    }
}

public record RecordWhatsAppConsentCommand(string WaId, string? Note = null)
    : IRequest<ApiResponse<WhatsAppConsentDto>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "WhatsAppContactConsent";
    public int? AuditEntityId => null;
}

public class RecordWhatsAppConsentCommandHandler(
    IWhatsAppAutomationRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<RecordWhatsAppConsentCommand, ApiResponse<WhatsAppConsentDto>>
{
    public async Task<ApiResponse<WhatsAppConsentDto>> Handle(
        RecordWhatsAppConsentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await repository.UpsertConsentOptInAsync(
            tenantId, request.WaId, "Agent", request.Note, currentUser.UserId, cancellationToken);
        var consent = await repository.GetConsentAsync(tenantId, request.WaId, cancellationToken);
        return ApiResponse<WhatsAppConsentDto>.SuccessResponse(consent!);
    }
}

public record OptOutWhatsAppConsentCommand(string WaId) : IRequest<ApiResponse<object>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "WhatsAppContactConsent";
    public int? AuditEntityId => null;
}

public class OptOutWhatsAppConsentCommandHandler(
    IWhatsAppAutomationRepository repository,
    ITenantContext tenantContext)
    : IRequestHandler<OptOutWhatsAppConsentCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(
        OptOutWhatsAppConsentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await repository.OptOutAsync(tenantId, request.WaId, cancellationToken);
        return ApiResponse<object>.SuccessResponse(new { optedOut = true });
    }
}
