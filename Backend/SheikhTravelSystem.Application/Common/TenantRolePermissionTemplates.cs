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
        "Platform.AuditLogs.View", "Platform.Menus.Manage", "Platform.Workspaces.Manage",
        "Platform.Dashboards.View", "Platform.Dashboards.Manage",
        "Platform.Security.View", "Platform.Security.Manage",
        "Platform.Settings.View", "Platform.Settings.Manage",
        "Booking.View", "Booking.Create", "Trip.View", "Route.View",
        "Vehicle.View", "Vehicle.Create", "Vehicle.Update", "Vehicle.Delete", "Driver.View", "Driver.Create", "Driver.Update", "Driver.Delete", "Driver.Assign", "Driver.Manage", "Driver.ManageStatus", "Driver.ViewPerformance",
        "GPS.View", "Fuel.View", "Maintenance.View", "Maintenance.Manage", "Maintenance.Request.Create", "Maintenance.Request.Approve",
        "Gps.AlertView", "Gps.AlertAcknowledge", "Gps.AlertResolve", "Gps.AlertArchive", "Gps.AlertDelete",
        "Maintenance.WorkOrder.Manage", "Maintenance.Workshop.Manage", "Maintenance.Vendor.Manage", "Maintenance.Report.View",
        "Customer.View", "Payment.View", "Invoice.View", "Report.View",
        "Gps.CommandView", "Gps.CommandSend", "Gps.CommandEngineCutoff", "Gps.CommandPositionRequest",
        "Gps.CommandRestart", "Gps.CommandRelay", "Gps.CommandBuzzer", "Gps.CommandCustomSms", "Gps.CommandRetry", "Gps.CommandCancel",
        "Ai.View", "Ai.Manage", "Ai.ExecuteWrite",
        "Notification.View", "Notification.Manage"
    ];

    public static readonly string[] FleetManager =
    [
        "Platform.Dashboard.View", "Trip.View", "Vehicle.View", "Vehicle.Create", "Vehicle.Update", "Vehicle.Delete",
        "Driver.View", "Driver.Create", "Driver.Update", "Driver.Delete", "Driver.Assign", "Driver.Manage", "Driver.ManageStatus", "Driver.ViewPerformance", "GPS.View", "Fuel.View",
        "Gps.AlertView", "Gps.AlertAcknowledge", "Gps.AlertResolve", "Gps.AlertArchive",
        "Maintenance.View", "Maintenance.Manage", "Maintenance.Request.Create", "Maintenance.Request.Approve", "Maintenance.WorkOrder.Manage", "Maintenance.Workshop.Manage", "Maintenance.Vendor.Manage", "Maintenance.Report.View", "Report.View",
        "Gps.CommandView", "Gps.CommandSend", "Gps.CommandEngineCutoff", "Gps.CommandPositionRequest",
        "Gps.CommandRestart", "Gps.CommandRelay", "Gps.CommandBuzzer", "Gps.CommandCustomSms", "Gps.CommandRetry", "Gps.CommandCancel",
        "Ai.View", "Ai.Manage", "Ai.ExecuteWrite",
        "Notification.View", "Notification.Manage"
    ];

    public static readonly string[] DriverManager =
    [
        "Platform.Dashboard.View",
        "Driver.View", "Driver.Create", "Driver.Update", "Driver.Assign", "Driver.Manage", "Driver.ManageStatus", "Driver.ViewPerformance",
        "Vehicle.View",
        "GPS.View",
        "Gps.AlertView",
        "Trip.View",
        "Report.View",
        "Notification.View"
    ];

    public static readonly string[] Dispatcher =
    [
        "Platform.Dashboard.View", "Booking.View", "Booking.Create", "Trip.View", "Route.View",
        "Vehicle.View", "Driver.View", "GPS.View", "Customer.View",
        "Gps.AlertView", "Gps.AlertAcknowledge",
        "Gps.CommandView", "Gps.CommandSend", "Gps.CommandPositionRequest",
        "Ai.View",
        "Notification.View"
    ];

    public static readonly string[] Accountant =
    [
        "Platform.Dashboard.View", "Payment.View", "Invoice.View", "Report.View", "Fuel.View",
        "Maintenance.Report.View", "Maintenance.View",
        "Notification.View"
    ];

    public static readonly string[] Driver =
    [
        "Trip.View", "GPS.View", "Fuel.View", "Maintenance.Request.Create", "Maintenance.View",
        "Gps.AlertView", "Gps.AlertAcknowledge",
        "Gps.CommandView",
        "Notification.View"
    ];

    public static IReadOnlyList<(string RoleCode, string[] Permissions)> StandardRoles { get; } =
    [
        ("TENANT_ADMIN", TenantAdmin),
        ("FLEET_MANAGER", FleetManager),
        ("DRIVER_MANAGER", DriverManager),
        ("DISPATCHER", Dispatcher),
        ("ACCOUNTANT", Accountant),
        ("DRIVER", Driver),
    ];
}
