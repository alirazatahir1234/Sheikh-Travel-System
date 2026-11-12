using System.Data;
using Dapper;

namespace SheikhTravelSystem.Infrastructure.Persistence;

/// <summary>
/// Subscriptions are billed per licensed GPS device (<c>TenantSubscriptions.MaxGpsDevices</c>), so a company
/// cannot register more trackers than it has licensed. A NULL limit or a missing subscription row means no cap.
/// </summary>
public static class GpsDeviceLicenseGuard
{
    /// <summary>Returns an error message when the tenant has no free licensed device slot; otherwise null.</summary>
    public static async Task<string?> CheckCapacityAsync(IDbConnection connection, int tenantId, CancellationToken ct)
    {
        var row = await connection.QuerySingleOrDefaultAsync<(int? Licensed, int Used)>(new CommandDefinition("""
            SELECT
                (SELECT TOP 1 MaxGpsDevices FROM TenantSubscriptions WHERE TenantId = @TenantId) AS Licensed,
                (SELECT COUNT(*) FROM GpsDevices WHERE TenantId = @TenantId AND IsDeleted = 0) AS Used
            """, new { TenantId = tenantId }, cancellationToken: ct));

        return ExceedsLicense(row.Licensed, row.Used)
            ? $"Your subscription covers {row.Licensed} GPS device(s) and all are in use. " +
              "Contact SheikhGo to add more devices to your yearly plan."
            : null;
    }

    public static bool ExceedsLicense(int? licensed, int used)
        => licensed.HasValue && used >= licensed.Value;
}
