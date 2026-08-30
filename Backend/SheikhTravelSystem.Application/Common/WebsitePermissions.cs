namespace SheikhTravelSystem.Application.Common;

public static class WebsitePermissions
{
    public const string View = "Website.View";
    public const string Edit = "Website.Edit";
    public const string Publish = "Website.Publish";
    public const string Media = "Website.Media";
    public const string ContactRequests = "Website.ContactRequests";
    public const string DemoRequests = "Website.DemoRequests";
    public const string Legal = "Website.Legal";
    public const string Settings = "Website.Settings";

    public static readonly string[] All =
    [
        View, Edit, Publish, Media, ContactRequests, DemoRequests, Legal, Settings
    ];
}
