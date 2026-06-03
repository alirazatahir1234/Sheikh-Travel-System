namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public static class GpsGeoHelper
{
    private const double EarthRadiusKm = 6371.0;

    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    public static bool IsInsideCircle(double lat, double lng, double centerLat, double centerLng, double radiusMeters)
        => HaversineKm(lat, lng, centerLat, centerLng) * 1000.0 <= radiusMeters;

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
