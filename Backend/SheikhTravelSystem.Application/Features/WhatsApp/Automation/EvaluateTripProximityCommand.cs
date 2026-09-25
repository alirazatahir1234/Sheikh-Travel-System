using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Automation;

public record EvaluateTripProximityCommand(
    int TenantId,
    int TripId,
    double Lat,
    double Lng,
    double SpeedKmh,
    DateTime FixTimeUtc) : IRequest<ApiResponse<object>>;

public class EvaluateTripProximityCommandHandler(
    IWhatsAppAutomationRepository repository,
    IWhatsAppAutomationTrigger trigger)
    : IRequestHandler<EvaluateTripProximityCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(
        EvaluateTripProximityCommand request, CancellationToken cancellationToken)
    {
        var trip = await repository.GetTripSnapshotAsync(request.TenantId, request.TripId, cancellationToken);
        if (trip is null)
            return ApiResponse<object>.SuccessResponse(new { skipped = true });

        if (trip.Status != (int)TripStatus.Started)
            return ApiResponse<object>.SuccessResponse(new { skipped = true, reason = "not_en_route_to_pickup" });

        if (trip.PickupLat is null || trip.PickupLng is null)
            return ApiResponse<object>.SuccessResponse(new { skipped = true, reason = "no_pickup_coords" });

        var distance = GeoMath.DistanceMeters(
            request.Lat, request.Lng, trip.PickupLat.Value, trip.PickupLng.Value);

        if (ProximityRules.IsArrived(distance, request.SpeedKmh))
        {
            await trigger.RaiseAsync(new AutomationEventRequest(
                request.TenantId,
                WaAutomationEventType.DriverArrived,
                trip.BookingId,
                trip.Id,
                AutomationPolicy.DedupeKey(WaAutomationEventType.DriverArrived, trip.Id)), ct: cancellationToken);
            return ApiResponse<object>.SuccessResponse(new { raised = WaAutomationEventType.DriverArrived, distance });
        }

        if (ProximityRules.IsArriving(distance))
        {
            await trigger.RaiseAsync(new AutomationEventRequest(
                request.TenantId,
                WaAutomationEventType.DriverArriving,
                trip.BookingId,
                trip.Id,
                AutomationPolicy.DedupeKey(WaAutomationEventType.DriverArriving, trip.Id)), ct: cancellationToken);
            return ApiResponse<object>.SuccessResponse(new { raised = WaAutomationEventType.DriverArriving, distance });
        }

        return ApiResponse<object>.SuccessResponse(new { distance });
    }
}
