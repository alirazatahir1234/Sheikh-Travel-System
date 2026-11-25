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

    public const string EnquiryView = "Travel.Enquiry.View";
    public const string EnquiryManage = "Travel.Enquiry.Manage";
    public const string VisaView = "Travel.Visa.View";
    public const string VisaManage = "Travel.Visa.Manage";

    /// <summary>Record embassy decisions (approve / reject applicants). Separate from data entry.</summary>
    public const string VisaDecide = "Travel.Visa.Decide";

    /// <summary>Phase 1 permissions (seeded by TravelNexusFoundationMigration).</summary>
    public static readonly string[] Phase1 = [View, Settings, CustomerView, CustomerManage, TravelerViewPii];

    /// <summary>Phase 2 permissions (seeded by TravelNexusCrmVisaMigration).</summary>
    public static readonly string[] Phase2 = [EnquiryView, EnquiryManage, VisaView, VisaManage, VisaDecide];

    public static readonly string[] All = [.. Phase1, .. Phase2];
}
