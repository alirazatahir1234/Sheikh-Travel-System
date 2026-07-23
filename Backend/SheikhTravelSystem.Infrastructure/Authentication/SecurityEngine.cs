using System.Net;
using System.Net.Sockets;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Authentication;

public class SecurityEngine(IDbConnectionFactory dbFactory) : ISecurityEngine
{
    public async Task<IReadOnlyList<SecurityPolicyDefinitionDto>> GetCatalogAsync(
        bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT PolicyKey, DisplayName, Category, Description, DefaultValue, ValueType,
                   SortOrder, Visible, IsActive, IsSystem
            FROM SecurityPolicyDefinitions
            WHERE (@ActiveOnly = 0 OR (IsActive = 1 AND Visible = 1))
            ORDER BY Category, SortOrder, DisplayName
            """;
        var rows = await connection.QueryAsync<SecurityPolicyDefinitionDto>(new CommandDefinition(
            sql, new { ActiveOnly = activeOnly ? 1 : 0 }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<SecurityPolicyValueDto>> GetCompanyPoliciesAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<SecurityPolicyValueDto>(new CommandDefinition("""
            SELECT d.PolicyKey, d.DisplayName, d.Category, d.Description, d.ValueType, d.DefaultValue,
                   COALESCE(p.PolicyValue, d.DefaultValue) AS EffectiveValue,
                   p.PolicyValue AS TenantValue,
                   p.UpdatedDate, p.UpdatedBy,
                   CAST(CASE WHEN p.PolicyKey IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsOverridden
            FROM SecurityPolicyDefinitions d
            LEFT JOIN TenantSecurityPolicies p
                ON p.PolicyKey = d.PolicyKey AND p.TenantId = @TenantId
            WHERE d.IsActive = 1 AND d.Visible = 1
            ORDER BY d.Category, d.SortOrder, d.DisplayName
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task SetCompanyPoliciesAsync(
        int tenantId,
        IReadOnlyDictionary<string, string> values,
        int? updatedBy,
        CancellationToken cancellationToken = default)
    {
        if (values.Count == 0) return;

        using var connection = dbFactory.CreateConnection();
        var known = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT PolicyKey FROM SecurityPolicyDefinitions WHERE IsActive = 1",
            cancellationToken: cancellationToken))).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in values)
        {
            if (!known.Contains(key)) continue;
            var normalized = value ?? string.Empty;
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM TenantSecurityPolicies WHERE TenantId = @TenantId AND PolicyKey = @PolicyKey)
                    UPDATE TenantSecurityPolicies
                    SET PolicyValue = @PolicyValue, UpdatedBy = @UpdatedBy, UpdatedDate = SYSUTCDATETIME()
                    WHERE TenantId = @TenantId AND PolicyKey = @PolicyKey;
                ELSE
                    INSERT INTO TenantSecurityPolicies (TenantId, PolicyKey, PolicyValue, UpdatedBy, UpdatedDate)
                    VALUES (@TenantId, @PolicyKey, @PolicyValue, @UpdatedBy, SYSUTCDATETIME());
                """,
                new
                {
                    TenantId = tenantId,
                    PolicyKey = key,
                    PolicyValue = normalized,
                    UpdatedBy = updatedBy
                },
                cancellationToken: cancellationToken));
        }

        await DualWriteLegacyAsync(connection, tenantId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetEffectiveMapAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        var policies = await GetCompanyPoliciesAsync(tenantId, cancellationToken);
        return policies.ToDictionary(
            p => p.PolicyKey,
            p => p.EffectiveValue ?? p.DefaultValue ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<SecurityCompanySummaryDto> GetSafeSummaryAsync(
        int tenantId,
        DateTime? passwordChangedAt = null,
        CancellationToken cancellationToken = default)
    {
        var map = await GetEffectiveMapAsync(tenantId, cancellationToken);
        var maxAge = GetInt(map, SecurityPolicyKeys.PasswordMaxAgeDays, 90);
        var idle = GetInt(map, SecurityPolicyKeys.SessionIdleTimeoutMinutes, 30);
        var absolute = GetInt(map, SecurityPolicyKeys.SessionAbsoluteTimeoutMinutes, 480);
        var maxAttempts = GetInt(map, SecurityPolicyKeys.LockoutMaxAttempts, 5);
        var auditLevel = GetString(map, SecurityPolicyKeys.AuditLevel, "Always");
        var mfa = GetBool(map, SecurityPolicyKeys.ComplianceMfaRequired, false);

        int? daysRemaining = null;
        var expired = false;
        if (maxAge > 0 && passwordChangedAt.HasValue)
        {
            var ageDays = (int)(DateTime.UtcNow.Date - passwordChangedAt.Value.Date).TotalDays;
            daysRemaining = Math.Max(0, maxAge - ageDays);
            expired = ageDays >= maxAge;
        }

        return new SecurityCompanySummaryDto(
            PasswordExpiryDays: maxAge > 0 ? maxAge : null,
            IdleTimeoutMinutes: idle > 0 ? idle : null,
            AbsoluteTimeoutMinutes: absolute > 0 ? absolute : null,
            AuditLevel: auditLevel,
            LockoutEnabled: maxAttempts > 0,
            MfaRequired: mfa,
            PasswordExpired: expired,
            PasswordDaysRemaining: daysRemaining);
    }

    public int GetInt(IReadOnlyDictionary<string, string> map, string key, int fallback)
    {
        if (!map.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return fallback;
        return int.TryParse(raw.Trim(), out var n) ? n : fallback;
    }

    public bool GetBool(IReadOnlyDictionary<string, string> map, string key, bool fallback)
    {
        if (!map.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return fallback;
        raw = raw.Trim();
        if (bool.TryParse(raw, out var b)) return b;
        if (raw is "1" or "yes" or "on") return true;
        if (raw is "0" or "no" or "off") return false;
        return fallback;
    }

    public string GetString(IReadOnlyDictionary<string, string> map, string key, string fallback)
    {
        if (!map.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return fallback;
        return raw.Trim();
    }

    public bool IsPasswordExpired(DateTime? passwordChangedAt, int maxAgeDays)
    {
        if (maxAgeDays <= 0 || passwordChangedAt is null) return false;
        return (DateTime.UtcNow.Date - passwordChangedAt.Value.Date).TotalDays >= maxAgeDays;
    }

    public bool IsClientIpAllowed(string? clientIp, bool restrictEnabled, string? allowedCidrs)
    {
        if (!restrictEnabled) return true;
        if (string.IsNullOrWhiteSpace(allowedCidrs)) return true;
        if (string.IsNullOrWhiteSpace(clientIp)) return true; // soft: missing IP does not hard-fail
        if (!IPAddress.TryParse(clientIp.Trim(), out var ip)) return true;

        foreach (var line in allowedCidrs.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (CidrContains(line, ip)) return true;
        }

        return false;
    }

    public bool ShouldWriteAudit(string auditLevel, bool isError = false, bool isCritical = false)
    {
        var level = (auditLevel ?? "Always").Trim();
        return level.ToLowerInvariant() switch
        {
            "disabled" => false,
            "critical" => isCritical,
            "errors" => isError || isCritical,
            _ => true // Always
        };
    }

    private static async Task DualWriteLegacyAsync(
        System.Data.IDbConnection connection, int tenantId, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rows = await connection.QueryAsync<(string PolicyKey, string EffectiveValue)>(new CommandDefinition("""
            SELECT d.PolicyKey, COALESCE(p.PolicyValue, d.DefaultValue) AS EffectiveValue
            FROM SecurityPolicyDefinitions d
            LEFT JOIN TenantSecurityPolicies p ON p.PolicyKey = d.PolicyKey AND p.TenantId = @TenantId
            WHERE d.IsActive = 1
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        foreach (var row in rows) map[row.PolicyKey] = row.EffectiveValue;

        static bool AsBool(string? v) =>
            string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "1";
        static int? AsInt(string? v) => int.TryParse(v, out var n) ? n : null;

        map.TryGetValue(SecurityPolicyKeys.ComplianceMfaRequired, out var mfa);
        map.TryGetValue(SecurityPolicyKeys.PasswordMaxAgeDays, out var age);
        map.TryGetValue(SecurityPolicyKeys.SessionIdleTimeoutMinutes, out var idle);
        map.TryGetValue(SecurityPolicyKeys.ComplianceGdprLogging, out var gdpr);
        map.TryGetValue(SecurityPolicyKeys.AuditLevel, out var audit);
        map.TryGetValue(SecurityPolicyKeys.ComplianceVatEnabled, out var vat);

        var auditEnabled = !string.Equals(audit, "Disabled", StringComparison.OrdinalIgnoreCase);

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM TenantSecuritySettings WHERE TenantId = @TenantId)
                UPDATE TenantSecuritySettings SET
                    IsMfaRequired = @IsMfaRequired,
                    PasswordExpiryDays = @PasswordExpiryDays,
                    SessionTimeoutMinutes = @SessionTimeoutMinutes,
                    IsGdprEnabled = @IsGdprEnabled,
                    IsAuditLoggingEnabled = @IsAuditLoggingEnabled,
                    IsVatEnabled = @IsVatEnabled
                WHERE TenantId = @TenantId;
            ELSE
                INSERT INTO TenantSecuritySettings (TenantId, IsMfaRequired, PasswordExpiryDays, SessionTimeoutMinutes,
                    IsGdprEnabled, IsAuditLoggingEnabled, IsVatEnabled)
                VALUES (@TenantId, @IsMfaRequired, @PasswordExpiryDays, @SessionTimeoutMinutes,
                    @IsGdprEnabled, @IsAuditLoggingEnabled, @IsVatEnabled);
            """,
            new
            {
                TenantId = tenantId,
                IsMfaRequired = AsBool(mfa),
                PasswordExpiryDays = AsInt(age) ?? 90,
                SessionTimeoutMinutes = AsInt(idle) ?? 30,
                IsGdprEnabled = AsBool(gdpr),
                IsAuditLoggingEnabled = auditEnabled,
                IsVatEnabled = AsBool(vat)
            },
            cancellationToken: cancellationToken));
    }

    private static bool CidrContains(string cidrOrIp, IPAddress address)
    {
        if (string.IsNullOrWhiteSpace(cidrOrIp)) return false;
        if (!cidrOrIp.Contains('/'))
            return IPAddress.TryParse(cidrOrIp, out var single) && single.Equals(address);

        var parts = cidrOrIp.Split('/', 2);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var network) || !int.TryParse(parts[1], out var prefix))
            return false;
        if (network.AddressFamily != address.AddressFamily) return false;

        var networkBytes = network.GetAddressBytes();
        var addressBytes = address.GetAddressBytes();
        if (networkBytes.Length != addressBytes.Length) return false;

        var fullBytes = prefix / 8;
        var remBits = prefix % 8;
        for (var i = 0; i < fullBytes; i++)
            if (networkBytes[i] != addressBytes[i]) return false;
        if (remBits == 0) return true;
        var mask = (byte)(0xFF << (8 - remBits));
        return (networkBytes[fullBytes] & mask) == (addressBytes[fullBytes] & mask);
    }
}
