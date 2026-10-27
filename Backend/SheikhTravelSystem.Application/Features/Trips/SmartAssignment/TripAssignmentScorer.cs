using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.Trips.SmartAssignment;

/// <summary>
/// Weighted heuristic scorer for trip–driver matching. Traffic uses Google Routes when available;
/// otherwise straight-line distance and an average-speed estimate (documented in <see cref="TrafficSource"/>).
/// </summary>
public static class TripAssignmentScorer
{
    public const string TrafficGoogleRoutes = "GoogleRoutes";
    public const string TrafficHaversineEstimate = "HaversineEstimate";

    private const decimal WeightRating = 0.22m;
    private const decimal WeightProximity = 0.18m;
    private const decimal WeightFatigue = 0.15m;
    private const decimal WeightFuel = 0.15m;
    private const decimal WeightDelivery = 0.15m;
    private const decimal WeightTraffic = 0.15m;

    public static (decimal Total, IReadOnlyList<TripAssignmentScoreFactorDto> Factors) Score(
        TripAssignmentCandidateInput candidate,
        TripAssignmentTripInput trip,
        RouteMetrics? route)
    {
        var factors = new List<TripAssignmentScoreFactorDto>(6);

        var ratingRaw = RatingScore(candidate.Rating);
        factors.Add(Factor("rating", "Driver rating", WeightRating, ratingRaw,
            $"{FormatRating(candidate.Rating)} stars"));

        var (distKm, etaMin, trafficSource) = ResolveRouteMetrics(candidate, trip, route);
        var proximityRaw = ProximityScore(distKm);
        factors.Add(Factor("proximity", "Distance to pickup", WeightProximity, proximityRaw,
            distKm.HasValue ? $"{distKm.Value:F1} km from driver" : "Driver location unknown — neutral score"));

        var fatigueRaw = FatigueScore(candidate.HoursWorkedToday);
        factors.Add(Factor("fatigue", "Fatigue / hours today", WeightFatigue, fatigueRaw,
            $"{candidate.HoursWorkedToday:F1} h worked today"));

        var fuelRaw = FuelScore(candidate.FuelLevelPercent);
        var fuelLabel = candidate.IsElectric ? "EV battery" : "Vehicle fuel";
        var fuelDetail = candidate.FuelLevelPercent.HasValue
            ? candidate.IsElectric
                ? $"{candidate.FuelLevelPercent.Value:F0}% battery"
                : $"{candidate.FuelLevelPercent.Value:F0}% tank"
            : candidate.IsElectric
                ? "No live battery reading — neutral score"
                : "No live fuel reading — neutral score";
        factors.Add(Factor("fuel", fuelLabel, WeightFuel, fuelRaw, fuelDetail));

        var deliveryRaw = DeliveryTimeScore(trip.PlannedStartUtc, etaMin);
        factors.Add(Factor("delivery", "On-time pickup", WeightDelivery, deliveryRaw,
            etaMin.HasValue
                ? $"ETA {etaMin} min vs planned {trip.PlannedStartUtc:HH:mm} UTC"
                : "ETA unknown — neutral score"));

        var trafficRaw = TrafficScore(route, distKm, etaMin);
        factors.Add(Factor("traffic", "Route / traffic", WeightTraffic, trafficRaw,
            trafficSource == TrafficGoogleRoutes
                ? "Google Routes (traffic-aware)"
                : "Estimated from distance (Routes API unavailable)"));

        var total = factors.Sum(f => f.WeightedScore);
        return (Math.Round(total, 2), factors);
    }

    public sealed record TripAssignmentCandidateInput(
        int DriverId,
        decimal? Rating,
        double? Latitude,
        double? Longitude,
        decimal? FuelLevelPercent,
        decimal HoursWorkedToday,
        bool IsElectric = false,
        decimal? ElectricRangeKm = null);

