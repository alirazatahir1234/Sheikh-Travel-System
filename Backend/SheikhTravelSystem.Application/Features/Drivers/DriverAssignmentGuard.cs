using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Drivers;

public static class DriverAssignmentGuard
{
    public static void EnsureAssignable(bool isActive, DriverStatus status, string? verificationStatus, DateTime licenseExpiry)
    {
        EnsureCoreAssignable(isActive, status, licenseExpiry);

        if (!string.Equals(verificationStatus, "Verified", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Driver must be verified before vehicle assignment.");
    }

    /// <summary>
    /// Fleet/trip assignment: active driver with valid license; pending verification is allowed.
    /// </summary>
    public static void EnsureAssignableForVehicleTrip(
        bool isActive,
        DriverStatus status,
        string? verificationStatus,
        DateTime licenseExpiry)
    {
        EnsureCoreAssignable(isActive, status, licenseExpiry);
        EnsureVerificationAllowsAssignment(verificationStatus);
    }

    private static void EnsureCoreAssignable(bool isActive, DriverStatus status, DateTime licenseExpiry)
    {
        if (!isActive)
            throw new ConflictException("Driver account is inactive.");

        if (status is DriverStatus.Suspended or DriverStatus.OnLeave)
            throw new ConflictException($"Driver is {status} and cannot be assigned.");

        if (licenseExpiry < DateTime.UtcNow.Date)
            throw new ConflictException("Driver license is expired.");
    }

    private static void EnsureVerificationAllowsAssignment(string? verificationStatus)
    {
        if (string.IsNullOrWhiteSpace(verificationStatus))
            return;

        if (string.Equals(verificationStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Driver verification was rejected.");

        if (string.Equals(verificationStatus, "ExpiredDocs", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Driver documents have expired.");
    }

    public static bool CanManuallySetStatus(DriverStatus target)
        => target != DriverStatus.OnTrip;

    public static void EnsureManualStatusAllowed(DriverStatus target)
    {
        if (!CanManuallySetStatus(target))
            throw new ConflictException("On Trip status is set automatically when a trip starts.");
    }
}
