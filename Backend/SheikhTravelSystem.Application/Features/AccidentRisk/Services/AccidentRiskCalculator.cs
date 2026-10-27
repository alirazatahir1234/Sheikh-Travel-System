namespace SheikhTravelSystem.Application.Features.AccidentRisk.Services;

/// <summary>
/// Rule-based accident risk (0–100, higher = more risk). Weights are starting points for ops tuning.
/// </summary>
public static class AccidentRiskCalculator
{
    public const int HighRiskThreshold = 65;

    public static DriverAccidentRiskDto BuildDriver(
        int driverId,
        string driverName,
        int accidentCount,
        int speedViolationCount,
        int fatigueEventCount)
    {
        var factors = new List<AccidentRiskFactorDto>();
        var score = 0;

        var accidentPts = Math.Min(40, accidentCount * 25);
        factors.Add(new AccidentRiskFactorDto(
            "accidents",
            "Past accidents",
            accidentCount,
            accidentPts,
            accidentCount == 0 ? "No logged accidents in period." : $"{accidentCount} accident incident(s) on record."));
        score += accidentPts;

        var speedPts = Math.Min(25, speedViolationCount * 2);
        factors.Add(new AccidentRiskFactorDto(
            "speed",
            "Speed violations",
            speedViolationCount,
            speedPts,
            speedViolationCount == 0
                ? "No overspeed GPS alerts or violations in period."
                : $"{speedViolationCount} overspeed signal(s) from GPS alerts and driver violations."));
        score += speedPts;

        var fatiguePts = Math.Min(20, fatigueEventCount * 8);
        factors.Add(new AccidentRiskFactorDto(
            "fatigue",
            "Fatigue events",
            fatigueEventCount,
            fatiguePts,
            fatigueEventCount == 0
                ? "No fatigue camera / signal events in period."
                : $"{fatigueEventCount} fatigue event(s) from DriverFatigueEvents."));
        score += fatiguePts;

        score = Math.Clamp(score, 0, 100);
        var band = BandFor(score);
        return new DriverAccidentRiskDto(driverId, driverName, score, band, score >= HighRiskThreshold, factors);
    }

    public static RouteAccidentRiskDto BuildRoute(
        int routeId,
        string routeLabel,
        decimal distanceKm,
        int tripCount,
        int accidentCount,
        int speedViolationCount)
    {
        var factors = new List<AccidentRiskFactorDto>();
        var score = 0;

        var difficultyPts = RouteDifficultyPoints(distanceKm);
        factors.Add(new AccidentRiskFactorDto(
            "route_difficulty",
            "Route difficulty",
            (int)Math.Round(distanceKm),
            difficultyPts,
            $"Distance {distanceKm:N0} km — longer corridors add baseline risk."));
        score += difficultyPts;

        var accidentPts = Math.Min(35, accidentCount * 20);
        factors.Add(new AccidentRiskFactorDto(
            "accidents",
            "Accidents on route",
            accidentCount,
            accidentPts,
            accidentCount == 0 ? "No booking-linked accidents in period." : $"{accidentCount} accident(s) tied to this route."));
        score += accidentPts;

        var speedPts = Math.Min(30, speedViolationCount * 2);
        factors.Add(new AccidentRiskFactorDto(
            "speed",
            "Speed violations on route",
            speedViolationCount,
            speedPts,
            speedViolationCount == 0
                ? "No overspeed during trips on this route."
                : $"{speedViolationCount} overspeed during route trips."));
        score += speedPts;

        if (tripCount > 0)
        {
            factors.Add(new AccidentRiskFactorDto(
                "volume",
                "Trip volume",
                tripCount,
                0,
                $"{tripCount} booking(s) on this route in the lookback window."));
        }

        score = Math.Clamp(score, 0, 100);
        var band = BandFor(score);
        return new RouteAccidentRiskDto(routeId, routeLabel, distanceKm, tripCount, score, band, score >= HighRiskThreshold, factors);
    }

    public static int RouteDifficultyPoints(decimal distanceKm) => distanceKm switch
    {
        >= 500m => 15,
        >= 200m => 10,
        >= 80m => 6,
        _ => 3
    };

    public static string BandFor(int score) => score switch
    {
        >= HighRiskThreshold => "High",
        >= 40 => "Moderate",
        _ => "Low"
    };

    public static string FormatProactiveDriverMessage(DriverAccidentRiskDto row)
    {
        var top = row.Factors.Where(f => f.Points > 0).OrderByDescending(f => f.Points).Take(2)
            .Select(f => f.Label.ToLowerInvariant());
        var factorText = top.Any() ? string.Join(", ", top) : "multiple signals";
        return
            $"Proactive accident-risk warning: driver {row.DriverName} scored {row.RiskScore}/100 ({row.RiskBand}). " +
            $"Contributors: {factorText}. Review assignment before the next long trip.";
    }

    public static string FormatProactiveRouteMessage(RouteAccidentRiskDto row)
    {
        var top = row.Factors.Where(f => f.Points > 0).OrderByDescending(f => f.Points).Take(2)
            .Select(f => f.Label.ToLowerInvariant());
        var factorText = top.Any() ? string.Join(", ", top) : "multiple signals";
        return
            $"Proactive accident-risk warning: route {row.RouteLabel} scored {row.RiskScore}/100 ({row.RiskBand}). " +
            $"Contributors: {factorText}. Consider extra checks or driver pairing.";
    }
}
