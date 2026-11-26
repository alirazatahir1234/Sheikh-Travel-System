using Dapper;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services;

public sealed class TenantTimeZoneResolver(
    IDbConnectionFactory dbFactory,
    IOptions<LocalizationOptions> localization) : ITenantTimeZoneResolver
{
    public string DefaultTimeZoneId => localization.Value.EffectiveDefaultTimeZone;

    public async Task<string> GetTenantTimeZoneIdAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantTz = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT TOP 1 TimeZone FROM TenantBranding WHERE TenantId = @TenantId",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
        return Pick(null, tenantTz, DefaultTimeZoneId);
    }

    public async Task<string> GetVehicleTimeZoneIdAsync(int vehicleId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        // Rooted at the caller's tenant: another tenant's vehicle id yields no branch, only the caller's tenant zone.
        var row = await connection.QuerySingleOrDefaultAsync<(string? BranchTz, string? TenantTz)>(new CommandDefinition(
            """
            SELECT br.TimeZone AS BranchTz, tb.TimeZone AS TenantTz
            FROM Tenants t
            LEFT JOIN TenantBranding tb ON tb.TenantId = t.Id
            LEFT JOIN Vehicles v ON v.Id = @VehicleId AND v.TenantId = t.Id
            LEFT JOIN Branches br ON br.Id = v.BranchId AND br.TenantId = t.Id
            WHERE t.Id = @TenantId
            """,
            new { VehicleId = vehicleId, TenantId = tenantId },
            cancellationToken: cancellationToken));
        return Pick(row.BranchTz, row.TenantTz, DefaultTimeZoneId);
    }

    public async Task<TimeZoneInfo> GetTenantTimeZoneAsync(int tenantId, CancellationToken cancellationToken = default) =>
        TenantTimeZoneHelper.Resolve(await GetTenantTimeZoneIdAsync(tenantId, cancellationToken), DefaultTimeZoneId);

    /// <summary>Branch → tenant → default; blank or unresolvable ids are skipped.</summary>
    internal static string Pick(string? branchTz, string? tenantTz, string defaultTz)
    {
        if (TenantTimeZoneHelper.IsValid(branchTz))
            return branchTz!.Trim();
        if (TenantTimeZoneHelper.IsValid(tenantTz))
            return tenantTz!.Trim();
        return defaultTz;
    }
}
