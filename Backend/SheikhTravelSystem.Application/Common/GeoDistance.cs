namespace SheikhTravelSystem.Application.Common;

public static class GeoDistance
{
    public const double AutoAssignMaxKm = 25d;
    public const double DefaultResponseSpeedKmh = 45d;

    public static double? HaversineKm(double? lat1, double? lon1, double? lat2, double? lon2)
    {
        if (lat1 is null || lon1 is null || lat2 is null || lon2 is null) return null;
        const double R = 6371d;
        var dLat = DegreesToRadians(lat2.Value - lat1.Value);
        var dLon = DegreesToRadians(lon2.Value - lon1.Value);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(DegreesToRadians(lat1.Value)) * Math.Cos(DegreesToRadians(lat2.Value))
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(R * c, 2);
    }

    public static int? EtaMinutesFromKm(double? distanceKm, double speedKmh = DefaultResponseSpeedKmh)
    {
        if (distanceKm is null or <= 0) return null;
        return (int)Math.Ceiling(distanceKm.Value / speedKmh * 60d);
    }

    /// <summary>UAE (AE), Pakistan (PK), or Saudi Arabia (SA) from coordinates.</summary>
    public static string InferRegionCode(double latitude, double longitude) =>
        MapTrafficRegions.InferRegionCode(latitude, longitude);

    private static double DegreesToRadians(double deg) => deg * Math.PI / 180d;
}
