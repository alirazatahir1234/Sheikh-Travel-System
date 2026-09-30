namespace SheikhTravelSystem.Tests.Security.Governance;

/// <summary>
/// Canonical inventory of tables that carry a TenantId column
/// (aligned with <c>TenantSchemaMigration.TenantScopedTables</c>).
/// Phase 3C governance treats these as tenant-owned unless allowlisted.
/// </summary>
public static class TenantScopedTableCatalog
{
    public static readonly IReadOnlyList<string> TablesWithTenantIdColumn =
    [
        "Users", "Customers", "Vehicles", "Drivers", "Routes", "Bookings", "Payments",
        "FuelLogs", "Maintenance", "GpsDevices", "Geofences", "GpsAlertRules", "GpsAlertEvents",
        "GpsDeviceCommands", "GpsPositions", "GpsTrips", "Notifications", "AuditLogs",
        "DriverAllowanceRules", "PromoCodes"
    ];

    /// <summary>
    /// Tables without TenantId that must be scoped via a tenant-owned parent join.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ParentScopedTables =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VehicleTracking"] = "Vehicles"
        };

    /// <summary>
    /// Repositories that Phase 3B fixed / verified — must keep ITenantContext,
    /// explicit tenantId parameters, or an equivalent documented scope helper.
    /// </summary>
    public static readonly IReadOnlyList<string> StrictTenantRepositories =
    [
        "DashboardRepository.cs",
        "BookingRepository.cs",
        "PaymentRepository.cs",
        "DriverAppRepository.cs",
        "FuelLogRepository.cs",
        "MaintenanceRepository.cs",
        "RouteRepository.cs",
        "DriverAllowanceRepository.cs",
        "PricingRepository.cs",
        "TrackingRepository.cs",
        "CustomerRepository.cs",
        "AuditLogRepository.cs",
        "GpsDeviceRepository.cs",
        "GpsTrackingRepository.cs",
        "VehicleRepository.cs",
        "DriverRepository.cs"
    ];
}
