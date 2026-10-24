using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.AI;

/// <summary>
/// Phase 3 — explainable predictive rules (no ML). Unit-testable; no I/O.
/// </summary>
public static class PredictiveIntelligenceRuleLogic
{
    public const int GpsStaleMinutes = 30;
    public const int LateStartGraceMinutes = 15;
    public const int MinFuelEntriesForBaseline = 3;
    public const int MinSpanDaysForFuelBaseline = 7;
    public const decimal MinKmForFuelRate = 100m;
    public const decimal FuelAnomalyVsMedianMultiplier = 1.5m;
    public const int MinFleetVehiclesForMedian = 2;

    /// <summary>
    /// Expected arrival/end from schedule only (no external Maps ETA).
    /// </summary>
    public static DateTime? ResolveExpectedEnd(
        DateTime plannedStart,
        DateTime? plannedEnd,
        int? estimatedDurationMinutes)
    {
        if (plannedEnd.HasValue) return plannedEnd.Value;
        if (estimatedDurationMinutes is > 0)
            return plannedStart.AddMinutes(estimatedDurationMinutes.Value);
        return null;
    }

    public static bool IsGpsStale(DateTime? lastGpsUtc, DateTime asOfUtc, int staleMinutes = GpsStaleMinutes)
    {
        if (!lastGpsUtc.HasValue) return true;
        return lastGpsUtc.Value < asOfUtc.AddMinutes(-staleMinutes);
    }

    public static bool IsActiveTripStatus(TripStatus status) =>
        status is TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute or TripStatus.Delayed;

    public static bool IsPreStartStatus(TripStatus status) =>
        status is TripStatus.Scheduled or TripStatus.DriverAssigned or TripStatus.VehicleAssigned;

    /// <summary>
    /// Schedule-based delay risk. Does not call Maps; stale GPS is returned separately.
    /// </summary>
    public static TripDelayAssessment EvaluateTripDelay(
        TripStatus status,
        DateTime plannedStart,
        DateTime? plannedEnd,
        int? estimatedDurationMinutes,
        DateTime? lastGpsUtc,
        DateTime asOfUtc)
    {
        var gpsStale = IsGpsStale(lastGpsUtc, asOfUtc);
        var expectedEnd = ResolveExpectedEnd(plannedStart, plannedEnd, estimatedDurationMinutes);

        if (status is TripStatus.Completed or TripStatus.Cancelled or TripStatus.Failed or TripStatus.Draft)
        {
            return new TripDelayAssessment(
                TripDelayRiskKind.None,
                false,
                expectedEnd,
                "Trip is not in an active/upcoming operational state.");
        }

        if (status == TripStatus.Delayed)
        {
            return new TripDelayAssessment(
                TripDelayRiskKind.AlreadyMarkedDelayed,
                gpsStale,
                expectedEnd,
                "Trip status is already Delayed.");
        }

        if (IsPreStartStatus(status))
        {
            var lateAfter = plannedStart.AddMinutes(LateStartGraceMinutes);
            if (asOfUtc > lateAfter)
            {
                var minutesLate = (int)Math.Round((asOfUtc - plannedStart).TotalMinutes);
                return new TripDelayAssessment(
                    TripDelayRiskKind.LateStart,
                    gpsStale,
                    expectedEnd,
                    $"Planned start {plannedStart:u}; still not started ({minutesLate} min late, " +
                    $"{LateStartGraceMinutes} min grace).");
            }

            return new TripDelayAssessment(
                TripDelayRiskKind.None,
                gpsStale && asOfUtc >= plannedStart.AddMinutes(-60),
                expectedEnd,
                "Within planned start window.");
        }

        if (IsActiveTripStatus(status))
        {
            if (expectedEnd is null)
            {
                return new TripDelayAssessment(
                    TripDelayRiskKind.InsufficientSchedule,
                    gpsStale,
                    null,
                    "Active trip has no PlannedEnd or EstimatedDurationMinutes — cannot assess arrival risk from schedule alone.");
            }

            if (asOfUtc > expectedEnd.Value)
            {
                var minutesOver = (int)Math.Round((asOfUtc - expectedEnd.Value).TotalMinutes);
                return new TripDelayAssessment(
                    TripDelayRiskKind.PastExpectedEnd,
                    gpsStale,
                    expectedEnd,
                    $"Past expected end {expectedEnd:u} by ~{minutesOver} min (schedule-based).");
            }

            var minutesLeft = (int)Math.Round((expectedEnd.Value - asOfUtc).TotalMinutes);
            if (minutesLeft <= 15)
            {
                return new TripDelayAssessment(
                    TripDelayRiskKind.ApproachingDeadline,
                    gpsStale,
                    expectedEnd,
                    $"Expected end {expectedEnd:u} in ~{minutesLeft} min.");
            }

            return new TripDelayAssessment(
                TripDelayRiskKind.None,
                gpsStale,
                expectedEnd,
                $"On schedule vs expected end {expectedEnd:u} (~{minutesLeft} min remaining).");
        }

        return new TripDelayAssessment(TripDelayRiskKind.None, gpsStale, expectedEnd, "No delay rule matched.");
    }

    public static bool HasSufficientFuelSample(int entryCount, int spanDays, decimal distanceKm) =>
        entryCount >= MinFuelEntriesForBaseline
        && spanDays >= MinSpanDaysForFuelBaseline
        && distanceKm >= MinKmForFuelRate;

    public static decimal? ComputeLitersPer100Km(decimal totalLiters, decimal startOdometer, decimal endOdometer)
    {
        var km = endOdometer - startOdometer;
        if (km < MinKmForFuelRate || totalLiters <= 0) return null;
        return Math.Round(totalLiters / km * 100m, 2);
    }

    public static decimal? Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0) return null;
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        if (sorted.Count % 2 == 1) return sorted[mid];
        return Math.Round((sorted[mid - 1] + sorted[mid]) / 2m, 2);
    }

    public static bool IsFuelRateAnomaly(
        decimal vehicleLitersPer100Km,
        decimal fleetMedianLitersPer100Km,
        decimal multiplier = FuelAnomalyVsMedianMultiplier) =>
        fleetMedianLitersPer100Km > 0
        && vehicleLitersPer100Km >= fleetMedianLitersPer100Km * multiplier;

    public static string FuelInvestigationWording(string vehicleLabel, decimal rate, decimal median, int entryCount) =>
        $"Investigation: {vehicleLabel} shows ~{rate:N1} L/100 km vs fleet median {median:N1} L/100 km " +
        $"({entryCount} fuel entries). Unusual vs baseline — review for data entry errors, route mix, or vehicle condition. " +
        "Not proof of misconduct.";
}

public enum TripDelayRiskKind
{
    None = 0,
    AlreadyMarkedDelayed = 1,
    LateStart = 2,
    PastExpectedEnd = 3,
    ApproachingDeadline = 4,
    InsufficientSchedule = 5
}

public readonly record struct TripDelayAssessment(
    TripDelayRiskKind Kind,
    bool GpsStale,
    DateTime? ExpectedEnd,
    string Evidence)
{
    public bool IsAtRisk => Kind is TripDelayRiskKind.AlreadyMarkedDelayed
        or TripDelayRiskKind.LateStart
        or TripDelayRiskKind.PastExpectedEnd
        or TripDelayRiskKind.ApproachingDeadline;
}
