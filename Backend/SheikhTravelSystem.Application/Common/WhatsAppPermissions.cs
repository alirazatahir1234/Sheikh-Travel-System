namespace SheikhTravelSystem.Application.Common;

public static class WhatsAppPermissions
{
    public const string View = "WhatsApp.View";
    public const string Reply = "WhatsApp.Reply";
    public const string Manage = "WhatsApp.Manage";
    public const string ManageAccounts = "WhatsApp.ManageAccounts";
    public const string ManageTemplates = "WhatsApp.ManageTemplates";
    public const string AiAssist = "WhatsApp.AiAssist";

    public static readonly string[] All =
    [
        View, Reply, Manage, ManageAccounts, ManageTemplates, AiAssist
    ];
}
