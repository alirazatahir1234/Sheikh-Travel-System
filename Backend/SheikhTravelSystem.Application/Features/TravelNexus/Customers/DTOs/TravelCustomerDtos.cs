namespace SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;

/// <summary>
/// A travel customer is a <c>dbo.Customers</c> row (billing party) plus a 1:1 <c>TravelCustomerProfiles</c> row.
/// The public id is the <c>CustomerId</c>. For corporate customers <c>FullName</c> is the company name.
/// </summary>
public class TravelCustomerListItemDto
{
    public int CustomerId { get; set; }
    public string CustomerKind { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? City { get; set; }
    public int? AssignedAgentUserId { get; set; }
    public string? AssignedAgentName { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? LeadSourceName { get; set; }
    public int TravelerCount { get; set; }
    public DateTime? NextPassportExpiry { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TravelCustomerDto : TravelCustomerListItemDto
{
    public string? Address { get; set; }
    public string? Cnic { get; set; }
    public string? TaxNumber { get; set; }
    public string? AlternatePhone { get; set; }
    public int? LeadSourceId { get; set; }
    public string? Notes { get; set; }
    /// <summary>Sub-agent who referred the customer (default sub-agent on their invoices).</summary>
    public int? SubAgentId { get; set; }
    public string? SubAgentName { get; set; }
    /// <summary>True when <see cref="Cnic"/> was masked for this caller.</summary>
    public bool PiiMasked { get; set; }
}

/// <summary>
/// Create payload. Set <see cref="ExistingCustomerId"/> to attach a travel profile to a customer that
/// already exists in Nexus (other customer fields are then applied as updates).
/// </summary>
public record CreateTravelCustomerDto(
    int? ExistingCustomerId,
    string CustomerKind,
    string FullName,
    string Phone,
    string? Email,
    string? Address,
    string? City,
    string? Cnic,
    string? ContactPerson,
    string? TaxNumber,
    string? AlternatePhone,
    int? AssignedAgentUserId,
    int? BranchId,
    int? LeadSourceId,
    string? Notes,
    int? SubAgentId = null);

/// <summary>Update payload. <c>Cnic</c> null or a masked value keeps the stored CNIC; empty string clears it.</summary>
public record UpdateTravelCustomerDto(
    string CustomerKind,
    string FullName,
    string Phone,
    string? Email,
    string? Address,
    string? City,
    string? Cnic,
    string? ContactPerson,
    string? TaxNumber,
    string? AlternatePhone,
    int? AssignedAgentUserId,
    int? BranchId,
    int? LeadSourceId,
    string? Notes,
    int? SubAgentId = null);

/// <summary>Minimal reference used for scope checks by dependent features (travelers, enquiries…).</summary>
public sealed record TravelCustomerRef(int CustomerId, int? BranchId);

public enum ExistingCustomerState { NotFound, Available, AlreadyTravelCustomer }
