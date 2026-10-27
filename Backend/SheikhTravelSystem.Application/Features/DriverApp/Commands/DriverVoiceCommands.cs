using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Application.Features.DriverApp.Services;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverApp.Commands;

public record SubmitDriverVoiceCommand(
    string Transcript,
    string LanguageCode,
    string? Intent,
    string? ClientOpId,
    double? Latitude,
    double? Longitude) : IRequest<ApiResponse<DriverVoiceCommandResultDto>>;

public class SubmitDriverVoiceCommandValidator : AbstractValidator<SubmitDriverVoiceCommand>
{
    private static readonly HashSet<string> AllowedLang = new(StringComparer.OrdinalIgnoreCase)
        { "en", "ar", "de" };

    public SubmitDriverVoiceCommandValidator()
    {
        RuleFor(x => x.Transcript).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.LanguageCode)
            .Must(c => AllowedLang.Contains(c.Trim()))
            .WithMessage("languageCode must be en, ar, or de.");
        RuleFor(x => x.ClientOpId).MaximumLength(64);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
    }
}

public class SubmitDriverVoiceCommandHandler(
    IDriverMobileSyncRepository syncRepository,
    IDriverAppRepository driverRepository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IMediator mediator,
    INotificationDecisionEngine decisionEngine)
    : IRequestHandler<SubmitDriverVoiceCommand, ApiResponse<DriverVoiceCommandResultDto>>
{
    public async Task<ApiResponse<DriverVoiceCommandResultDto>> Handle(
        SubmitDriverVoiceCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverVoiceCommandResultDto>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var (_, replay) = await DriverMobileIdempotencyHelper.TryReplayAsync<DriverVoiceCommandResultDto>(
            syncRepository, tenantId, request.ClientOpId, cancellationToken);
        if (replay != null) return replay;

        var transcript = request.Transcript.Trim();
        var intent = DriverVoiceIntentParser.Parse(transcript, request.Intent);
        var intentName = intent.ToString();

        var active = await driverRepository.GetStartedBookingForSosAsync(driverId.Value, tenantId, cancellationToken);
        var vehicleId = active.VehicleId;
        var bookingId = active.BookingId;
        var createdAt = DateTime.UtcNow;

        int? sosId = null;
        if (intent == DriverVoiceIntent.Sos)
        {
            var sosKey = string.IsNullOrWhiteSpace(request.ClientOpId)
                ? null
                : $"{request.ClientOpId.Trim()}:sos";
            var (_, sosReplay) = await DriverMobileIdempotencyHelper.TryReplayAsync<DriverSosResultDto>(
                syncRepository, tenantId, sosKey, cancellationToken);
            if (sosReplay?.Data != null)
            {
                sosId = sosReplay.Data.Id;
            }
            else
            {
                var sos = await mediator.Send(
                    new DriverSosCommand(request.Latitude, request.Longitude, transcript),
                    cancellationToken);
                if (!sos.Success)
                    return ApiResponse<DriverVoiceCommandResultDto>.FailResponse(sos.Message ?? "SOS failed.");
                sosId = sos.Data?.Id;
                if (sos.Success && sos.Data != null)
                {
                    await DriverMobileIdempotencyHelper.PersistAsync(
                        syncRepository, tenantId, driverId.Value, sosKey,
                        "sos", sos, 200, cancellationToken);
                }
            }
        }

        var driver = await driverRepository.GetDriverNamePhoneAsync(driverId.Value, tenantId, cancellationToken);
        var driverLabel = driver?.FullName ?? "Driver";

        var eventType = intent switch
        {
            DriverVoiceIntent.Sos => "sos",
            DriverVoiceIntent.Alert => "driver_voice_alert",
            _ => "driver_voice_message",
        };

        if (intent != DriverVoiceIntent.Sos)
        {
            var title = intent == DriverVoiceIntent.Alert
                ? $"Driver alert — {driverLabel}"
                : $"Driver message — {driverLabel}";
            var lang = request.LanguageCode.Trim().ToLowerInvariant();
            var body = $"[{lang.ToUpperInvariant()} voice] {transcript}";

            await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                eventType, title, body,
                intent == DriverVoiceIntent.Alert ? NotificationType.Sos : NotificationType.TripUpdated,
                ReferenceId: null, TenantId: tenantId,
                SuggestedPriority: intent == DriverVoiceIntent.Alert ? 3 : 2,
                RequestedChannels: [NotificationChannels.InApp],
                Broadcast: false), cancellationToken);
        }

        var voiceId = await syncRepository.InsertVoiceCommandAsync(new DriverVoiceCommandInsert(
            tenantId,
            driverId.Value,
            string.IsNullOrWhiteSpace(request.ClientOpId) ? null : request.ClientOpId.Trim(),
            request.LanguageCode.Trim().ToLowerInvariant(),
            transcript,
            intentName,
            vehicleId,
            bookingId,
            request.Latitude,
            request.Longitude,
            sosId,
            createdAt), cancellationToken);

        var result = ApiResponse<DriverVoiceCommandResultDto>.SuccessResponse(
            new DriverVoiceCommandResultDto(voiceId, intentName, sosId, createdAt),
            intent == DriverVoiceIntent.Sos ? "SOS sent from voice." : "Voice command recorded.");

        await DriverMobileIdempotencyHelper.PersistAsync(
            syncRepository, tenantId, driverId.Value, request.ClientOpId,
            "voiceCommand", result, 200, cancellationToken);

        return result;
    }
}
