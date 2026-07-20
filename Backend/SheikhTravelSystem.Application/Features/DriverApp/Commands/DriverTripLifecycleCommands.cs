using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Application.Features.Trips;
using SheikhTravelSystem.Application.Features.Trips.Commands;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverApp.Commands;

/// <summary>
/// Driver-facing lifecycle actions mapped onto ERP <see cref="TripStatus"/>.
/// Accept → Started (driving to pickup)
/// Arrived → AtPickup
/// Onboard → Enroute
/// Complete → Completed
/// Reject → Cancelled
/// </summary>
public enum DriverTripAction
{
    Accept = 1,
    Arrived = 2,
    Onboard = 3,
    Complete = 4,
    Reject = 5
}

public record DriverAdvanceTripCommand(int Id, DriverTripAction Action, string? Reason = null)
    : IRequest<ApiResponse<bool>>;

public class DriverAdvanceTripCommandValidator : AbstractValidator<DriverAdvanceTripCommand>
{
    public DriverAdvanceTripCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.Reason)
            .NotEmpty()
            .When(x => x.Action == DriverTripAction.Reject)
            .WithMessage("Rejection reason is required.");
    }
}

public class DriverAdvanceTripCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IMediator mediator)
    : IRequestHandler<DriverAdvanceTripCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DriverAdvanceTripCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<bool>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        // Prefer operational Trips row owned by this driver (by Trip.Id or BookingId).
        var trip = await connection.QuerySingleOrDefaultAsync<TripRef>(new CommandDefinition(
            @"SELECT TOP 1 Id, Status, BookingId, VehicleId
              FROM Trips
              WHERE TenantId = @TenantId AND DriverId = @DriverId AND IsDeleted = 0
                AND (Id = @Id OR BookingId = @Id)
              ORDER BY CASE WHEN Id = @Id THEN 0 ELSE 1 END, Id DESC",
            new { Id = request.Id, DriverId = driverId.Value, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (trip is not null)
            return await AdvanceOperationalTripAsync(trip, request, cancellationToken);

        // Legacy booking-only assignment (no Trips row yet).
        var ownsBooking = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM Bookings
                WHERE Id = @Id AND DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
              ) THEN 1 ELSE 0 END",
            new { request.Id, DriverId = driverId.Value, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (!ownsBooking)
            return ApiResponse<bool>.FailResponse("Trip not found or not assigned to you.");

        return await AdvanceLegacyBookingAsync(request, cancellationToken);
    }

    private async Task<ApiResponse<bool>> AdvanceOperationalTripAsync(
        TripRef trip, DriverAdvanceTripCommand request, CancellationToken cancellationToken)
    {
        var current = (TripStatus)trip.Status;
        var target = MapAction(current, request.Action);
        if (target is null)
            return ApiResponse<bool>.FailResponse(
                $"Action {request.Action} is not allowed from status {DriverTripLabels.Name(current)}.");

        if (!TripLifecycle.CanTransition(current, target.Value))
            return ApiResponse<bool>.FailResponse(
                $"Cannot transition from {DriverTripLabels.Name(current)} to {DriverTripLabels.Name(target.Value)}.");

        var result = await mediator.Send(
            new UpdateTripStatusCommand(
                trip.Id,
                target.Value,
                Note: $"Driver:{request.Action}",
                CancellationReason: request.Action == DriverTripAction.Reject ? request.Reason : null),
            cancellationToken);

        if (!result.Success)
            return result;

        await SyncLinkedBookingAsync(trip.BookingId, target.Value, request.Reason, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, $"Trip updated to {DriverTripLabels.Name(target.Value)}.");
    }

    private async Task<ApiResponse<bool>> AdvanceLegacyBookingAsync(
        DriverAdvanceTripCommand request, CancellationToken cancellationToken)
    {
        var bookingStatus = request.Action switch
        {
            DriverTripAction.Accept or DriverTripAction.Arrived or DriverTripAction.Onboard
                => BookingStatus.Started,
            DriverTripAction.Complete => BookingStatus.Completed,
            DriverTripAction.Reject => BookingStatus.Cancelled,
            _ => (BookingStatus?)null
        };

        if (bookingStatus is null)
            return ApiResponse<bool>.FailResponse("Unsupported action.");

        return request.Action switch
        {
            DriverTripAction.Accept or DriverTripAction.Arrived or DriverTripAction.Onboard
                => await mediator.Send(new DriverStartTripCommand(request.Id), cancellationToken),
            DriverTripAction.Complete
                => await mediator.Send(new DriverCompleteTripCommand(request.Id), cancellationToken),
            DriverTripAction.Reject
                => await mediator.Send(new DriverRejectTripCommand(request.Id, request.Reason ?? "Rejected"), cancellationToken),
            _ => ApiResponse<bool>.FailResponse("Unsupported action.")
        };
    }

    private async Task SyncLinkedBookingAsync(
        int? bookingId, TripStatus tripStatus, string? reason, CancellationToken cancellationToken)
    {
        if (!bookingId.HasValue) return;

        var bookingStatus = tripStatus switch
        {
            TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute or TripStatus.Delayed
                => BookingStatus.Started,
            TripStatus.Completed => BookingStatus.Completed,
            TripStatus.Cancelled or TripStatus.Failed => BookingStatus.Cancelled,
            _ => (BookingStatus?)null
        };
        if (bookingStatus is null) return;

        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Bookings SET Status = @Status, UpdatedAt = GETUTCDATE(),
                CancellationReason = CASE WHEN @Status = @Cancelled THEN @Reason ELSE CancellationReason END
              WHERE Id = @Id AND IsDeleted = 0",
            new
            {
                Id = bookingId.Value,
                Status = (int)bookingStatus.Value,
                Cancelled = (int)BookingStatus.Cancelled,
                Reason = reason
            },
            cancellationToken: cancellationToken));
    }

    private static TripStatus? MapAction(TripStatus current, DriverTripAction action) => action switch
    {
        DriverTripAction.Accept when current is TripStatus.Scheduled
            or TripStatus.DriverAssigned or TripStatus.VehicleAssigned or TripStatus.Delayed
            => TripStatus.Started,
        DriverTripAction.Arrived when current is TripStatus.Started or TripStatus.Delayed
            => TripStatus.AtPickup,
        DriverTripAction.Onboard when current is TripStatus.AtPickup or TripStatus.Started or TripStatus.Delayed
            => TripStatus.Enroute,
        DriverTripAction.Complete when current is TripStatus.Enroute or TripStatus.Started or TripStatus.Delayed
            => TripStatus.Completed,
        DriverTripAction.Reject when !TripLifecycle.IsTerminal(current)
            => TripStatus.Cancelled,
        _ => null
    };

    private sealed class TripRef
    {
        public int Id { get; init; }
        public int Status { get; init; }
        public int? BookingId { get; init; }
        public int? VehicleId { get; init; }
    }
}

