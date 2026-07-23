namespace SheikhTravelSystem.Application.Common;

public static class OperationsPermissions
{
    public const string BookingView = "Booking.View";
    public const string BookingCreate = "Booking.Create";
    public const string BookingUpdate = "Booking.Update";
    public const string BookingDelete = "Booking.Delete";
    public const string TripView = "Trip.View";
    public const string TripCreate = "Trip.Create";
    public const string TripUpdate = "Trip.Update";
    public const string TripDelete = "Trip.Delete";
    public const string TripAssign = "Trip.Assign";
    public const string RouteView = "Route.View";
    public const string RouteCreate = "Route.Create";
    public const string RouteUpdate = "Route.Update";
    public const string RouteDelete = "Route.Delete";

    public static readonly string[] All =
    [
        BookingView,
        BookingCreate,
        BookingUpdate,
        BookingDelete,
        TripView,
        TripCreate,
        TripUpdate,
        TripDelete,
        TripAssign,
        RouteView,
        RouteCreate,
        RouteUpdate,
        RouteDelete
    ];
}
