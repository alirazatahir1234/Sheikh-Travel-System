namespace SheikhTravelSystem.Application.Features.GpsTracking;

/// <summary>
/// How a tenant produces operational trip records alongside GPS telemetry.
/// Booking = manual/booking-driven trips on <c>/trips</c> (e.g. Jonesy); GPS trips stay segment-detected.
/// AutoGpsDaily = one GPS trip per vehicle per local day from movement (no booking trip required).
/// </summary>
public enum FleetTripCreationMode
{
    Booking = 0,
    AutoGpsDaily = 1
}

public static class FleetTripCreationModeNames
{
    public const string Booking = "Booking";
    public const string AutoGpsDaily = "AutoGpsDaily";

    public static string ToStorage(FleetTripCreationMode mode) =>
        mode switch
        {
            FleetTripCreationMode.AutoGpsDaily => AutoGpsDaily,
            _ => Booking
        };

    public static FleetTripCreationMode Parse(string? value) =>
        string.Equals(value, AutoGpsDaily, StringComparison.OrdinalIgnoreCase)
            ? FleetTripCreationMode.AutoGpsDaily
            : FleetTripCreationMode.Booking;
}
