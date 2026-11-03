namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Optional Traccar/device metadata for ingest quality gates. Null on driver-app HTTP ingest.
/// </summary>
public sealed record PositionIngestSourceContext(
    bool? TraccarValid = null,
    bool? TraccarOutdated = null,
    double? TraccarAccuracyMeters = null,
    DateTime? FixTimeUtc = null,
    int? SatelliteCount = null,
    bool? LbsOnlyFix = null,
    /// <summary>
    /// When true, overwrite VCL/device LastIgnition with NULL (clear sticky ON)
    /// without inventing Ignition OFF.
    /// </summary>
    bool ClearStickyIgnition = false);
