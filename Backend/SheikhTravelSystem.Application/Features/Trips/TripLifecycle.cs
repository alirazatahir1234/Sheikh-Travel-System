using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Trips;

public static class TripLifecycle
{
    public static bool IsTerminal(TripStatus status)
        => status is TripStatus.Completed or TripStatus.Cancelled or TripStatus.Failed;

    public static bool CanTransition(TripStatus from, TripStatus to) => (from, to) switch
    {
        (TripStatus.Draft, TripStatus.Scheduled) => true,
        (TripStatus.Draft, TripStatus.Cancelled) => true,
        (TripStatus.Scheduled, TripStatus.DriverAssigned) => true,
        (TripStatus.Scheduled, TripStatus.VehicleAssigned) => true,
        (TripStatus.Scheduled, TripStatus.Started) => true,
        (TripStatus.Scheduled, TripStatus.Delayed) => true,
        (TripStatus.Scheduled, TripStatus.Cancelled) => true,
        (TripStatus.DriverAssigned, TripStatus.VehicleAssigned) => true,
        (TripStatus.DriverAssigned, TripStatus.Started) => true,
        (TripStatus.DriverAssigned, TripStatus.Delayed) => true,
        (TripStatus.DriverAssigned, TripStatus.Cancelled) => true,
        (TripStatus.VehicleAssigned, TripStatus.DriverAssigned) => true,
        (TripStatus.VehicleAssigned, TripStatus.Started) => true,
        (TripStatus.VehicleAssigned, TripStatus.Delayed) => true,
        (TripStatus.VehicleAssigned, TripStatus.Cancelled) => true,
        (TripStatus.Started, TripStatus.AtPickup) => true,
        (TripStatus.Started, TripStatus.Enroute) => true,
        (TripStatus.Started, TripStatus.Delayed) => true,
        (TripStatus.Started, TripStatus.Completed) => true,
        (TripStatus.Started, TripStatus.Cancelled) => true,
        (TripStatus.Started, TripStatus.Failed) => true,
        (TripStatus.AtPickup, TripStatus.Enroute) => true,
        (TripStatus.AtPickup, TripStatus.Delayed) => true,
        (TripStatus.AtPickup, TripStatus.Cancelled) => true,
        (TripStatus.Enroute, TripStatus.Delayed) => true,
        (TripStatus.Enroute, TripStatus.Completed) => true,
        (TripStatus.Enroute, TripStatus.Failed) => true,
        (TripStatus.Enroute, TripStatus.Cancelled) => true,
        (TripStatus.Delayed, TripStatus.Started) => true,
        (TripStatus.Delayed, TripStatus.AtPickup) => true,
        (TripStatus.Delayed, TripStatus.Enroute) => true,
        (TripStatus.Delayed, TripStatus.Completed) => true,
        (TripStatus.Delayed, TripStatus.Cancelled) => true,
        (TripStatus.Delayed, TripStatus.Failed) => true,
        _ => false
    };

    public static TripStatus ResolveAssignmentStatus(TripStatus current, bool hasDriver, bool hasVehicle)
    {
        if (IsTerminal(current) || current is TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute or TripStatus.Delayed)
            return current;

        if (hasVehicle)
            return TripStatus.VehicleAssigned;
        if (hasDriver)
            return TripStatus.DriverAssigned;
        return current == TripStatus.Draft ? TripStatus.Draft : TripStatus.Scheduled;
    }
}
