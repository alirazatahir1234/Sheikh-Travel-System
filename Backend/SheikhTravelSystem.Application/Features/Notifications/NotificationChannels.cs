namespace SheikhTravelSystem.Application.Features.Notifications;

public static class NotificationChannels
{
    public const string InApp = "InApp";
    public const string Email = "Email";
    public const string Sms = "Sms";
    public const string Push = "Push";
    public const string Browser = "Browser";
    public const string WhatsApp = "WhatsApp";

    public static readonly IReadOnlyList<string> All =
    [
        InApp, Email, Sms, Push, Browser, WhatsApp
    ];

    public static string Normalize(string? channel) =>
        string.IsNullOrWhiteSpace(channel) ? InApp : channel.Trim();
}
