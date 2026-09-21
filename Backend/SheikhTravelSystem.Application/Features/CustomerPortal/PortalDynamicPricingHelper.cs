using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.CustomerPortal;

/// <summary>
/// Pure geo helpers for portal dynamic pricing (no SQL).
/// </summary>
public static class PortalDynamicPricingHelper
{
    public const decimal MaxTripDistanceKm = 800;

    public static decimal HaversineDistanceKm(double lat1, double lng1, double lat2, double lng2)
        => (decimal)GpsGeoHelper.HaversineKm(lat1, lng1, lat2, lng2);

    public static int EstimateDurationMinutes(decimal distanceKm)
        => distanceKm <= 0 ? 60 : (int)Math.Ceiling((double)distanceKm / 70.0 * 60);
}
