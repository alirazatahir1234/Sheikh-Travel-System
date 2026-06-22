namespace SheikhTravelSystem.Application.Common;

public static class MaintenancePermissions
{
    public const string View = "Maintenance.View";
    public const string Manage = "Maintenance.Manage";
    public const string RequestCreate = "Maintenance.Request.Create";
    public const string WorkOrderManage = "Maintenance.WorkOrder.Manage";
    public const string WorkshopManage = "Maintenance.Workshop.Manage";
    public const string VendorManage = "Maintenance.Vendor.Manage";
    public const string RequestApprove = "Maintenance.Request.Approve";
    public const string ReportView = "Maintenance.Report.View";

    public static readonly string[] All =
    [
        View,
        Manage,
        RequestCreate,
        RequestApprove,
        WorkOrderManage,
        WorkshopManage,
        VendorManage,
        ReportView
    ];
}
