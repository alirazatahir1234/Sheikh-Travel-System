namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Default permission sets for system roles seeded per tenant.
/// </summary>
public static class TenantRolePermissionTemplates
{
    public static readonly string[] TenantAdmin =
    [
        "Platform.Dashboard.View", "Platform.Users.View", "Platform.Users.Create", "Platform.Users.Edit",
        "Platform.Roles.View", "Platform.Roles.Manage", "Platform.Branches.Manage", "Platform.Departments.Manage",
        "Platform.AuditLogs.View", "Platform.Menus.Manage",
        "Booking.View", "Booking.Create", "Trip.View", "Route.View",
        "Vehicle.View", "Vehicle.Create", "Vehicle.Update", "Vehicle.Delete", "Driver.View", "Driver.Manage",
        "GPS.View", "Fuel.View", "Maintenance.View", "Customer.View", "Payment.View", "Invoice.View", "Report.View"
    ];

    public static readonly string[] FleetManager =
    [
        "Platform.Dashboard.View", "Vehicle.View", "Vehicle.Create", "Vehicle.Update", "Vehicle.Delete",
        "Driver.View", "Driver.Manage", "GPS.View", "Fuel.View", "Maintenance.View", "Report.View"
    ];

    public static readonly string[] Dispatcher =
    [
        "Platform.Dashboard.View", "Booking.View", "Booking.Create", "Trip.View", "Route.View",
        "Vehicle.View", "Driver.View", "GPS.View", "Customer.View"
    ];

    public static readonly string[] Accountant =
    [
        "Platform.Dashboard.View", "Payment.View", "Invoice.View", "Report.View", "Fuel.View"
    ];

    public static readonly string[] Driver =
    [
        "Trip.View", "GPS.View", "Fuel.View"
    ];

    public static IReadOnlyList<(string RoleCode, string[] Permissions)> StandardRoles { get; } =
    [
        ("TENANT_ADMIN", TenantAdmin),
        ("FLEET_MANAGER", FleetManager),
        ("DISPATCHER", Dispatcher),
        ("ACCOUNTANT", Accountant),
        ("DRIVER", Driver),
    ];
}
