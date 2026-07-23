using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.Platform;

/// <summary>Shared license / plan reads for Stage 4.</summary>
internal static class LicenseQueries
{
    public static async Task<CompanyLicenseDto?> LoadCompanyLicenseAsync(
        IDbConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition("""
            SELECT
                t.Id AS TenantId,
                t.Name AS CompanyName,
                COALESCE(s.SubscriptionCode, sp.SubscriptionCode) AS SubscriptionCode,
                COALESCE(s.PlanName, t.SubscriptionPlan, sp.DisplayName) AS PlanName,
                COALESCE(sp.DisplayName, s.PlanName, t.SubscriptionPlan) AS PlanDisplayName,
                COALESCE(s.Status, N'Active') AS Status,
                s.SubscriptionStartDate AS StartDate,
                s.SubscriptionEndDate AS EndDate,
                COALESCE(s.AutoRenew, 1) AS AutoRenew,
                COALESCE(s.MaxUsers, sp.MaxUsers) AS MaxUsers,
                COALESCE(s.MaxDrivers, sp.MaxDrivers) AS MaxDrivers,
                COALESCE(s.MaxVehicles, sp.MaxVehicles) AS MaxVehicles,
                COALESCE(s.MaxBranches, sp.MaxBranches) AS MaxBranches,
                COALESCE(s.MaxGpsDevices, sp.MaxGpsDevices) AS MaxGpsDevices,
                COALESCE(s.StorageQuotaGb, sp.StorageQuotaGb) AS StorageQuotaGb,
                COALESCE(s.AICredits, sp.AICredits) AS AICredits,
                COALESCE(s.GPSEnabled, sp.GPSEnabled, 1) AS GPSEnabled,
                sp.DefaultModuleCodesJson,
                (SELECT COUNT(*) FROM Users u WHERE u.TenantId = t.Id AND u.IsDeleted = 0 AND u.IsActive = 1) AS UsedUsers,
                (SELECT COUNT(*) FROM Drivers d WHERE d.TenantId = t.Id AND d.IsDeleted = 0) AS UsedDrivers,
                (SELECT COUNT(*) FROM Vehicles v WHERE v.TenantId = t.Id AND v.IsDeleted = 0) AS UsedVehicles,
                (SELECT COUNT(*) FROM Branches b WHERE b.TenantId = t.Id) AS UsedBranches,
                (SELECT COUNT(*) FROM GpsDevices g
                 INNER JOIN Vehicles v ON v.Id = g.VehicleId AND v.TenantId = t.Id AND v.IsDeleted = 0
                 WHERE g.IsDeleted = 0) AS UsedGpsDevices
            FROM Tenants t
            LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
            LEFT JOIN SubscriptionPlans sp ON sp.SubscriptionCode = COALESCE(
                s.SubscriptionCode,
                CASE
                    WHEN UPPER(COALESCE(s.PlanName, t.SubscriptionPlan, N'')) LIKE N'%ENTERPRISE%' THEN N'ENTERPRISE'
                    WHEN UPPER(COALESCE(s.PlanName, t.SubscriptionPlan, N'')) LIKE N'%PRO%' THEN N'PRO'
                    WHEN UPPER(COALESCE(s.PlanName, t.SubscriptionPlan, N'')) LIKE N'%STARTER%' THEN N'STARTER'
                    ELSE N'ENTERPRISE'
                END)
            WHERE t.Id = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row is null) return null;

        var planName = (string?)row.PlanName;
        var subscriptionCode = (string?)row.SubscriptionCode;
        var licensedFromDb = SubscriptionPlanCatalog.ParseModuleCodes((string?)row.DefaultModuleCodesJson);
        var licensed = licensedFromDb.Count > 0
            ? licensedFromDb
            : SubscriptionPlanCatalog.LicensedModuleCodes(subscriptionCode ?? planName);

        var installed = (await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT m.ModuleCode
            FROM TenantModules tm
            INNER JOIN Modules m ON m.Id = tm.ModuleId
            WHERE tm.TenantId = @TenantId
            ORDER BY m.ModuleCode
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

        return new CompanyLicenseDto(
            CompanyId: (int)row.TenantId,
            TenantId: (int)row.TenantId,
            CompanyName: (string)row.CompanyName,
            SubscriptionCode: subscriptionCode,
            PlanName: planName,
            PlanDisplayName: (string?)row.PlanDisplayName,
            Status: (string)row.Status,
            StartDate: (DateTime?)row.StartDate,
            EndDate: (DateTime?)row.EndDate,
            AutoRenew: (bool)row.AutoRenew,
            LicensedModules: licensed.ToList(),
            InstalledModules: installed,
            MaxUsers: (int?)row.MaxUsers,
            MaxDrivers: (int?)row.MaxDrivers,
            MaxVehicles: (int?)row.MaxVehicles,
            MaxBranches: (int?)row.MaxBranches,
            MaxGpsDevices: (int?)row.MaxGpsDevices,
            StorageQuotaGb: (int?)row.StorageQuotaGb,
            AICredits: (int?)row.AICredits,
            GPSEnabled: (bool)row.GPSEnabled,
            UsedUsers: (int)row.UsedUsers,
            UsedDrivers: (int)row.UsedDrivers,
            UsedVehicles: (int)row.UsedVehicles,
            UsedBranches: (int)row.UsedBranches,
            UsedGpsDevices: (int)row.UsedGpsDevices);
    }

    public static async Task<string?> ResolvePlanNameAsync(
        IDbConnection connection,
        int tenantId,
        CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            SELECT COALESCE(s.PlanName, t.SubscriptionPlan, s.SubscriptionCode)
            FROM Tenants t
            LEFT JOIN TenantSubscriptions s ON s.TenantId = t.Id
            WHERE t.Id = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

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
