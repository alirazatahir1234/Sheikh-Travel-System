namespace SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.DTOs;

public class TravelSupplierDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Airline | Hotel | BedBank | Consolidator | Transport | Other.</summary>
    public string SupplierType { get; set; } = string.Empty;
    public string? IataCode { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? CurrencyCode { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    /// <summary>Flight and hotel services that reference the supplier (delete is refused when &gt; 0).</summary>
    public int UsageCount { get; set; }
}

public record SaveTravelSupplierDto(string Name, string SupplierType, string? IataCode, string? ContactName, string? Phone,
    string? Email, string? CurrencyCode, bool IsActive, string? Notes);

public class TravelAirportDto
{
    public string IataCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string IanaTimeZone { get; set; } = string.Empty;
}
