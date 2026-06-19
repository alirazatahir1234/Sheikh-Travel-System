namespace SheikhTravelSystem.Application.Features.Drivers;

internal static class DriverSql
{
    internal const string LicenseExpiryFlags = """
        CASE WHEN d.LicenseExpiryDate < CAST(GETUTCDATE() AS DATE) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS LicenseExpired,
        CASE WHEN d.LicenseExpiryDate >= CAST(GETUTCDATE() AS DATE)
              AND d.LicenseExpiryDate <= DATEADD(day, 30, CAST(GETUTCDATE() AS DATE))
             THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS LicenseExpiringSoon
        """;

    internal const string ListSelect = $"""
        d.Id, d.DriverCode, d.FirstName, d.LastName, d.FullName, d.Phone, d.LicenseNumber, d.LicenseExpiryDate,
        {LicenseExpiryFlags},
        d.Nationality, d.Status, d.IsActive, d.VerificationStatus, d.BranchId,
        b.Name AS BranchName,
        av.VehicleId AS AssignedVehicleId,
        av.VehicleCode AS AssignedVehicleCode,
        av.RegistrationNumber AS AssignedVehicleRegistration,
        d.CreatedAt
        """;

    internal const string ListFrom = """
        FROM Drivers d
        LEFT JOIN Branches b ON b.Id = d.BranchId AND b.IsActive = 1
        OUTER APPLY (
            SELECT TOP 1 v.Id AS VehicleId, v.VehicleCode, v.RegistrationNumber
            FROM AssignmentHistory ah
            INNER JOIN Vehicles v ON v.Id = ah.VehicleId AND v.IsDeleted = 0
            WHERE ah.DriverId = d.Id AND ah.IsDeleted = 0 AND ah.Status = N'Active'
            ORDER BY ah.StartAt DESC
        ) av
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
        d.Status, d.IsActive, d.CreatedAt, d.UpdatedAt
        """;

    internal const string DetailFrom = """
        FROM Drivers d
        LEFT JOIN Branches b ON b.Id = d.BranchId AND b.IsActive = 1
        LEFT JOIN Departments dep ON dep.Id = d.DepartmentId AND dep.IsActive = 1
        OUTER APPLY (
            SELECT TOP 1 v.Id AS VehicleId, v.VehicleCode, v.RegistrationNumber
            FROM AssignmentHistory ah
            INNER JOIN Vehicles v ON v.Id = ah.VehicleId AND v.IsDeleted = 0
            WHERE ah.DriverId = d.Id AND ah.IsDeleted = 0 AND ah.Status = N'Active'
            ORDER BY ah.StartAt DESC
        ) av
        """;
}
