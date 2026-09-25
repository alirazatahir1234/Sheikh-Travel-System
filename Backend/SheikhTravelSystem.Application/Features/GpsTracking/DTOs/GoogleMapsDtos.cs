namespace SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

public record LatLngPoint(double Latitude, double Longitude);

public record GoogleRouteResult(
    int DistanceMeters,
    int DurationSeconds,
    string? EncodedPolyline,
    IReadOnlyList<LatLngPoint>? Points);

public record GoogleTimeZoneResult(
    string TimeZoneId,
    int RawOffsetSeconds,
    int DstOffsetSeconds);

public record SnappedGpsPoint(
    double Latitude,
    double Longitude,
    int? OriginalIndex);

public record OptimizeDispatchVehicleDto(
    string Label,
    double StartLatitude,
    double StartLongitude,
    double EndLatitude,
    double EndLongitude,
    int? Capacity = null);

public record OptimizeDispatchShipmentDto(
    string Label,
    double Latitude,
    double Longitude,
    int? LoadDemand = null,
    int ServiceDurationSeconds = 300);

public record OptimizeDispatchToursRequestDto(
    string? ProjectId,
    IReadOnlyList<OptimizeDispatchVehicleDto> Vehicles,
    IReadOnlyList<OptimizeDispatchShipmentDto> Shipments);

public record OptimizedTourVisitDto(string Label, int Sequence);

public record OptimizedTourDto(
    string VehicleLabel,
    IReadOnlyList<OptimizedTourVisitDto> Visits);

public record OptimizeDispatchToursResultDto(
    IReadOnlyList<OptimizedTourDto> Tours);

public record StaticMapRequestDto(
    double CenterLatitude,
    double CenterLongitude,
    int Zoom = 14,
    int Width = 640,
    int Height = 480,
    IReadOnlyList<LatLngPoint>? Markers = null,
    IReadOnlyList<LatLngPoint>? Path = null);

public record StreetViewRequestDto(
    double Latitude,
    double Longitude,
    int Width = 600,
    int Height = 400,
    int? Heading = null,
    int? Pitch = null,
    int? Fov = null);

/// <summary>Fleet Live Map nearby place (Places API New).</summary>
public record NearbyPlaceDto(
    string PlaceId,
    string Name,
    string? Address,
    double Latitude,
    double Longitude,
    double? DistanceMeters,
    double? Rating,
    int? UserRatingCount,
    IReadOnlyList<string> Types,
    string Category,
    bool? OpenNow = null,
    string? OpeningStatus = null,
    string? GoogleMapsUri = null,
    /// <summary>First photo resource name from Nearby Search (e.g. places/.../photos/...). Not a browser image URL.</summary>
    string? PhotoResourceName = null,
    /// <summary>Author attribution display names for the first photo when Google returns them.</summary>
    IReadOnlyList<string>? PhotoAttributions = null,
    /// <summary>Resolved browser image URL from Place Photos media (null until resolved).</summary>
    string? PhotoUrl = null);

/// <summary>Resolved Place Photos (New) media for a selected nearby place.</summary>
public record NearbyPlacePhotoDto(
    string? PhotoUrl,
    IReadOnlyList<string> Attributions);
