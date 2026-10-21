namespace SheikhTravelSystem.Application.Common;

public static class GpsPermissions
{
    public const string AlertView = "Gps.AlertView";
    public const string AlertAcknowledge = "Gps.AlertAcknowledge";
    public const string AlertResolve = "Gps.AlertResolve";
    public const string AlertArchive = "Gps.AlertArchive";
    public const string AlertDelete = "Gps.AlertDelete";
    /// <summary>Register, update, delete, install, uninstall, and transfer GPS trackers.</summary>
    public const string DeviceManage = "Gps.DeviceManage";
    public const string CommandSend = "Gps.CommandSend";
    public const string CommandView = "Gps.CommandView";
    public const string CommandEngineCutoff = "Gps.CommandEngineCutoff";
    /// <summary>Approve (and dispatch) tenant GPS commands that require approval (e.g. engineStop).</summary>
    public const string CommandApprove = "Gps.CommandApprove";
    public const string CommandPositionRequest = "Gps.CommandPositionRequest";
    public const string CommandRestart = "Gps.CommandRestart";
    public const string CommandRelay = "Gps.CommandRelay";
    public const string CommandBuzzer = "Gps.CommandBuzzer";
    public const string CommandCustomSms = "Gps.CommandCustomSms";
    public const string CommandRetry = "Gps.CommandRetry";
    public const string CommandCancel = "Gps.CommandCancel";

    public static readonly string[] All =
    [
        AlertView, AlertAcknowledge, AlertResolve, AlertArchive, AlertDelete,
        DeviceManage,
        CommandSend, CommandView, CommandEngineCutoff, CommandApprove, CommandPositionRequest,
        CommandRestart, CommandRelay, CommandBuzzer, CommandCustomSms, CommandRetry, CommandCancel
    ];
}
