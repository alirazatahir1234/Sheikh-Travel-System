using SheikhTravelSystem.Domain.Common;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Domain.Entities;

public class Driver : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public DateTime LicenseExpiryDate { get; set; }
    public string? CNIC { get; set; }
    public string? Address { get; set; }
    public string? DriverCode { get; set; }
    public string? Nationality { get; set; }
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContact { get; set; }
    public DateTime? HireDate { get; set; }
    public string? PhotoUrl { get; set; }
    public string VerificationStatus { get; set; } = "Pending";
    public int? BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public DriverStatus Status { get; set; } = DriverStatus.Available;
    public bool IsActive { get; set; } = true;
    public int? UserId { get; set; }
    public decimal? Rating { get; set; }
    public int? YearsExperience { get; set; }
}
