using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.Platform;

/// <summary>Shared license / plan mapping helpers for Stage 4 (SQL lives in IPlatformRepository).</summary>
internal static class LicenseQueries
{
    public static IReadOnlyList<SubscriptionPlanDto> FromSeed()
        => SubscriptionPlanCatalog.All
            .Where(p => p.Visible)
            .OrderBy(p => p.SortOrder)
            .Select(ToDto)
            .ToList();

    public static SubscriptionPlanDto ToDto(SubscriptionPlanCatalog.Entry e)
        => new(
            e.SubscriptionCode,
            e.DisplayName,
            e.Description,
            e.PlanType,
            e.Status,
            e.SortOrder,
            e.DurationMonths,
            e.IsDefault,
            e.Visible,
            e.DocumentationUrl,
            e.DefaultModuleCodes,
            e.MaxUsers,
            e.MaxVehicles,
            e.MaxDrivers,
            e.MaxBranches,
            e.MaxGpsDevices,
            e.StorageQuotaGb,
            e.AICredits,
            e.GPSEnabled);

    public static LicenseSummaryDto ToSummary(CompanyLicenseDto license)
        => new(
            license.SubscriptionCode,
            license.PlanName,
            license.PlanDisplayName,
            license.Status,
            license.StartDate,
            license.EndDate,
            license.AutoRenew,
            license.LicensedModules,
            license.MaxUsers,
            license.MaxDrivers,
            license.MaxVehicles,
            license.StorageQuotaGb,
            license.AICredits,
            license.GPSEnabled);
}
