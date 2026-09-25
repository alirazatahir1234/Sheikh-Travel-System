using System.Collections.Concurrent;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Automation;

/// <summary>In-memory latest vehicle position for public tracking (3s TTL).</summary>
public static class TrackingPositionCache
{
    private static readonly ConcurrentDictionary<int, (TrackingPositionDto Pos, DateTime Expires)> Store = new();

    public static void Set(int vehicleId, TrackingPositionDto position)
        => Store[vehicleId] = (position, DateTime.UtcNow.AddSeconds(3));

    public static TrackingPositionDto? Get(int vehicleId)
    {
        if (!Store.TryGetValue(vehicleId, out var entry))
            return null;
        if (entry.Expires < DateTime.UtcNow)
        {
            Store.TryRemove(vehicleId, out _);
            return null;
        }
        return entry.Pos;
    }
}

public record GetPublicTrackingQuery(string Token) : IRequest<ApiResponse<TrackingViewDto>>;

public class GetPublicTrackingQueryHandler(
    IWhatsAppAutomationRepository repository,
    ITrackingTokenProtector tokenProtector)
    : IRequestHandler<GetPublicTrackingQuery, ApiResponse<TrackingViewDto>>
{
    private static readonly TrackingBrandDto Brand = new("SheikhGo", "+92 42 000 0000");

    public async Task<ApiResponse<TrackingViewDto>> Handle(
        GetPublicTrackingQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length < 16)
            return ApiResponse<TrackingViewDto>.FailResponse("Not found.");

        var hash = TrackingToken.Hash(request.Token.Trim());
        var link = await repository.GetTrackingLinkByHashAsync(hash, cancellationToken);
        if (link is null || link.RevokedAt is not null || link.ExpiresAt < DateTime.UtcNow)
            return ApiResponse<TrackingViewDto>.FailResponse("Not found.");

        try
        {
            var plain = tokenProtector.Unprotect(link.TokenProtected);
            if (!string.Equals(plain, request.Token.Trim(), StringComparison.Ordinal))
                return ApiResponse<TrackingViewDto>.FailResponse("Not found.");
        }
        catch
        {
            return ApiResponse<TrackingViewDto>.FailResponse("Not found.");
        }

        await repository.IncrementTrackingViewAsync(link.Id, cancellationToken);

        var trip = await repository.GetTripSnapshotAsync(link.TenantId, link.TripId, cancellationToken);
        if (trip is null)
            return ApiResponse<TrackingViewDto>.FailResponse("Not found.");

        var booking = trip.BookingId is > 0
            ? await repository.GetBookingSnapshotAsync(link.TenantId, trip.BookingId.Value, cancellationToken)
            : null;

        var state = MapState((TripStatus)trip.Status);
        TrackingPositionDto? position = null;
        int? eta = null;

        if (state is "EnRoute" or "Arriving" or "Arrived" or "InProgress")
        {
            position = trip.VehicleId is > 0 ? TrackingPositionCache.Get(trip.VehicleId.Value) : null;
            if (position is not null
                && trip.PickupLat is not null && trip.PickupLng is not null
                && state is "EnRoute" or "Arriving")
            {
                var dist = GeoMath.DistanceMeters(
                    position.Lat, position.Lng, trip.PickupLat.Value, trip.PickupLng.Value);
                eta = GeoMath.EstimateEtaMinutes(dist);
            }
        }

        if (state is "Completed" or "Cancelled" or "Assigned")
            position = null;

        return ApiResponse<TrackingViewDto>.SuccessResponse(new TrackingViewDto(
            state,
            booking?.BookingNumber ?? trip.TripNumber,
            trip.DriverFirstName,
            new TrackingVehicleDto(trip.VehicleDescription ?? "Vehicle", trip.PlateNumber ?? "—"),
            new TrackingPlaceDto(trip.PickupAddress, trip.PickupLat, trip.PickupLng, trip.PickupAt),
            new TrackingPlaceDto(trip.DropoffAddress, trip.DropoffLat, trip.DropoffLng, null),
            position,
            eta,
            5,
            Brand));
    }

    private static string MapState(TripStatus status) => status switch
    {
        TripStatus.DriverAssigned or TripStatus.VehicleAssigned or TripStatus.Scheduled => "Assigned",
        TripStatus.Started => "EnRoute",
        TripStatus.AtPickup => "Arrived",
        TripStatus.Enroute or TripStatus.Delayed => "InProgress",
        TripStatus.Completed => "Completed",
        TripStatus.Cancelled or TripStatus.Failed => "Cancelled",
        _ => "Assigned"
    };
}

public record RecordTripRatingCommand(
    int TenantId,
    int TripId,
    int Score,
    string SenderWaId,
    int? MessageId = null) : IRequest<ApiResponse<object>>;

public class RecordTripRatingCommandHandler(IWhatsAppAutomationRepository repository)
    : IRequestHandler<RecordTripRatingCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(
        RecordTripRatingCommand request, CancellationToken cancellationToken)
    {
        if (request.Score is not (1 or 3 or 5))
            return ApiResponse<object>.FailResponse("Invalid score.");

        var trip = await repository.GetTripSnapshotAsync(request.TenantId, request.TripId, cancellationToken);
        if (trip is null)
            return ApiResponse<object>.FailResponse("Trip not found.");

        var booking = trip.BookingId is > 0
            ? await repository.GetBookingSnapshotAsync(request.TenantId, trip.BookingId.Value, cancellationToken)
            : null;
        var expected = AutomationMessageComposer.ResolveRecipient(booking, trip);
        if (string.IsNullOrWhiteSpace(expected))
            return ApiResponse<object>.FailResponse("No recipient.");

        var senderDigits = new string(request.SenderWaId.Where(char.IsDigit).ToArray());
        var expectedDigits = new string(expected.Where(char.IsDigit).ToArray());
        if (!string.Equals(senderDigits, expectedDigits, StringComparison.Ordinal)
            && !expectedDigits.EndsWith(senderDigits, StringComparison.Ordinal)
            && !senderDigits.EndsWith(expectedDigits, StringComparison.Ordinal))
            return ApiResponse<object>.FailResponse("Sender mismatch.");

        var inserted = await repository.TryInsertTripRatingAsync(
            request.TenantId, request.TripId, trip.DriverId, request.Score, request.MessageId, cancellationToken);

        return ApiResponse<object>.SuccessResponse(new { recorded = inserted, score = request.Score });
    }
}
