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

    /// <summary>Upload documents, request ad-hoc documents, remove ad-hoc requirements.</summary>
    public const string DocumentManage = "Travel.Document.Manage";

    /// <summary>Verify, reject or waive applicant documents.</summary>
    public const string DocumentVerify = "Travel.Document.Verify";

    /// <summary>Open document files (passport scans etc.). Every download is audited.</summary>
    public const string DocumentDownload = "Travel.Document.Download";

    public const string PackageView = "Travel.Package.View";
    /// <summary>Packages and the hotel catalog.</summary>
    public const string PackageManage = "Travel.Package.Manage";
    public const string GroupView = "Travel.Group.View";
    /// <summary>Groups, pilgrims, hotel stays, rooms and transport.</summary>
    public const string GroupManage = "Travel.Group.Manage";
    /// <summary>Pilgrim manifest (PII; every fetch is audited).</summary>
    public const string GroupManifest = "Travel.Group.Manifest";

    public const string SupplierView = "Travel.Supplier.View";
    /// <summary>Supplier profiles (airlines, hotels, bed banks, consolidators, transport).</summary>
    public const string SupplierManage = "Travel.Supplier.Manage";
    public const string BookingView = "Travel.Booking.View";
    /// <summary>Booking files, flights, hotels, passengers, tickets, check-in/out.</summary>
    public const string BookingManage = "Travel.Booking.Manage";
    /// <summary>Cancellations, penalties and refund tracking.</summary>
    public const string BookingCancel = "Travel.Booking.Cancel";

    /// <summary>Phase 1 permissions (seeded by TravelNexusFoundationMigration).</summary>
    public static readonly string[] Phase1 = [View, Settings, CustomerView, CustomerManage, TravelerViewPii];

    /// <summary>Phase 2 permissions (seeded by TravelNexusCrmVisaMigration).</summary>
    public static readonly string[] Phase2 = [EnquiryView, EnquiryManage, VisaView, VisaManage, VisaDecide];

    /// <summary>Phase 3 permissions (seeded by TravelNexusDocumentsMigration).</summary>
    public static readonly string[] Phase3 = [DocumentManage, DocumentVerify, DocumentDownload];

    /// <summary>Phase 4 permissions (seeded by TravelNexusUmrahMigration).</summary>
    public static readonly string[] Phase4 = [PackageView, PackageManage, GroupView, GroupManage, GroupManifest];

    /// <summary>Phase 5 permissions (seeded by TravelNexusBookingsMigration).</summary>
    public static readonly string[] Phase5 = [SupplierView, SupplierManage, BookingView, BookingManage, BookingCancel];

    public static readonly string[] All = [.. Phase1, .. Phase2, .. Phase3, .. Phase4, .. Phase5];
}
