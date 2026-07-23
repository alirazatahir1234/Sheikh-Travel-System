namespace SheikhTravelSystem.Application.Common;

public static class AnalyticsPermissions
{
    public const string ReportView = "Report.View";
    public const string CustomerView = "Customer.View";
    public const string GpsView = "GPS.View";

    public static readonly string[] All =
    [
        ReportView,
        CustomerView,
        GpsView
    ];
}
