namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>Outcome of <see cref="GpsPositionQualityEvaluator"/> for one ingest.</summary>
public sealed record GpsPositionIngestPlan(
    bool RejectEntireIngest = false,
    bool WriteHistory = true,
    bool UpdateVclCoordinates = true,
    bool UpdateVclKinematics = true,
    bool IsGpsValid = true,
    string? QualityCode = null,
    int IdenticalCoordinateStreak = 0,
    bool SkipMovementAlerts = false,
    /// <summary>Overwrite stored ignition with NULL (ACC omitted + settled) — not OFF.</summary>
    bool ClearStickyIgnition = false);
