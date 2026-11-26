using System.Text.RegularExpressions;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Common;

/// <summary>
/// Wire values for TravelNexus string enums. Mirrored as TypeScript unions in
/// <c>sheikhgo-erp/src/app/features/travel-nexus/models/travel.models.ts</c> and by CHECK constraints
/// in <c>TravelNexusFoundationMigration</c>. Change all three together.
/// </summary>
public static partial class TravelValues
{
    public static readonly string[] VisaCategories =
        ["Tourist", "Business", "Umrah", "Hajj", "Work", "Student", "Transit", "Family", "Other"];

    public static readonly string[] VisaEntryTypes = ["Single", "Double", "Multiple"];

    public static readonly string[] CustomerKinds = ["Individual", "Corporate"];

    public static readonly string[] Genders = ["Male", "Female"];

    public static readonly string[] MahramRelations =
        ["Husband", "Father", "Son", "Brother", "Grandfather", "Grandson", "Uncle", "Nephew", "FatherInLaw", "SonInLaw", "Other"];

    // ── Phase 2: CRM ──
    public static readonly string[] EnquiryServiceTypes = ["Visa", "Umrah", "Hajj", "Flight", "Hotel", "Package", "Other"];
    public static readonly string[] EnquiryStatuses = ["New", "Contacted", "Qualified", "Quoted", "Won", "Lost"];
    public static readonly string[] Priorities = ["Low", "Normal", "High"];
    public static readonly string[] ActivityTypes = ["Call", "WhatsApp", "Sms", "Email", "Meeting", "Note", "StatusChange"];
    /// <summary>Activity types a user may log (StatusChange is written by the system).</summary>
    public static readonly string[] UserActivityTypes = ["Call", "WhatsApp", "Sms", "Email", "Meeting", "Note"];
    public static readonly string[] ActivityDirections = ["Inbound", "Outbound", "Internal"];

    // ── Phase 2: visa cases ──
    public static readonly string[] VisaCaseStatuses =
        ["Draft", "DocumentsPending", "DocumentsVerified", "Submitted", "InProcessing",
         "Approved", "PartiallyApproved", "Rejected", "OnHold", "Cancelled"];
    public static readonly string[] ApplicantDecisions = ["Pending", "Approved", "Rejected", "Withdrawn"];
    public static readonly string[] VisaHistoryEventTypes =
        ["CaseStatus", "ApplicantAdded", "ApplicantRemoved", "ApplicantDecision",
         "DocumentUploaded", "DocumentVerified", "DocumentRejected", "DocumentWaived", "DocumentRequested", "DocumentRemoved"];

    // ── Phase 3: checklists & documents ──
    public static readonly string[] ChecklistStatuses = ["Draft", "Active", "Retired"];
    public static readonly string[] ApplicantConditions = ["All", "Adult", "Minor", "Male", "Female", "FemaleWithMahram"];
    public static readonly string[] ApplicantDocumentStatuses = ["Missing", "Uploaded", "Verified", "Rejected", "Waived"];
    public static readonly string[] DocumentHistoryEventTypes =
        ["DocumentUploaded", "DocumentVerified", "DocumentRejected", "DocumentWaived", "DocumentRequested", "DocumentRemoved"];

    // ── Phase 4: Umrah ──
    public static readonly string[] HotelCities = ["Makkah", "Madinah", "Jeddah", "Taif", "Other"];
    public static readonly string[] PackageTypes = ["Umrah", "Hajj"];
    public static readonly string[] PackageStatuses = ["Draft", "Published", "Archived"];
    public static readonly string[] RoomTypes = ["Quad", "Triple", "Double", "Single"];
    public static readonly string[] PriceCategories = ["Adult", "Child", "Infant"];
    public static readonly string[] GroupStatuses =
        ["Draft", "Open", "RegistrationClosed", "VisaProcessing", "Ready", "Departed", "Returned", "Closed", "Cancelled"];
    public static readonly string[] MemberStatuses = ["Registered", "Confirmed", "Cancelled"];
    public static readonly string[] GenderPolicies = ["Male", "Female", "Family"];
    public static readonly string[] TransportLegTypes = ["AirportTransfer", "IntercityTransfer", "Ziyarat", "Other"];
    public static readonly string[] GroupHistoryEventTypes =
        ["GroupStatus", "MemberRegistered", "MemberConfirmed", "MemberCancelled", "RoomAssigned", "RoomUnassigned", "LeaderChanged", "ManifestGenerated"];

    /// <summary>Beds per room type.</summary>
    public static int RoomCapacity(string roomType) => roomType switch { "Quad" => 4, "Triple" => 3, "Double" => 2, "Single" => 1, _ => 0 };

