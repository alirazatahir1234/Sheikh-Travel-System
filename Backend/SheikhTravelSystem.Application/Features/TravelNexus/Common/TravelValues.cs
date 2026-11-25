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
