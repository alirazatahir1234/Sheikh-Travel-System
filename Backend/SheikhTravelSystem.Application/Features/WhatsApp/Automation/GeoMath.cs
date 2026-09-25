namespace SheikhTravelSystem.Application.Features.WhatsApp.Automation;

public static class GeoMath
{
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>Haversine distance in meters.</summary>
    public static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLng = DegreesToRadians(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
                  * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    public static double KnotsToKmh(double knots) => knots * 1.852;

    /// <summary>ETA minutes: distance × road factor ÷ speed, clamped 1–180.</summary>
    public static int EstimateEtaMinutes(
        double distanceMeters,
        double averageSpeedKmh = AutomationPolicy.DefaultCitySpeedKmh,
        double roadFactor = AutomationPolicy.RoadFactor)
    {
        if (averageSpeedKmh <= 0) averageSpeedKmh = AutomationPolicy.DefaultCitySpeedKmh;
        var km = (distanceMeters / 1000.0) * roadFactor;
        var hours = km / averageSpeedKmh;
        var minutes = (int)Math.Round(hours * 60);
        return Math.Clamp(minutes, 1, 180);
    }

    private static double DegreesToRadians(double deg) => deg * Math.PI / 180.0;
}

public static class ProximityRules
{
    public static bool IsArriving(double distanceMeters)
        => distanceMeters < AutomationPolicy.ArrivingMeters;

    public static bool IsArrived(double distanceMeters, double speedKmh)
        => distanceMeters < AutomationPolicy.ArrivedMeters
           && speedKmh < AutomationPolicy.ArrivedMaxSpeedKmh;
}
