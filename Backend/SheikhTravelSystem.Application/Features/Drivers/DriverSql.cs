namespace SheikhTravelSystem.Application.Features.Drivers;

internal static class DriverSql
{
    internal const string LicenseExpiryFlags = """
        CASE WHEN d.LicenseExpiryDate < CAST(GETUTCDATE() AS DATE) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS LicenseExpired,
        CASE WHEN d.LicenseExpiryDate >= CAST(GETUTCDATE() AS DATE)
              AND d.LicenseExpiryDate <= DATEADD(day, 30, CAST(GETUTCDATE() AS DATE))
             THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS LicenseExpiringSoon
        """;

    internal const string GpsOnlineFlag = """
        CAST(CASE WHEN av.GpsDeviceId IS NOT NULL AND gd.LastSeenAt >= DATEADD(minute, -15, GETUTCDATE())
             THEN 1 ELSE 0 END AS BIT) AS GpsOnline
        """;

    internal const string AvailabilityBucketColumn = """
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
        END AS AvailabilityBucket
        """;

    internal const string ListSelect = $"""
        d.Id, d.DriverCode, d.FirstName, d.LastName, d.FullName, d.Phone, d.LicenseNumber, d.LicenseExpiryDate,
        {LicenseExpiryFlags},
        d.Nationality, d.Status, d.IsActive, d.VerificationStatus, d.BranchId,
        b.Name AS BranchName,
        d.DepartmentId, dep.Name AS DepartmentName, d.HireDate,
        av.VehicleId AS AssignedVehicleId,
        av.VehicleCode AS AssignedVehicleCode,
        av.RegistrationNumber AS AssignedVehicleRegistration,
        d.Rating,
        {GpsOnlineFlag},
        {AvailabilityBucketColumn},
        d.CreatedAt
        """;

    internal const string ListFrom = """
        FROM Drivers d
        LEFT JOIN Branches b ON b.Id = d.BranchId AND b.IsActive = 1
        LEFT JOIN Departments dep ON dep.Id = d.DepartmentId AND dep.IsActive = 1
        OUTER APPLY (
            SELECT TOP 1 v.Id AS VehicleId, v.VehicleCode, v.RegistrationNumber, v.GpsDeviceId
            FROM AssignmentHistory ah
            INNER JOIN Vehicles v ON v.Id = ah.VehicleId AND v.IsDeleted = 0
            WHERE ah.DriverId = d.Id AND ah.IsDeleted = 0 AND ah.Status = N'Active'
            ORDER BY ah.StartAt DESC
        ) av
        LEFT JOIN GpsDevices gd ON gd.Id = av.GpsDeviceId AND gd.IsDeleted = 0
        """;

    internal const string DetailColumns = $"""
        d.Id, d.DriverCode, d.FirstName, d.LastName, d.FullName, d.Phone, d.LicenseNumber, d.LicenseExpiryDate,
        {LicenseExpiryFlags},
        d.CNIC, d.Address, d.Nationality, d.Email, d.DateOfBirth, d.Gender,
        d.EmergencyContactName, d.EmergencyContact,
        d.HireDate, d.PhotoUrl, d.VerificationStatus,
        d.BranchId, b.Name AS BranchName, d.DepartmentId, dep.Name AS DepartmentName,
        av.VehicleId AS AssignedVehicleId, av.VehicleCode AS AssignedVehicleCode,
        av.RegistrationNumber AS AssignedVehicleRegistration,
        d.Status, d.IsActive, d.Rating, d.YearsExperience,
        {GpsOnlineFlag},
        {AvailabilityBucketColumn},
        d.CreatedAt, d.UpdatedAt
        """;

    internal const string DetailFrom = """
        FROM Drivers d
        LEFT JOIN Branches b ON b.Id = d.BranchId AND b.IsActive = 1
        LEFT JOIN Departments dep ON dep.Id = d.DepartmentId AND dep.IsActive = 1
        OUTER APPLY (
            SELECT TOP 1 v.Id AS VehicleId, v.VehicleCode, v.RegistrationNumber, v.GpsDeviceId
            FROM AssignmentHistory ah
            INNER JOIN Vehicles v ON v.Id = ah.VehicleId AND v.IsDeleted = 0
            WHERE ah.DriverId = d.Id AND ah.IsDeleted = 0 AND ah.Status = N'Active'
            ORDER BY ah.StartAt DESC
        ) av
        LEFT JOIN GpsDevices gd ON gd.Id = av.GpsDeviceId AND gd.IsDeleted = 0
        """;
}
