namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

internal static class DriverSql
{
    /// <summary>
    /// Dispatch bucket using the joined active-assignment alias <c>av</c> (VehicleId set = Busy).
    /// Avoids a correlated EXISTS when AssignmentHistory is already joined / OUTER APPLYed.
    /// </summary>
    internal const string BucketSqlExpression = """
        CASE
            WHEN d.IsActive = 0 OR d.Status IN (@Suspended, @OnLeave) OR d.LicenseExpiryDate < CAST(GETUTCDATE() AS DATE)
                THEN N'Unavailable'
            WHEN d.Status = @OnTrip THEN N'OnTrip'
            WHEN av.VehicleId IS NOT NULL THEN N'Busy'
            WHEN d.Status IN (@Available, @OffDuty) THEN N'Available'
            ELSE N'Unavailable'
        END
        """;

    /// <summary>
    /// Same rules as <see cref="BucketSqlExpression"/> but with a correlated EXISTS —
    /// only for queries that do not join active assignments (e.g. single-driver detail helpers).
    /// </summary>
    internal const string BucketSqlExpressionExists = """
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

    internal const string AvailabilityBucketColumn = $"""
        {BucketSqlExpression} AS AvailabilityBucket
        """;

    /// <summary>
    /// Latest active assignment per driver (set-based). Prefer over correlated OUTER APPLY for aggregates.
    /// Requires @TenantId. Alias columns as VehicleId / GpsDeviceId for BucketSqlExpression + GpsOnlineFlag.
    /// </summary>
    internal const string ActiveAssignmentJoin = """
        LEFT JOIN (
            SELECT ah.DriverId,
                   v.Id AS VehicleId,
                   v.GpsDeviceId,
                   v.VehicleCode,
                   v.RegistrationNumber,
                   v.Name AS VehicleName,
                   v.Make AS VehicleMake,
                   v.Model AS VehicleModel,
                   v.Color AS VehicleColor,
                   ROW_NUMBER() OVER (PARTITION BY ah.DriverId ORDER BY ah.StartAt DESC) AS rn
            FROM AssignmentHistory ah
            INNER JOIN Vehicles v ON v.Id = ah.VehicleId AND v.IsDeleted = 0
            WHERE ah.IsDeleted = 0 AND ah.Status = N'Active'
              AND ah.DriverId IS NOT NULL
              AND (ah.TenantId IS NULL OR ah.TenantId = @TenantId)
        ) av ON av.DriverId = d.Id AND av.rn = 1
        """;

    internal const string ListSelect = $"""
        d.Id, d.DriverCode, d.FirstName, d.LastName, d.FullName, d.Phone, d.LicenseNumber, d.LicenseExpiryDate,
        {LicenseExpiryFlags},
        d.Nationality, d.Status, d.IsActive, d.VerificationStatus, d.BranchId,
        b.Name AS BranchName,
        d.DepartmentId, dep.Name AS DepartmentName, d.HireDate, d.PhotoUrl,
        av.VehicleId AS AssignedVehicleId,
        av.VehicleCode AS AssignedVehicleCode,
        av.RegistrationNumber AS AssignedVehicleRegistration,
        av.VehicleName AS AssignedVehicleName,
        av.VehicleMake AS AssignedVehicleMake,
        av.VehicleModel AS AssignedVehicleModel,
        av.VehicleColor AS AssignedVehicleColor,
        d.Rating,
        {GpsOnlineFlag},
        {AvailabilityBucketColumn},
        d.CreatedAt
        """;

    internal const string ListFrom = $"""
        FROM Drivers d
        LEFT JOIN Branches b ON b.Id = d.BranchId AND b.IsActive = 1
        LEFT JOIN Departments dep ON dep.Id = d.DepartmentId AND dep.IsActive = 1
        {ActiveAssignmentJoin}
        LEFT JOIN GpsDevices gd ON gd.Id = av.GpsDeviceId AND gd.IsDeleted = 0
        """;

    /// <summary>Drivers-only FROM for COUNT(*) when availability bucket filter is not applied.</summary>
    internal const string ListCountFrom = """
        FROM Drivers d
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
        av.VehicleName AS AssignedVehicleName, av.VehicleMake AS AssignedVehicleMake,
        av.VehicleModel AS AssignedVehicleModel, av.VehicleColor AS AssignedVehicleColor,
        d.Status, d.IsActive, d.Rating, d.YearsExperience,
        {GpsOnlineFlag},
        {AvailabilityBucketColumn},
        d.CreatedAt, d.UpdatedAt
        """;

    internal const string DetailFrom = $"""
        FROM Drivers d
        LEFT JOIN Branches b ON b.Id = d.BranchId AND b.IsActive = 1
        LEFT JOIN Departments dep ON dep.Id = d.DepartmentId AND dep.IsActive = 1
        {ActiveAssignmentJoin}
        LEFT JOIN GpsDevices gd ON gd.Id = av.GpsDeviceId AND gd.IsDeleted = 0
        """;
}
