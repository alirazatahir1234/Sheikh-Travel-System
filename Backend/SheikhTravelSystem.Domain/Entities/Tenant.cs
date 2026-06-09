namespace SheikhTravelSystem.Domain.Entities;

public class Tenant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; } = true;

    public string? TenantType { get; set; }
    public string? IndustryType { get; set; }
    public string StorageModel { get; set; } = "SharedDatabase";
    public string Status { get; set; } = "Active";
    public string? DataRegion { get; set; }
    public int? CreatedByUserId { get; set; }

    /// <summary>Legacy sync column — canonical source is TenantModules.</summary>
    public string? EnabledModulesJson { get; set; }
    /// <summary>Legacy sync column — canonical source is TenantSubscriptions.</summary>
    public string? SubscriptionPlan { get; set; }
    public string? PlanId { get; set; }

    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