    public sealed record TripAssignmentTripInput(
        DateTime PlannedStartUtc,
        double? PickupLatitude,
        double? PickupLongitude,
        double? DestinationLatitude,
        double? DestinationLongitude);

    public sealed record RouteMetrics(
        int DistanceMeters,
        int DurationSeconds,
        string Source);

    private static TripAssignmentScoreFactorDto Factor(
        string key, string label, decimal weight, decimal raw, string detail)
        => new(key, label, weight, raw, Math.Round(raw * weight, 2), detail);

    private static decimal RatingScore(decimal? rating)
    {
        if (!rating.HasValue || rating <= 0) return 70m;
        var clamped = Math.Clamp(rating.Value, 0m, 5m);
        return Math.Round(clamped / 5m * 100m, 1);
    }

    private static string FormatRating(decimal? rating)
        => rating is > 0 ? rating.Value.ToString("0.0") : "—";

    private static decimal ProximityScore(double? distanceKm)
    {
        if (!distanceKm.HasValue) return 55m;
        if (distanceKm <= 3) return 100m;
        if (distanceKm <= 8) return 90m;
        if (distanceKm <= 15) return 75m;
        if (distanceKm <= 30) return 55m;
        if (distanceKm <= 60) return 35m;
        return 15m;
    }

    private static decimal FatigueScore(decimal hoursWorked)
    {
        if (hoursWorked <= 4) return 100m;
        if (hoursWorked <= 6) return 85m;
        if (hoursWorked <= 8) return 65m;
        if (hoursWorked <= 10) return 40m;
        return 15m;
    }

    private static decimal FuelScore(decimal? fuelPercent)
    {
        if (!fuelPercent.HasValue) return 60m;
        var p = fuelPercent.Value;
        if (p >= 60) return 100m;
        if (p >= 40) return 80m;
        if (p >= 25) return 55m;
        if (p >= 15) return 30m;
        return 10m;
    }

    private static decimal DeliveryTimeScore(DateTime plannedStartUtc, int? etaMinutes)
    {
        if (!etaMinutes.HasValue) return 60m;
        var arrival = DateTime.UtcNow.AddMinutes(etaMinutes.Value);
        var slack = (plannedStartUtc - arrival).TotalMinutes;
        if (slack >= 30) return 100m;
        if (slack >= 10) return 90m;
        if (slack >= 0) return 75m;
        if (slack >= -10) return 50m;
        if (slack >= -25) return 25m;
        return 5m;
    }

    private static decimal TrafficScore(RouteMetrics? route, double? distanceKm, int? etaMinutes)
    {
        if (route is not null && distanceKm is > 0 && etaMinutes is > 0)
        {
            var freeFlowMin = distanceKm.Value / 50.0 * 60.0;
            var ratio = etaMinutes.Value / Math.Max(freeFlowMin, 1);
            if (ratio <= 1.15) return 95m;
            if (ratio <= 1.35) return 75m;
            if (ratio <= 1.6) return 50m;
            return 25m;
        }

        return distanceKm.HasValue ? 65m : 50m;
    }

    private static (double? DistKm, int? EtaMin, string Source) ResolveRouteMetrics(
        TripAssignmentCandidateInput candidate,
        TripAssignmentTripInput trip,
        RouteMetrics? route)
    {
        if (route is not null)
        {
            return (route.DistanceMeters / 1000.0, (int)Math.Ceiling(route.DurationSeconds / 60.0), route.Source);
        }

        if (candidate.Latitude is null || candidate.Longitude is null
            || trip.PickupLatitude is null || trip.PickupLongitude is null)
            return (null, null, TrafficHaversineEstimate);

        var km = GpsGeoHelper.HaversineKm(
            candidate.Latitude.Value, candidate.Longitude.Value,
            trip.PickupLatitude.Value, trip.PickupLongitude.Value);
        var eta = (int)Math.Ceiling(km / 45.0 * 60.0);
        return (km, eta, TrafficHaversineEstimate);
    }
}
