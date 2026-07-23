namespace SheikhTravelSystem.Application.Common;

public static class NotificationPermissions
{
    public const string View = "Notification.View";
    public const string Manage = "Notification.Manage";

    public static readonly string[] All =
    [
        View,
        Manage
    ];
}
