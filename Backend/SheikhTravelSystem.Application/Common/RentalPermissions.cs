namespace SheikhTravelSystem.Application.Common;

public static class RentalPermissions
{
    public const string View = "Rental.View";
    public const string Manage = "Rental.Manage";
    public const string Rates = "Rental.Rates";

    public static readonly string[] All = [View, Manage, Rates];
}
