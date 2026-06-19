namespace SheikhTravelSystem.Application.Common;

public static class DriverPermissions
{
    public const string DriverView = "Driver.View";
    public const string DriverCreate = "Driver.Create";
    public const string DriverUpdate = "Driver.Update";
    public const string DriverDelete = "Driver.Delete";
    public const string DriverAssign = "Driver.Assign";

    public static readonly string[] All =
    [
        DriverView,
        DriverCreate,
        DriverUpdate,
        DriverDelete,
        DriverAssign
    ];
}
