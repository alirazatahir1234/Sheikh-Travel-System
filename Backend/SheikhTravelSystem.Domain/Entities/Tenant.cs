namespace SheikhTravelSystem.Domain.Entities;

public class Tenant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    /// <summary>JSON array of enabled module keys, e.g. ["bookings","gps-tracking"].</summary>
    public string? EnabledModulesJson { get; set; }
    public string? PlanId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