    // ── Phase 5: suppliers and bookings ──
    public static readonly string[] SupplierTypes = ["Airline", "Hotel", "BedBank", "Consolidator", "Transport", "Other"];
    public static readonly string[] BookingStatuses = ["Open", "Completed", "Cancelled"];
    public static readonly string[] FlightDirections = ["Outbound", "Return", "RoundTrip", "OneWay", "Internal"];
    public static readonly string[] FlightStatuses = ["Requested", "OnHold", "Confirmed", "Ticketed", "Cancelled"];
    public static readonly string[] CabinClasses = ["Economy", "PremiumEconomy", "Business", "First"];
    public static readonly string[] PassengerTypes = ["Adult", "Child", "Infant"];
    public static readonly string[] TicketStatuses = ["NotIssued", "Issued", "Voided", "Refunded"];
    public static readonly string[] HotelBookingStatuses = ["Requested", "Confirmed", "CheckedIn", "CheckedOut", "NoShow", "Cancelled"];
    public static readonly string[] MealPlans = ["RoomOnly", "Breakfast", "HalfBoard", "FullBoard"];
    public static readonly string[] BookingServiceTypes = ["Flight", "Hotel"];
    public static readonly string[] RefundStatuses = ["NotApplicable", "Pending", "ReceivedFromSupplier", "PaidToCustomer", "Closed"];
    public static readonly string[] BookingHistoryEventTypes =
        ["FileStatus", "FlightStatus", "HotelStatus", "TicketIssued", "TicketVoided", "Cancelled", "RefundStatus", "ServiceAdded", "ServiceUpdated"];

    // ── Phase 6A: customer finance ──
    public static readonly string[] InvoiceStatuses = ["Draft", "Issued", "Void"];
    /// <summary>Derived from the balance, never stored.</summary>
    public static readonly string[] InvoicePaymentStatuses = ["Unpaid", "PartiallyPaid", "Paid", "CreditDue"];
    public static readonly string[] InvoiceSourceTypes = ["Flight", "Hotel", "VisaServiceFee", "VisaEmbassyFee", "GroupMember"];
    public static readonly string[] PaymentMethods = ["Cash", "BankTransfer", "Card", "Cheque", "Online", "Other"];
    /// <summary>The Nexus payment vocabulary (Backend/CLAUDE.md). Pending = cheque or transfer not yet cleared.</summary>
    public static readonly string[] PaymentStatuses = ["Pending", "Completed", "Failed", "Refunded"];
    public static readonly string[] CreditNoteStatuses = ["Issued", "Void"];
    public static readonly string[] InstallmentStatuses = ["Paid", "PartlyPaid", "Due", "Overdue"];
    public static readonly string[] FinanceHistoryEventTypes =
        ["InvoiceCreated", "InvoiceUpdated", "InvoiceIssued", "InvoiceVoided", "PaymentRecorded", "PaymentStatus", "Allocated", "Unallocated",
         "CreditNoteIssued", "CreditNoteVoided", "RefundPaid", "Reconciled"];

    // ── Phase 6B: supplier finance, commissions ──
    public static readonly string[] SupplierRateTypes = ["HotelRoomNight", "Transfer", "Service"];
    public static readonly string[] SupplierCommissionServiceTypes = ["Flight", "Hotel", "Other"];
    public static readonly string[] SupplierCommissionBases = ["PercentOfCost", "FixedPerUnit"];
    public static readonly string[] SupplierBillStatuses = ["Draft", "Posted", "Void"];
    public static readonly string[] SupplierBillSourceTypes = ["Flight", "Hotel"];
    public static readonly string[] ContextTypes = ["Booking", "VisaCase", "Group"];
    public static readonly string[] BeneficiaryTypes = ["Staff", "SubAgent"];
    public static readonly string[] CommissionScopes = ["All", "Flight", "Hotel", "Visa", "Umrah"];
    public static readonly string[] CommissionBases = ["PercentOfMargin", "PercentOfSell"];
    /// <summary>Derived from the invoice balance, never stored.</summary>
    public static readonly string[] CommissionStatuses = ["Accrued", "Payable"];
    public static readonly string[] SupplierFinanceEventTypes =
        ["BillCreated", "BillUpdated", "BillPosted", "BillVoided", "PaymentRecorded", "PaymentStatus", "Allocated", "Unallocated",
         "CreditIssued", "CreditVoided", "RefundReceived", "Reconciled", "PayoutRecorded", "AttributionChanged"];

    /// <summary>Passport status values computed for travelers (not stored).</summary>
    public static readonly string[] PassportStatuses = ["Missing", "Valid", "ExpiringSoon", "Expired"];

    public static bool IsOneOf(string? value, string[] allowed)
        => value is not null && allowed.Contains(value, StringComparer.Ordinal);

    public static bool IsCurrencyCode(string? value)
        => value is not null && CurrencyRegex().IsMatch(value);

    public static bool IsCountryCode(string? value)
        => value is not null && CountryRegex().IsMatch(value);

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyRegex();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryRegex();
}
