namespace SheikhTravelSystem.Application.Common;

public static class TripPermissions
{
    public const string TripView = "Trip.View";
    public const string TripExport = "Trip.Export";

    public static readonly string[] All = [TripView, TripExport];
}
