namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// TravelNexus (travel agency / visa / Umrah) permissions. Module code <see cref="TenantModuleCatalog.TravelAgencyModuleCode"/>.
/// Later phases append their codes here and seed them in their own migration.
/// </summary>
public static class TravelPermissions
{
    public const string View = "Travel.View";
    public const string Settings = "Travel.Settings";
    public const string CustomerView = "Travel.Customer.View";
    public const string CustomerManage = "Travel.Customer.Manage";

    /// <summary>Reveals full passport and CNIC numbers; everyone else sees masked values.</summary>
    public const string TravelerViewPii = "Travel.Traveler.ViewPII";

    public static readonly string[] All = [View, Settings, CustomerView, CustomerManage, TravelerViewPii];
}
