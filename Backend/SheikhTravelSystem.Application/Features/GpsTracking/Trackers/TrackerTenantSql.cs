namespace SheikhTravelSystem.Application.Features.GpsTracking.Trackers;

public static class TrackerTenantSql
{
    /// <summary>
    /// Tenant-owned devices, or Traccar-imported rows linked to this tenant's vehicles.
    /// </summary>
    public const string DeviceScopeFilter = """
        AND (
            d.TenantId = @TenantId
            OR (d.TenantId IS NULL AND v.TenantId = @TenantId)
        )
        """;

    public const string DeviceExistsForTenant = """
        SELECT CASE WHEN EXISTS(
            SELECT 1 FROM GpsDevices d
            LEFT JOIN Vehicles v ON v.Id = d.VehicleId AND v.IsDeleted = 0
            WHERE d.Id = @Id AND d.IsDeleted = 0
              AND (d.TenantId = @TenantId OR (d.TenantId IS NULL AND v.TenantId = @TenantId))
        ) THEN 1 ELSE 0 END
        """;
}