internal static class DriverTripLabels
{
    public static string Name(TripStatus status) => status switch
    {
        TripStatus.Draft => "Draft",
        TripStatus.Scheduled => "Scheduled",
        TripStatus.DriverAssigned => "Assigned",
        TripStatus.VehicleAssigned => "Assigned",
        TripStatus.Started => "Driving to pickup",
        TripStatus.AtPickup => "Arrived at pickup",
        TripStatus.Enroute => "Enroute",
        TripStatus.Delayed => "Delayed",
        TripStatus.Completed => "Completed",
        TripStatus.Cancelled => "Cancelled",
        TripStatus.Failed => "Failed",
        _ => status.ToString()
    };

    public static IReadOnlyList<string> NextActions(TripStatus status) => status switch
    {
        TripStatus.Scheduled or TripStatus.DriverAssigned or TripStatus.VehicleAssigned
            => ["Accept", "Reject"],
        TripStatus.Started => ["Arrived", "Onboard", "Reject"],
        TripStatus.AtPickup => ["Onboard", "Reject"],
        TripStatus.Enroute => ["Complete", "Reject"],
        TripStatus.Delayed => ["Accept", "Arrived", "Onboard", "Complete", "Reject"],
        _ => []
    };

    public static IReadOnlyList<string> NextActionsFromBooking(BookingStatus status) => status switch
    {
        BookingStatus.Confirmed => ["Accept", "Reject"],
        BookingStatus.Started => ["Arrived", "Onboard", "Complete", "Reject"],
        _ => []
    };
}
