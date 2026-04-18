using SheikhTravelSystem.Domain.Common;

namespace SheikhTravelSystem.Domain.Entities;

public class Customer : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? CNIC { get; set; }
    public bool IsActive { get; set; } = true;
}
