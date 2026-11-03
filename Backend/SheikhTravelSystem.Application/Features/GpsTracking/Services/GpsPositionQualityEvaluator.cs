using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Detects Traccar invalid/LBS fixes, frozen coordinates with bogus speed, and impossible teleports.
/// Pure logic — no I/O.
/// </summary>
public static class GpsPositionQualityEvaluator
{
    public const string QualityTraccarInvalid = "traccar_invalid";
    public const string QualityTraccarOutdated = "traccar_outdated";
    public const string QualityLbsOnly = "lbs_only";
    public const string QualityZeroAccuracy = "zero_accuracy";
    public const string QualityFrozenCoordinates = "frozen_coordinates";
    public const string QualityImpossibleTeleport = "impossible_teleport";

    public static bool ShouldRejectTraccarPosition(
        PositionIngestSourceContext? source,
        GpsPositionQualityOptions options)
    {
        if (source is null || !options.RejectInvalidTraccarFixes)
            return false;

        if (source.TraccarValid == false)
            return true;

        if (options.RejectOutdatedTraccarFixes && source.TraccarOutdated == true)
            return true;

        if (options.RejectLbsOnlyFixes && source.LbsOnlyFix == true)
            return true;

        if (options.RejectZeroAccuracyFixes
            && source.TraccarAccuracyMeters is <= 0)
            return true;

        return false;
    }

    public static GpsPositionIngestPlan Evaluate(
        IngestPositionDto incoming,
        VehicleLocationSnapshot? previous,
        PositionIngestSourceContext? source,
        GpsPositionQualityOptions options,
        DateTime recordedAtUtc)
    {
        var clearSticky = source?.ClearStickyIgnition == true;

        if (ShouldRejectTraccarPosition(source, options))
        {
            return new GpsPositionIngestPlan(
                RejectEntireIngest: true,
                WriteHistory: false,
                UpdateVclCoordinates: false,
                UpdateVclKinematics: false,
                IsGpsValid: false,
                QualityCode: ResolveRejectCode(source),
                IdenticalCoordinateStreak: previous?.IdenticalCoordinateStreak ?? 0,
                SkipMovementAlerts: true,
                ClearStickyIgnition: clearSticky);
        }

        var streak = 0;
        var qualityCode = (string?)null;
        var isValid = true;
        var updateCoords = true;
        var updateKinematics = true;
        var skipAlerts = false;

        if (previous?.Latitude is double pLat && previous.Longitude is double pLng)
        {
            var distM = HaversineMeters(pLat, pLng, incoming.Latitude, incoming.Longitude);
            var epsilon = options.CoordinateEpsilonMeters <= 0 ? 15 : options.CoordinateEpsilonMeters;
            var coordsUnchanged = distM <= epsilon;

            if (!coordsUnchanged)
            {
                streak = 0;
            }
            else
            {
                streak = (previous.IdenticalCoordinateStreak ?? 0) + 1;
            }

            var timeSeconds = previous.LastUpdateUtc is { } last
                ? Math.Max(0, (recordedAtUtc - last).TotalSeconds)
                : 0;

            // Prefer device fix-time delta over ingest wall clock (sync polls can be seconds apart).
            if (source?.FixTimeUtc is { } fixUtc && previous.LastUpdateUtc is { } prevUtc)
            {
                timeSeconds = Math.Max(0, (fixUtc - prevUtc).TotalSeconds);
            }

            var distKm = distM / 1000.0;
            var recoveringFromUnreliableFix = previous.IsGpsValid == false;

            if (!coordsUnchanged
                && !recoveringFromUnreliableFix
                && timeSeconds > 0
                && timeSeconds <= options.TeleportMaxSeconds)
            {
                var maxKm = (timeSeconds / 3600.0) * (double)options.TeleportMaxPlausibleSpeedKmh;
                if (distKm >= options.TeleportMinDistanceKm && distKm > maxKm)
                {
                    isValid = false;
                    qualityCode = QualityImpossibleTeleport;
                    updateCoords = false;
                    updateKinematics = false;
                    skipAlerts = true;
                }
            }

            // Valid GNSS reacquisition after a stale anchor (e.g. Lahore LBS while vehicle is in Pasrur).
            if (!coordsUnchanged
                && distKm >= options.TeleportMinDistanceKm
                && IsTrustworthyTraccarGnssFix(source))
            {
                isValid = true;
                qualityCode = qualityCode == QualityImpossibleTeleport ? null : qualityCode;
                updateCoords = true;
                updateKinematics = true;
                skipAlerts = qualityCode == QualityFrozenCoordinates;
            }

            var frozenSpeedFloor = options.FrozenMinSpeedKmh <= 0 ? 5m : options.FrozenMinSpeedKmh;
            var freezeAfter = options.FreezeVclAfterIdenticalPoints <= 0 ? 3 : options.FreezeVclAfterIdenticalPoints;

            if (coordsUnchanged
                && incoming.Speed >= frozenSpeedFloor
                && streak >= freezeAfter)
            {
                isValid = false;
                qualityCode ??= QualityFrozenCoordinates;
                updateCoords = false;
                updateKinematics = false;
                skipAlerts = true;
            }
            else if (coordsUnchanged
                     && incoming.Speed >= frozenSpeedFloor
                     && streak >= Math.Max(1, freezeAfter - 1))
            {
                // Early warning: still show last good coords but flag unreliable before full freeze.
                isValid = false;
                qualityCode ??= QualityFrozenCoordinates;
                skipAlerts = true;
            }
        }

        if (source?.LbsOnlyFix == true && isValid)
        {
            isValid = false;
            qualityCode ??= QualityLbsOnly;
        }

        if (source?.TraccarOutdated == true && isValid && options.FlagOutdatedWithoutReject)
        {
            isValid = false;
            qualityCode ??= QualityTraccarOutdated;
        }

        return new GpsPositionIngestPlan(
            WriteHistory: true,
            UpdateVclCoordinates: updateCoords,
            UpdateVclKinematics: updateKinematics,
            IsGpsValid: isValid,
            QualityCode: qualityCode,
            IdenticalCoordinateStreak: streak,
            SkipMovementAlerts: skipAlerts,
            ClearStickyIgnition: clearSticky);
    }

    public static bool IsLbsOnlyFromTraccar(bool valid, int? satelliteCount, double accuracyMeters)
    {
        if (!valid)
            return true;

        if (satelliteCount is 0)
            return true;

        // Traccar often reports accuracy 0 for cell/LBS fixes on Jimi.
        if (accuracyMeters <= 0 && satelliteCount is null or 0)
            return true;

        return false;
    }

    private static bool IsTrustworthyTraccarGnssFix(PositionIngestSourceContext? source)
    {
        if (source is null)
            return false;

        if (source.TraccarValid == false)
            return false;

        if (source.LbsOnlyFix == true)
            return false;

        return source.SatelliteCount is > 0;
    }

    private static string? ResolveRejectCode(PositionIngestSourceContext? source)
    {
        if (source?.TraccarValid == false)
            return QualityTraccarInvalid;
        if (source?.TraccarOutdated == true)
            return QualityTraccarOutdated;
        if (source?.LbsOnlyFix == true)
            return QualityLbsOnly;
        if (source?.TraccarAccuracyMeters is <= 0)
            return QualityZeroAccuracy;
        return QualityTraccarInvalid;
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

public sealed record VehicleLocationSnapshot(
    decimal? Speed,
    bool? Ignition,
    double? Latitude,
    double? Longitude,
    string? Address,
    int? IdenticalCoordinateStreak,
    bool? IsGpsValid,
    DateTime? LastUpdateUtc);
