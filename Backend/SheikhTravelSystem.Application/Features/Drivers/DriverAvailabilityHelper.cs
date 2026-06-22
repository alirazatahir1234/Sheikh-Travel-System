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

    public static string BucketSqlExpression => """
        CASE
            WHEN d.IsActive = 0 OR d.Status IN (@Suspended, @OnLeave) OR d.LicenseExpiryDate < CAST(GETUTCDATE() AS DATE)
                THEN N'Unavailable'
            WHEN d.Status = @OnTrip THEN N'OnTrip'
            WHEN EXISTS (
                SELECT 1 FROM AssignmentHistory ah
                WHERE ah.DriverId = d.Id AND ah.IsDeleted = 0 AND ah.Status = N'Active'
            ) THEN N'Busy'
            WHEN d.Status IN (@Available, @OffDuty) THEN N'Available'
            ELSE N'Unavailable'
        END
        """;
}
