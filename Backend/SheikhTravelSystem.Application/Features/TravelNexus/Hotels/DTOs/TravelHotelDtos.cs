namespace SheikhTravelSystem.Application.Features.TravelNexus.Hotels.DTOs;

public class TravelHotelDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Makkah | Madinah | Jeddah | Taif | Other.</summary>
    public string City { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "SA";
    public int? StarRating { get; set; }
    public int? DistanceToHaramMeters { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
}

public record SaveTravelHotelDto(string Name, string City, string CountryCode, int? StarRating, int? DistanceToHaramMeters,
    string? Address, string? Phone, bool IsActive);
