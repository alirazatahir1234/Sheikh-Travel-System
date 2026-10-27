using System.Text.Json;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.FatigueDetection;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Fatigue;

public sealed class FatigueAlertOrchestrator(
    IFatigueDetectionRepository repository,
    INotificationDecisionEngine decisionEngine) : IFatigueAlertOrchestrator
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<ApiResponse<IngestFatigueSignalResultDto>> IngestAsync(
        int tenantId,
        int driverId,
        IngestFatigueSignalRequest request,
        CancellationToken ct = default)
    {
        var settings = await repository.GetOrCreateSettingsAsync(tenantId, ct);
        var trip = await repository.GetActiveTripContextAsync(tenantId, driverId, ct);
        var vehicleId = request.VehicleId ?? trip.VehicleId;
        var bookingId = trip.BookingId;

        var obd = new FatigueObdSupportingSignals(
            request.ObdSpeedKmh,
            request.ObdHardBrakingEvents15Min,
            request.ObdProlongedHighSpeed);

        var driverRisk = await repository.GetDriverRiskScoreAsync(tenantId, driverId, ct);
        var composite = FatigueRiskScoring.ComputeComposite(request.Confidence, driverRisk, obd);
        var supportingJson = JsonSerializer.Serialize(new
        {
            obd = obd,
            camera = new { request.EarScore, request.ExpressionHint, request.Source }
        }, JsonOpts);

        var metrics = new FatigueSignalMetrics(
            request.SignalType.Trim(),
            request.Confidence ?? composite,
            request.EarScore,
            request.ExpressionHint,
            request.Latitude,
            request.Longitude,
            request.PayloadJson);

        var openId = await repository.GetOpenEventIdForDriverAsync(tenantId, driverId, ct);
        int eventId;
        DateTime ackDeadline;

        if (openId is int existing)
        {
            eventId = existing;
            ackDeadline = DateTime.UtcNow.AddSeconds(settings.AckWindowSeconds);
            await repository.TouchOpenEventAsync(tenantId, existing, metrics, ct);
        }
        else
        {
            ackDeadline = DateTime.UtcNow.AddSeconds(settings.AckWindowSeconds);
            eventId = await repository.InsertEventAsync(new FatigueEventInsertRow(
                tenantId,
                driverId,
                vehicleId,
                bookingId,
                string.IsNullOrWhiteSpace(request.Source) ? "external_api" : request.Source.Trim(),
                metrics,
                ackDeadline,
                driverRisk,
                composite,
                supportingJson), ct);
        }

        var driver = await repository.GetDriverContactAsync(tenantId, driverId, ct);
        var driverUserId = await repository.GetDriverUserIdAsync(tenantId, driverId, ct);
        var name = driver?.FullName ?? $"Driver #{driverId}";
        var loc = request.Latitude.HasValue && request.Longitude.HasValue
            ? $" at {request.Latitude:F5},{request.Longitude:F5}"
            : "";
        var detail = BuildSignalDetail(metrics, composite, driverRisk);
        var adminTitle = $"Fatigue — {name}";
        var adminMessage =
            $"{name} ({driver?.Phone ?? "no phone"}) — {request.SignalType}{detail}{loc}. " +
            $"Composite risk {composite:P0}. Ack window {settings.AckWindowSeconds}s.";

        var adminEventType = (request.Source ?? "").Contains("obd", StringComparison.OrdinalIgnoreCase)
            ? "driver_fatigue_obd"
            : "driver_fatigue";

        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            adminEventType,
            adminTitle,
            adminMessage,
            NotificationType.DriverFatigue,
            ReferenceId: eventId,
            TenantId: tenantId,
            SuggestedPriority: 4,
            RequestedChannels: [NotificationChannels.InApp, NotificationChannels.Push, NotificationChannels.Email]),
            ct);

        if (driverUserId is int uid)
        {
            await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                "driver_fatigue_prompt",
                "Fatigue check — tap to confirm",
                "Possible drowsiness detected. Open the driver app and confirm you are alert.",
                NotificationType.DriverFatigue,
                ReferenceId: eventId,
                TenantId: tenantId,
                TargetUserId: uid,
                SuggestedPriority: 4,
                RequestedChannels: [NotificationChannels.Push, NotificationChannels.InApp]),
                ct);
        }

        return ApiResponse<IngestFatigueSignalResultDto>.SuccessResponse(
            new IngestFatigueSignalResultDto(eventId, openId.HasValue ? "Updated" : "Open", ackDeadline),
            "Fatigue signal recorded.");
    }

    private static string BuildSignalDetail(FatigueSignalMetrics metrics, decimal composite, decimal? driverRisk)
    {
        var parts = new List<string>();
        if (metrics.EarScore is decimal ear)
            parts.Add($"EAR {ear:F3}");
        if (metrics.Confidence is decimal conf)
            parts.Add($"conf {conf:P0}");
        parts.Add($"composite {composite:P0}");
        if (driverRisk is decimal dr)
            parts.Add($"driver_risk {dr:P0}");
        if (!string.IsNullOrWhiteSpace(metrics.ExpressionHint))
            parts.Add(metrics.ExpressionHint);
        return parts.Count == 0 ? "" : " (" + string.Join(", ", parts) + ")";
    }
}
