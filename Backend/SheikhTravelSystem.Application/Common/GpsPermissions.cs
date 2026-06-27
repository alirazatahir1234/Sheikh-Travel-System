namespace SheikhTravelSystem.Application.Common;

public static class GpsPermissions
{
    public const string CommandSend = "Gps.CommandSend";
    public const string CommandView = "Gps.CommandView";

    public static readonly string[] All = [CommandSend, CommandView];
}
