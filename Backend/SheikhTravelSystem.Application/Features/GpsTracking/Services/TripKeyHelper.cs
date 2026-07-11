using System.Text;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class TripKeyHelper
{
    public static string Build(int vehicleId, DateTime startTimeUtc)
    {
        var normalized = startTimeUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(startTimeUtc, DateTimeKind.Utc)
            : startTimeUtc.ToUniversalTime();
        var raw = $"{vehicleId}:{normalized:O}";
        return ToBase64Url(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryParse(string tripKey, out int vehicleId, out DateTime startTimeUtc)
    {
        vehicleId = 0;
        startTimeUtc = default;

        if (string.IsNullOrWhiteSpace(tripKey))
        {
            return false;
        }

        try
        {
            var bytes = FromBase64Url(tripKey.Trim());
            var raw = Encoding.UTF8.GetString(bytes);
            var separator = raw.IndexOf(':');
            if (separator <= 0 || separator >= raw.Length - 1)
            {
                return false;
            }

            if (!int.TryParse(raw[..separator], out vehicleId))
            {
                return false;
            }

            if (!DateTime.TryParse(raw[(separator + 1)..], null, System.Globalization.DateTimeStyles.RoundtripKind, out startTimeUtc))
            {
                return false;
            }

            startTimeUtc = startTimeUtc.ToUniversalTime();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ToBase64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}
