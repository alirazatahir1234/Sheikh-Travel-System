namespace SheikhTravelSystem.Application.Common;

public static class AnalyticsPermissions
{
    public const string ReportView = "Report.View";
    public const string CustomerView = "Customer.View";
    public const string CustomerCreate = "Customer.Create";
    public const string CustomerUpdate = "Customer.Update";
    public const string CustomerDelete = "Customer.Delete";
    public const string GpsView = "GPS.View";

    public static readonly string[] All =
    [
        ReportView,
        CustomerView,
        CustomerCreate,
        CustomerUpdate,
        CustomerDelete,
        GpsView
    ];
}
