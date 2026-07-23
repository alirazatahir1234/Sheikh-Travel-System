namespace SheikhTravelSystem.Application.Common;

public static class OperationsPermissions
{
    public const string BookingView = "Booking.View";
    public const string BookingCreate = "Booking.Create";
    public const string TripView = "Trip.View";
    public const string RouteView = "Route.View";

    public static readonly string[] All =
    [
        BookingView,
        BookingCreate,
        TripView,
        RouteView
    ];
}
