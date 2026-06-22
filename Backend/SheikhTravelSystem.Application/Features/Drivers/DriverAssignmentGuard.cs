using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Drivers;

public static class DriverAssignmentGuard
{
    public static void EnsureAssignable(bool isActive, DriverStatus status, string? verificationStatus, DateTime licenseExpiry)
    {
        if (!isActive)
            throw new ConflictException("Driver account is inactive.");

        if (status is DriverStatus.Suspended or DriverStatus.OnLeave)
            throw new ConflictException($"Driver is {status} and cannot be assigned.");

        if (!string.Equals(verificationStatus, "Verified", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Driver must be verified before vehicle assignment.");

        if (licenseExpiry < DateTime.UtcNow.Date)
            throw new ConflictException("Driver license is expired.");
    }

    public static bool CanManuallySetStatus(DriverStatus target)
        => target != DriverStatus.OnTrip;

    public static void EnsureManualStatusAllowed(DriverStatus target)
    {
        if (!CanManuallySetStatus(target))
            throw new ConflictException("On Trip status is set automatically when a trip starts.");
    }
}
