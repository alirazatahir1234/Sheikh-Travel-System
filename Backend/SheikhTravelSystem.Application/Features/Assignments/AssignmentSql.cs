namespace SheikhTravelSystem.Application.Features.Assignments;

internal static class AssignmentSql
{
    internal const string DisplayStatusExpression = """
        CASE
            WHEN a.Status = N'Active' AND a.EndAt IS NOT NULL AND a.EndAt < GETUTCDATE() THEN N'Overdue'
            WHEN a.Status = N'Active' AND a.StartAt > GETUTCDATE() THEN N'Assigned'
            WHEN a.Status = N'Scheduled' AND a.StartAt <= GETUTCDATE() THEN N'Active'
            ELSE a.Status
        END
        """;

    internal static string ListSelect => $"""
        a.Id,
        ISNULL(a.AssignmentNo, CONCAT(N'ASN-', RIGHT(CONCAT(N'000000', CAST(a.Id AS NVARCHAR)), 6))) AS AssignmentNo,
        a.VehicleId,
        v.Name AS VehicleName,
        v.RegistrationNumber AS VehicleRegistration,
        v.VehicleCode,
        a.DriverId,
        d.FullName AS DriverName,
        d.DriverCode,
        a.AssignmentType,
        a.Status,
        {DisplayStatusExpression} AS DisplayStatus,
        a.StartAt,
        a.EndAt,
        a.Purpose,
        ISNULL(a.PickupLocation, b.PickupAddress) AS PickupLocation,
        ISNULL(a.DropLocation, b.DropoffAddress) AS DropLocation,
        CASE WHEN a.EndAt IS NOT NULL THEN DATEDIFF(day, a.StartAt, a.EndAt)
             ELSE DATEDIFF(day, a.StartAt, GETUTCDATE()) END AS DurationDays,
        a.OdometerStart,
        a.OdometerEnd,
        a.Reason,
        a.Notes,
        a.CreatedBy,
        a.CreatedAt,
        a.ModifiedBy,
        a.ModifiedAt,
        CAST(CASE WHEN gd.LastSeenAt IS NOT NULL AND gd.LastSeenAt >= DATEADD(minute, -15, GETUTCDATE()) THEN 1 ELSE 0 END AS BIT) AS GpsOnline,
        gp.Speed AS GpsSpeed,
        gp.Timestamp AS GpsLastSeen,
        gp.Ignition,
        CAST(CASE WHEN d.LicenseExpiryDate IS NOT NULL
              AND d.LicenseExpiryDate >= CAST(GETUTCDATE() AS DATE)
              AND d.LicenseExpiryDate <= DATEADD(day, 30, CAST(GETUTCDATE() AS DATE))
             THEN 1 ELSE 0 END AS BIT) AS DriverLicenseExpiringSoon,
        CAST(CASE WHEN v.Status = 3 OR EXISTS (
            SELECT 1 FROM Maintenance m
            WHERE m.VehicleId = v.Id AND m.IsDeleted = 0
              AND m.Status IN (1, 2)
              AND m.MaintenanceDate <= DATEADD(day, 1, CAST(GETUTCDATE() AS DATE))
        ) THEN 1 ELSE 0 END AS BIT) AS VehicleMaintenanceDue
        """;

    internal const string ListFrom = """
        FROM AssignmentHistory a
        INNER JOIN Vehicles v ON a.VehicleId = v.Id AND v.IsDeleted = 0
        LEFT JOIN Drivers d ON a.DriverId = d.Id AND d.IsDeleted = 0
        LEFT JOIN Bookings b ON a.BookingId = b.Id AND b.IsDeleted = 0
        LEFT JOIN GpsDevices gd ON gd.Id = v.GpsDeviceId AND gd.IsDeleted = 0
        OUTER APPLY (
            SELECT TOP 1 p.Speed, p.Ignition, p.RecordedAt AS Timestamp
            FROM GpsPositions p
            WHERE p.VehicleId = v.Id
            ORDER BY p.RecordedAt DESC
        ) gp
        """;
}
