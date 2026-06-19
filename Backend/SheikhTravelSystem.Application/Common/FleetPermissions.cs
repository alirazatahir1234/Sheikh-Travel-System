namespace SheikhTravelSystem.Application.Common;

public static class FleetPermissions
{
    public const string VehicleView = "Vehicle.View";
    public const string VehicleCreate = "Vehicle.Create";
    public const string VehicleUpdate = "Vehicle.Update";
    public const string VehicleDelete = "Vehicle.Delete";

    public static readonly string[] All =
    [
        VehicleView,
        VehicleCreate,
        VehicleUpdate,
        VehicleDelete
    ];
}
