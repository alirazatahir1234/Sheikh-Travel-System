using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Drivers;

public enum DriverAvailabilityBucket
{
    Available,
    Busy,
    OnTrip,
    Unavailable
}

public static class DriverAvailabilityHelper
{
    public static DriverAvailabilityBucket Compute(
        bool isActive,
        DriverStatus status,
        bool hasActiveAssignment,
        bool licenseExpired)
    {
        if (!isActive || status is DriverStatus.Suspended or DriverStatus.OnLeave || licenseExpired)
            return DriverAvailabilityBucket.Unavailable;

        if (status == DriverStatus.OnTrip)
            return DriverAvailabilityBucket.OnTrip;

        if (hasActiveAssignment)
            return DriverAvailabilityBucket.Busy;

        if (status is DriverStatus.Available or DriverStatus.OffDuty)
            return DriverAvailabilityBucket.Available;

        return DriverAvailabilityBucket.Unavailable;
    }
}
