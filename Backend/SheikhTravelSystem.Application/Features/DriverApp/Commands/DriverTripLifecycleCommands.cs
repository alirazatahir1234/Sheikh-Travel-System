using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Application.Features.Trips;
using SheikhTravelSystem.Application.Features.Trips.Commands;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;
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
    IDriverAppRepository repository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IWhatsAppAutomationHooks automationHooks,
    IMediator mediator)
    : IRequestHandler<DriverAdvanceTripCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DriverAdvanceTripCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<bool>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var trip = await repository.FindDriverTripAsync(request.Id, driverId.Value, tenantId, cancellationToken);

        if (trip is null)
        {
            var ownsBooking = await repository.OwnsBookingForTenantAsync(request.Id, driverId.Value, tenantId, cancellationToken);
            if (!ownsBooking)
                return ApiResponse<bool>.FailResponse("Trip not found or not assigned to you.");

            trip = await EnsureOperationalTripForBookingAsync(request.Id, driverId.Value, tenantId, cancellationToken);
            if (trip is null)
                return ApiResponse<bool>.FailResponse("Could not prepare trip for this booking.");
        }

        await repository.EnsureTripVehicleAsync(trip.Id, trip.BookingId, driverId.Value, tenantId, cancellationToken);
        trip = await repository.GetTripRefAsync(trip.Id, driverId.Value, tenantId, cancellationToken) ?? trip;

        await BootstrapTripIfBookingAlreadyStartedAsync(trip, cancellationToken);
        trip = await repository.GetTripRefAsync(trip.Id, driverId.Value, tenantId, cancellationToken) ?? trip;

        return await AdvanceOperationalTripAsync(trip, request, cancellationToken);
    }

    private async Task<ApiResponse<bool>> AdvanceOperationalTripAsync(
        DriverTripRef trip, DriverAdvanceTripCommand request, CancellationToken cancellationToken)
    {
        var current = (TripStatus)trip.Status;
        var target = MapAction(current, request.Action);
        if (target is null)
        {
            if (request.Action == DriverTripAction.Accept &&
                current is TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute)
                return ApiResponse<bool>.SuccessResponse(true, "Trip is already accepted.");

            if (request.Action == DriverTripAction.Arrived &&
                current is TripStatus.AtPickup or TripStatus.Enroute)
                return ApiResponse<bool>.SuccessResponse(true, "Already marked arrived at pickup.");

            if (request.Action == DriverTripAction.Onboard && current == TripStatus.Enroute)
                return ApiResponse<bool>.SuccessResponse(true, "Passenger already onboard.");

            if (request.Action == DriverTripAction.Complete && current == TripStatus.Completed)
                return ApiResponse<bool>.SuccessResponse(true, "Trip is already completed.");

            if (request.Action == DriverTripAction.Reject && current == TripStatus.Cancelled)
                return ApiResponse<bool>.SuccessResponse(true, "Trip is already cancelled.");

            return ApiResponse<bool>.FailResponse(
                $"Action {request.Action} is not allowed from status {DriverTripLabels.Name(current)}.");
        }

        if (!TripLifecycle.CanTransition(current, target.Value))
            return ApiResponse<bool>.FailResponse(
                $"Cannot transition from {DriverTripLabels.Name(current)} to {DriverTripLabels.Name(target.Value)}.");

        var result = await mediator.Send(
            new UpdateTripStatusCommand(
                trip.Id, target.Value, Note: $"Driver:{request.Action}",
                CancellationReason: request.Action == DriverTripAction.Reject ? request.Reason : null),
            cancellationToken);

        if (!result.Success)
            return result;

        await SyncLinkedBookingAsync(trip.BookingId, target.Value, request.Reason, cancellationToken);

        try
        {
            var tenantId = tenantContext.GetRequiredTenantId();
            await automationHooks.OnTripStatusChangedAsync(
                tenantId, trip.Id, trip.BookingId, target.Value, cancellationToken);
        }
        catch
        {
            // Non-fatal
        }

        return ApiResponse<bool>.SuccessResponse(true, $"Trip updated to {DriverTripLabels.Name(target.Value)}.");
    }

    private async Task<DriverTripRef?> EnsureOperationalTripForBookingAsync(
        int bookingId, int driverId, int tenantId, CancellationToken cancellationToken)
    {
        var existing = await repository.FindTripByBookingAsync(bookingId, driverId, tenantId, cancellationToken);
        if (existing is not null)
            return existing;

        var create = await mediator.Send(new CreateTripFromBookingCommand(bookingId), cancellationToken);
        if (!create.Success || create.Data <= 0)
            return null;

        return await repository.GetTripRefAsync(create.Data, driverId, tenantId, cancellationToken);
    }

    private async Task BootstrapTripIfBookingAlreadyStartedAsync(DriverTripRef trip, CancellationToken cancellationToken)
    {
        if (!trip.BookingId.HasValue) return;
        var bookingStatus = await repository.GetBookingStatusAsync(trip.BookingId.Value, cancellationToken);
        if (bookingStatus != (int)BookingStatus.Started) return;
        var current = (TripStatus)trip.Status;
        if (current >= TripStatus.Started) return;
        await mediator.Send(new UpdateTripStatusCommand(trip.Id, TripStatus.Started, Note: "Driver:Bootstrap"), cancellationToken);
    }

    private async Task SyncLinkedBookingAsync(int? bookingId, TripStatus tripStatus, string? reason, CancellationToken cancellationToken)
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
        await repository.SyncLinkedBookingStatusAsync(
            bookingId.Value, (int)bookingStatus.Value, (int)BookingStatus.Cancelled, reason, cancellationToken);
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
