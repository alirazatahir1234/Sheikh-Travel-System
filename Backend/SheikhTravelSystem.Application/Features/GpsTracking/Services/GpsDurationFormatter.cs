namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>Formats minute counts for history UI (matches ERP History screen).</summary>
public static class GpsDurationFormatter
{
    public static string FormatMinutes(int minutes)
    {
        if (minutes < 1) return "0 min";
        var h = minutes / 60;
        var m = minutes % 60;
        if (h == 0) return $"{m} min";
        if (m == 0) return $"{h} hr";
        return $"{h} hr {m} min";
    }
}
