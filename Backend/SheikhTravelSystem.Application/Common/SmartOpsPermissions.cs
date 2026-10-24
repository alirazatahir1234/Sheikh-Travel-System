namespace SheikhTravelSystem.Application.Common;

public static class SmartOpsPermissions
{
    public const string AlertView = "SmartOps.AlertView";
    public const string AlertManage = "SmartOps.AlertManage";

    public static readonly string[] All = [AlertView, AlertManage];
}
