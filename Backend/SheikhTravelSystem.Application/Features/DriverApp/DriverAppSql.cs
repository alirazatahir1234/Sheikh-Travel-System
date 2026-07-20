namespace SheikhTravelSystem.Application.Features.DriverApp;

internal static class DriverAppSql
{
    /// <summary>
    /// Resolves the driver's current vehicle: active fleet assignment first, then active booking.
    /// Requires alias <c>d</c> (Drivers) and parameter <c>@TenantId</c>.
    /// </summary>
    internal const string CurrentVehicleApply = """
        OUTER APPLY (
            SELECT TOP 1 v.Id, v.Name, v.RegistrationNumber
            FROM Vehicles v
            WHERE v.IsDeleted = 0
              AND v.Id = COALESCE(
                (SELECT TOP 1 ah.VehicleId
                 FROM AssignmentHistory ah
                 WHERE ah.DriverId = d.Id AND ah.TenantId = @TenantId AND ah.IsDeleted = 0 AND ah.Status = N'Active'
                 ORDER BY ah.StartAt DESC),
                (SELECT TOP 1 b.VehicleId
                 FROM Bookings b
                 WHERE b.DriverId = d.Id AND b.TenantId = @TenantId AND b.IsDeleted = 0
                   AND b.VehicleId IS NOT NULL AND b.Status IN (2, 3)
                 ORDER BY b.PickupTime DESC))
        ) cv
        """;

    internal const string AssignedVehicleIdsUnion = """
        SELECT VehicleId FROM AssignmentHistory
        WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
          AND Status = N'Active' AND VehicleId IS NOT NULL
        UNION
        SELECT VehicleId FROM Trips
        WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0 AND VehicleId IS NOT NULL
          AND Status NOT IN (9, 10, 11)
        UNION
        SELECT VehicleId FROM Bookings
        WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0 AND VehicleId IS NOT NULL
          AND Status IN (2, 3)
        """;

    internal const string DriverOwnsVehicleExists = """
        SELECT CASE WHEN EXISTS(
            SELECT 1 FROM AssignmentHistory
            WHERE DriverId = @DriverId AND VehicleId = @VehicleId
              AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Active'
            UNION ALL
            SELECT 1 FROM Trips WHERE DriverId = @DriverId AND VehicleId = @VehicleId
              AND TenantId = @TenantId AND IsDeleted = 0 AND Status NOT IN (9, 10, 11)
            UNION ALL
            SELECT 1 FROM Bookings WHERE DriverId = @DriverId AND VehicleId = @VehicleId
              AND TenantId = @TenantId AND IsDeleted = 0 AND Status IN (2, 3)
        ) THEN 1 ELSE 0 END
        """;
}
