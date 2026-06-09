using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Domain.Constants;

namespace SheikhTravelSystem.Application.Features.Tenants;

public record ProvisionTenantCommand : IRequest<ApiResponse<int>>
{
    // 1. Tenant profile
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string TenantType { get; init; } = "Travel Agency";
    public string IndustryType { get; init; } = "Logistics & Transport";
    public string StorageModel { get; init; } = "SharedDatabase";
    public string Status { get; init; } = "Active";
    public string? DataRegion { get; init; } = "UAE";

    // 2. Subscription & modules
    public string? PlanName { get; init; } = "Enterprise";
    public int? MaxUsers { get; init; } = 100;
    public int? MaxVehicles { get; init; } = 500;
    public int? MaxDrivers { get; init; } = 500;
    public int? MaxBranches { get; init; } = 50;
    public int? MaxGpsDevices { get; init; } = 500;
    public IReadOnlyList<string>? ModuleCodes { get; init; }

    // 3. Initial admin
    public string AdminFullName { get; init; } = string.Empty;
    public string AdminEmail { get; init; } = string.Empty;
    public string AdminPassword { get; init; } = string.Empty;
    public string? AdminMobile { get; init; }

    // 4. Branding & localization
    public string? Country { get; init; } = "United Arab Emirates";
    public string? CurrencyCode { get; init; } = AppConstants.DefaultCurrency;
    public string? TimeZone { get; init; } = "Asia/Dubai";
    public string? PrimaryColor { get; init; } = "#007657";
    public string? Website { get; init; }
    public string? SupportEmail { get; init; }
    public string? LogoUrl { get; init; }

    // 5. Security
    public bool IsMfaRequired { get; init; }
    public int PasswordExpiryDays { get; init; } = 90;
    public int SessionTimeoutMinutes { get; init; } = 30;
    public bool IsGdprEnabled { get; init; } = true;
    public bool IsAuditLoggingEnabled { get; init; } = true;
    public bool IsVatEnabled { get; init; }

    // 6. Organization bootstrap
    public bool GenerateOrganizationStructure { get; init; } = true;
    public string? HeadOfficeName { get; init; } = "Head Office";
    public string? DefaultBranchName { get; init; } = "Main Operations Center";
    public string? DefaultDepartments { get; init; } = "Operations,Finance,Fleet,HR";

    // 7. Billing
    public string? BillingContactName { get; init; }
    public string? BillingEmail { get; init; }
    public string? BillingAddress { get; init; }
    public string? CompanyTRN { get; init; }

    // 8. GPS provisioning
    public string? GpsProviderName { get; init; }
}
