using SheikhTravelSystem.Application.Features.Notifications;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// The geofence editor's "Notify" channels, stored comma-separated in <c>Geofences.NotifyChannels</c>.
/// <see cref="DriverPush"/> is a push to the vehicle's driver; the others go to the geofence's recipients.
/// </summary>
public static class GeofenceNotifyChannels
{
    public const string InApp = "InApp";
    public const string WhatsApp = "WhatsApp";
    public const string Email = "Email";
    public const string Sms = "Sms";
    public const string Push = "Push";
    public const string DriverPush = "DriverPush";

    public static readonly IReadOnlyList<string> All = [InApp, WhatsApp, Email, Sms, Push, DriverPush];

    /// <summary>Known channels in canonical spelling, unknown ones dropped, order kept.</summary>
    public static IReadOnlyList<string> Parse(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return [];

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => All.FirstOrDefault(k => string.Equals(k, c, StringComparison.OrdinalIgnoreCase)))
            .Where(c => c is not null)
            .Select(c => c!)
            .Distinct()
            .ToList();
    }

    /// <summary>Null when nothing valid was chosen (the alert's default channels then apply).</summary>
    public static string? Format(IEnumerable<string>? channels)
    {
        var parsed = Parse(channels is null ? null : string.Join(',', channels));
        return parsed.Count == 0 ? null : string.Join(',', parsed);
    }

    /// <summary>Delivery channels for the recipients. In-app also lights the browser notification.</summary>
    public static IReadOnlyList<string> ToDeliveryChannels(IReadOnlyList<string> chosen)
    {
        var result = new List<string>();
        foreach (var channel in chosen)
        {
            switch (channel)
            {
                case InApp:
                    result.Add(NotificationChannels.InApp);
                    result.Add(NotificationChannels.Browser);
                    break;
                case WhatsApp:
                    result.Add(NotificationChannels.WhatsApp);
                    break;
                case Email:
                    result.Add(NotificationChannels.Email);
                    break;
                case Sms:
                    result.Add(NotificationChannels.Sms);
                    break;
                case Push:
                    result.Add(NotificationChannels.Push);
                    break;
            }
        }

        return result.Distinct().ToList();
    }
}
