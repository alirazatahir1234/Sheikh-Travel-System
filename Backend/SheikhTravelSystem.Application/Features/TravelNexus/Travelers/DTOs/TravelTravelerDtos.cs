namespace SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;

/// <summary>
/// A passport holder belonging to a travel customer (the customer pays; travelers travel).
/// Passport and CNIC are masked unless the caller holds <c>Travel.Traveler.ViewPII</c>.
/// </summary>
public class TravelTravelerDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string? Title { get; set; }
    public string GivenNames { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string FullName => $"{GivenNames} {Surname}".Trim();
    public string Gender { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string? PlaceOfBirth { get; set; }
    public string? NationalityCode { get; set; }
    public string? NationalityName { get; set; }
    public string? PassportNumber { get; set; }
    public string? PassportIssuingCountryCode { get; set; }
    public string? PassportIssuingCountryName { get; set; }
    public DateTime? PassportIssueDate { get; set; }
    public DateTime? PassportExpiryDate { get; set; }
    /// <summary>Missing | Valid | ExpiringSoon | Expired (computed with tenant warning days).</summary>
    public string PassportStatus { get; set; } = "Missing";
    public string? Cnic { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int? MahramTravelerId { get; set; }
    public string? MahramName { get; set; }
    public string? MahramRelation { get; set; }
    public string? Notes { get; set; }
    public bool PiiMasked { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Create/update payload. On update, <c>PassportNumber</c>/<c>Cnic</c> null or a masked echo keeps the
/// stored value; empty string clears it.
/// </summary>
public record SaveTravelTravelerDto(
    string? Title,
    string GivenNames,
    string Surname,
    string Gender,
    DateTime? DateOfBirth,
    string? PlaceOfBirth,
    string? NationalityCode,
    string? PassportNumber,
    string? PassportIssuingCountryCode,
    DateTime? PassportIssueDate,
    DateTime? PassportExpiryDate,
    string? Cnic,
    string? Phone,
    string? Email,
    int? MahramTravelerId,
    string? MahramRelation,
    string? Notes);

/// <summary>Stored identity values, used to resolve "keep" semantics on update.</summary>
public sealed record TravelTravelerIdentity(int Id, int CustomerId, string Gender, string? PassportNumber, string? Cnic);
