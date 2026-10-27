using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Human-readable start/end labels for GPS trip lists (reverse-geocoded address or coordinates).
/// </summary>
public static class GpsTripLocationHelper
{
    public static bool HasUsableCoordinates(double? latitude, double? longitude)
        => latitude is not null
           && longitude is not null
           && !(Math.Abs(latitude.Value) < 1e-6 && Math.Abs(longitude.Value) < 1e-6);

    public static double? NormalizeLatitude(double lat, double lon)
        => HasUsableCoordinates(lat, lon) ? lat : null;

    public static double? NormalizeLongitude(double lat, double lon)
        => HasUsableCoordinates(lat, lon) ? lon : null;

    public static string FormatCoordinateLabel(double latitude, double longitude)
        => $"{latitude:F5}, {longitude:F5}";

    /// <summary>
    /// Prefer a stored address; fall back to GPS coordinates when address is missing or a placeholder.
    /// </summary>
    public static string? ResolveEndpointLabel(string? address, double? latitude, double? longitude)
    {
        var trimmed = address?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed)
            && !string.Equals(trimmed, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return TripReplayAddressEnricher.SanitizeFleetAddress(trimmed) ?? trimmed;
        }

        if (HasUsableCoordinates(latitude, longitude))
        {
            return FormatCoordinateLabel(latitude!.Value, longitude!.Value);
        }

        return null;
    }

    public static GpsTripDto ApplyDisplayAddresses(GpsTripDto trip)
    {
        var start = ResolveEndpointLabel(trip.StartAddress, trip.StartLatitude, trip.StartLongitude);
        var end = ResolveEndpointLabel(trip.EndAddress, trip.EndLatitude, trip.EndLongitude);
        if (string.Equals(start, end, StringComparison.OrdinalIgnoreCase)
            && start is not null
            && HasUsableCoordinates(trip.StartLatitude, trip.StartLongitude)
            && HasUsableCoordinates(trip.EndLatitude, trip.EndLongitude)
            && (trip.StartLatitude != trip.EndLatitude || trip.StartLongitude != trip.EndLongitude))
        {
            // Same geocoded label for different coordinates — show coords on the end point.
            end = FormatCoordinateLabel(trip.EndLatitude!.Value, trip.EndLongitude!.Value);
        }

        return trip with { StartAddress = start, EndAddress = end };
    }
}
