using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.Commands;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Automation;

public record ProcessAutomationEventCommand(long EventId) : IRequest<ApiResponse<object>>;

public class ProcessAutomationEventCommandHandler(
    IWhatsAppAutomationRepository automationRepo,
    IWhatsAppTemplateRepository templateRepo,
    IWhatsAppInboxRepository inboxRepo,
    IWhatsAppAccountResolver accountResolver,
    IMediator mediator,
    ITrackingTokenProtector tokenProtector,
    ITenantContext tenantContext,
    IOptions<WhatsAppOptions> whatsAppOptions,
    IWhatsAppRealtimePublisher realtime,
    ILogger<ProcessAutomationEventCommandHandler> logger)
    : IRequestHandler<ProcessAutomationEventCommand, ApiResponse<object>>
{
    private readonly AutomationMessageComposer _composer = new();

    public async Task<ApiResponse<object>> Handle(
        ProcessAutomationEventCommand request, CancellationToken cancellationToken)
    {
        var evt = await automationRepo.GetEventAsync(request.EventId, cancellationToken);
        if (evt is null)
            return ApiResponse<object>.FailResponse("Event not found.");

        tenantContext.SetTenant(evt.TenantId);

        if (evt.Status is WaAutomationEventStatus.Sent or WaAutomationEventStatus.Skipped or WaAutomationEventStatus.Cancelled)
            return ApiResponse<object>.SuccessResponse(new { skipped = true, reason = "terminal" });

        if (!await automationRepo.ClaimEventAsync(request.EventId, TimeSpan.FromMinutes(2), cancellationToken))
            return ApiResponse<object>.SuccessResponse(new { skipped = true, reason = "locked" });

        try
        {
            var rule = await automationRepo.GetRuleAsync(evt.TenantId, evt.EventType, cancellationToken);
            if (rule is null || !rule.IsEnabled)
            {
                await automationRepo.MarkEventSkippedAsync(request.EventId, WaAutomationSkipReason.RuleDisabled, cancellationToken);
                return ApiResponse<object>.SuccessResponse(new { skipped = true, reason = WaAutomationSkipReason.RuleDisabled });
            }

            // Staleness for arrival events
            if (evt.EventType is WaAutomationEventType.DriverArriving or WaAutomationEventType.DriverArrived
                && DateTime.UtcNow - evt.DueAt > AutomationPolicy.ArrivalStaleAfter)
            {
                await automationRepo.MarkEventSkippedAsync(request.EventId, WaAutomationSkipReason.Stale, cancellationToken);
                return ApiResponse<object>.SuccessResponse(new { skipped = true, reason = WaAutomationSkipReason.Stale });
            }

            AutomationBookingSnapshot? booking = null;
            if (evt.BookingId is > 0)
                booking = await automationRepo.GetBookingSnapshotAsync(evt.TenantId, evt.BookingId.Value, cancellationToken);

            AutomationTripSnapshot? trip = null;
            if (evt.TripId is > 0)
                trip = await automationRepo.GetTripSnapshotAsync(evt.TenantId, evt.TripId.Value, cancellationToken);
            else if (booking is not null)
            {
                // optional: leave trip null
            }

            if (booking is not null && evt.BookingId is > 0 && booking.Id != evt.BookingId)
            {
                await automationRepo.MarkEventSkippedAsync(request.EventId, WaAutomationSkipReason.NotRelevant, cancellationToken);
                return ApiResponse<object>.FailResponse("Tenant mismatch.");
            }

            if (!AutomationMessageComposer.IsRelevant(evt.EventType, booking, trip, DateTime.UtcNow))
            {
                var reason = booking?.Status == (int)BookingStatus.Cancelled
                    ? WaAutomationSkipReason.BookingCancelled
                    : WaAutomationSkipReason.NotRelevant;
                await automationRepo.MarkEventSkippedAsync(request.EventId, reason, cancellationToken);
                return ApiResponse<object>.SuccessResponse(new { skipped = true, reason });
            }

            // Quiet hours for non-urgent: reschedule instead of drop
            if (!rule.IsUrgent)
            {
                var tz = ResolveTenantTimeZone();
                var adjusted = AutomationPolicy.ApplyQuietHours(evt.DueAt, tz, rule.IsUrgent);
                if (adjusted > DateTime.UtcNow.AddMinutes(1))
                {
                    await automationRepo.RescheduleEventAsync(request.EventId, adjusted, cancellationToken);
                    return ApiResponse<object>.SuccessResponse(new { rescheduled = true, dueAt = adjusted });
                }
            }

            var recipient = AutomationMessageComposer.ResolveRecipient(booking, trip);
            if (string.IsNullOrWhiteSpace(recipient))
            {
                await automationRepo.MarkEventSkippedAsync(request.EventId, WaAutomationSkipReason.NoRecipient, cancellationToken);
                return ApiResponse<object>.SuccessResponse(new { skipped = true, reason = WaAutomationSkipReason.NoRecipient });
            }

            var waId = WhatsAppPhone.ToApiDigits(recipient);
            var consented = await automationRepo.HasActiveOptInAsync(evt.TenantId, waId, cancellationToken);
            if (!consented)
            {
                var consent = await automationRepo.GetConsentAsync(evt.TenantId, waId, cancellationToken);
                var reason = consent?.OptOutAt is not null
                    ? WaAutomationSkipReason.OptedOut
                    : WaAutomationSkipReason.NoConsent;
                await automationRepo.MarkEventSkippedAsync(request.EventId, reason, cancellationToken);
                return ApiResponse<object>.SuccessResponse(new { skipped = true, reason });
            }

            string? trackingToken = null;
            if (NeedsTrackingLink(evt.EventType) && (evt.TripId is > 0 || trip?.Id > 0))
            {
                var tripId = evt.TripId ?? trip!.Id;
                trackingToken = await EnsureTrackingTokenAsync(
                    evt.TenantId, tripId, evt.BookingId ?? trip?.BookingId, cancellationToken);
            }

            if (evt.EventType == WaAutomationEventType.BookingCancelled && evt.TripId is > 0)
                await automationRepo.RevokeTrackingLinksForTripAsync(evt.TripId.Value, cancellationToken);

            var language = AutomationMessageComposer.ResolveLanguage(booking, "en");
            var templateName = rule.TemplateName ?? evt.EventType.ToLowerInvariant();
            var template = await templateRepo.GetByNameAsync(
                evt.TenantId, rule.AccountId ?? 0, templateName, language, cancellationToken);

            // Fallback: find any approved template by name on account
            if (template is null && rule.AccountId is > 0)
            {
                if (!string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
                {
                    template = await templateRepo.GetByNameAsync(
                        evt.TenantId, rule.AccountId.Value, templateName, "en", cancellationToken);
                    if (template is not null)
                    {
                        language = "en";
                        logger.LogInformation("WhatsApp automation falling back to en template {Name}", templateName);
                    }
                }
            }

            // If AccountId unset, resolve default PK account then look up template
            WhatsAppAccountRow? account = null;
            if (rule.AccountId is > 0)
                account = await inboxRepo.GetAccountByIdAsync(evt.TenantId, rule.AccountId.Value, cancellationToken);
            account ??= await accountResolver.ResolveAsync(evt.TenantId, null, recipient, cancellationToken);

            if (account is null)
            {
                await automationRepo.MarkEventFailedAsync(request.EventId, "No WhatsApp account", cancellationToken);
                return ApiResponse<object>.FailResponse("No WhatsApp account");
            }

            if (template is null)
            {
                template = await templateRepo.GetByNameAsync(
                    evt.TenantId, account.Id, templateName, language, cancellationToken);
                if (template is null && !string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
                {
                    template = await templateRepo.GetByNameAsync(
                        evt.TenantId, account.Id, templateName, "en", cancellationToken);
                    language = "en";
                }
            }

            if (template is null
                || !string.Equals(template.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                await automationRepo.MarkEventSkippedAsync(
                    request.EventId, WaAutomationSkipReason.TemplateMissing, cancellationToken);
                return ApiResponse<object>.SuccessResponse(new { skipped = true, reason = WaAutomationSkipReason.TemplateMissing });
            }

            var composed = _composer.Compose(
                evt.EventType, booking, trip, language, trackingToken, template.Name);
            if (composed is null)
            {
                await automationRepo.MarkEventFailedAsync(request.EventId, "Composer failed", cancellationToken);
                return ApiResponse<object>.FailResponse("Composer failed");
            }

            var send = await mediator.Send(new SendWhatsAppTemplateCommand(
                ConversationId: null,
                WhatsAppAccountId: account.Id,
                RecipientPhoneNumber: recipient,
                TemplateName: composed.TemplateName,
                Language: composed.Language,
                BodyParameters: composed.BodyParameters,
                UrlButtonSuffix: composed.UrlButtonSuffix,
                QuickReplyPayloads: composed.QuickReplyPayloads,
                AutomationEventId: request.EventId), cancellationToken);

            if (!send.Success || send.Data is null)
            {
                await automationRepo.MarkEventFailedAsync(
                    request.EventId, send.Message ?? "Send failed", cancellationToken);
                return ApiResponse<object>.FailResponse(send.Message ?? "Send failed");
            }

            await automationRepo.LinkMessageToAutomationAsync(send.Data.MessageId, request.EventId, cancellationToken);
            await automationRepo.MarkEventSentAsync(request.EventId, send.Data.MessageId, recipient, cancellationToken);

            await realtime.PublishAsync(evt.TenantId, new
            {
                type = "whatsapp.automation_event_updated",
                eventId = request.EventId,
                bookingId = evt.BookingId,
                tripId = evt.TripId,
                eventType = evt.EventType,
                status = WaAutomationEventStatus.Sent,
                messageId = send.Data.MessageId
            }, cancellationToken);

            // Optional booker copy
            if (rule.NotifyBooker && !string.IsNullOrWhiteSpace(booking?.BookerPhone)
                && !string.Equals(WhatsAppPhone.ToApiDigits(booking.BookerPhone), waId, StringComparison.Ordinal))
            {
                // Fire-and-forget secondary send without failing primary
                try
                {
                    await mediator.Send(new SendWhatsAppTemplateCommand(
                        null, account.Id, booking.BookerPhone, composed.TemplateName, composed.Language,
                        composed.BodyParameters, composed.UrlButtonSuffix, null, request.EventId), cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Booker copy send failed for event {EventId}", request.EventId);
                }
            }

            return ApiResponse<object>.SuccessResponse(new
            {
                sent = true,
                messageId = send.Data.MessageId
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ProcessAutomationEvent failed for {EventId}", request.EventId);
            await automationRepo.MarkEventFailedAsync(request.EventId, Truncate(ex.Message, 1900), cancellationToken);
            return ApiResponse<object>.FailResponse(ex.Message);
        }
    }

    private async Task<string?> EnsureTrackingTokenAsync(
        int tenantId, int tripId, int? bookingId, CancellationToken ct)
    {
        var existing = await automationRepo.GetActiveTrackingLinkAsync(tripId, ct);
        if (existing is not null && existing.ExpiresAt > DateTime.UtcNow && existing.RevokedAt is null)
        {
            try
            {
                return tokenProtector.Unprotect(existing.TokenProtected);
            }
            catch
            {
                await automationRepo.RevokeTrackingLinksForTripAsync(tripId, ct);
            }
        }

        var token = TrackingToken.Generate();
        var hash = TrackingToken.Hash(token);
        var protectedToken = tokenProtector.Protect(token);
        var expires = DateTime.UtcNow.Add(AutomationPolicy.TrackingHardCap);
        await automationRepo.InsertTrackingLinkAsync(
            tenantId, tripId, bookingId, hash, protectedToken, expires, ct);
        return token;
    }

    private static bool NeedsTrackingLink(string eventType)
        => eventType is WaAutomationEventType.DriverAssigned
            or WaAutomationEventType.DriverEnRoute
            or WaAutomationEventType.DriverArriving
            or WaAutomationEventType.DriverArrived;

    private static TimeZoneInfo ResolveTenantTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }

    private static string Truncate(string? v, int max)
        => string.IsNullOrEmpty(v) ? "" : v.Length <= max ? v : v[..max];
}
