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
        var where = activeOnly ? "WHERE IsActive = 1 AND Visible = 1" : "";
        var rows = await connection.QueryAsync<SecurityPolicyDefinitionDto>(new CommandDefinition($"""
            SELECT PolicyKey, DisplayName, Category, Description, DefaultValue, ValueType,
                   SortOrder, Visible, IsActive, IsSystem
            FROM SecurityPolicyDefinitions
            {where}
            ORDER BY Category, SortOrder, DisplayName
            """, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<SecurityPolicyValueDto>> GetCompanyPoliciesAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<(
            string PolicyKey, string DisplayName, string Category, string? Description,
            string ValueType, string DefaultValue, string? TenantValue,
            DateTime? UpdatedDate, int? UpdatedBy)>(new CommandDefinition("""
            SELECT d.PolicyKey, d.DisplayName, d.Category, d.Description, d.ValueType, d.DefaultValue,
                   p.PolicyValue AS TenantValue, p.UpdatedDate, p.UpdatedBy
            FROM SecurityPolicyDefinitions d
            LEFT JOIN TenantSecurityPolicies p
                ON p.PolicyKey = d.PolicyKey AND p.TenantId = @TenantId
            WHERE d.IsActive = 1 AND d.Visible = 1
            ORDER BY d.Category, d.SortOrder, d.DisplayName
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        return rows.Select(r =>
        {
            var effective = string.IsNullOrWhiteSpace(r.TenantValue) ? r.DefaultValue : r.TenantValue!;
            return new SecurityPolicyValueDto(
                r.PolicyKey, r.DisplayName, r.Category, r.Description, r.ValueType,
                r.DefaultValue, effective, r.TenantValue, r.UpdatedDate, r.UpdatedBy,
                !string.IsNullOrWhiteSpace(r.TenantValue));
        }).ToList();
    }

    public async Task SetCompanyPoliciesAsync(
        int tenantId,
        IReadOnlyDictionary<string, string> values,
        int? updatedBy,
        CancellationToken cancellationToken = default)
    {
        if (values.Count == 0) return;

        using var connection = dbFactory.CreateConnection();
        var validKeys = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT PolicyKey FROM SecurityPolicyDefinitions WHERE IsActive = 1",
            cancellationToken: cancellationToken))).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in values)
        {
            if (!validKeys.Contains(key)) continue;
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM TenantSecurityPolicies WHERE TenantId = @TenantId AND PolicyKey = @PolicyKey)
                    UPDATE TenantSecurityPolicies
                    SET PolicyValue = @PolicyValue, UpdatedBy = @UpdatedBy, UpdatedDate = SYSUTCDATETIME()
                    WHERE TenantId = @TenantId AND PolicyKey = @PolicyKey;
                ELSE
                    INSERT INTO TenantSecurityPolicies (TenantId, PolicyKey, PolicyValue, UpdatedBy, UpdatedDate)
                    VALUES (@TenantId, @PolicyKey, @PolicyValue, @UpdatedBy, SYSUTCDATETIME());
                """,
                new { TenantId = tenantId, PolicyKey = key, PolicyValue = value ?? "", UpdatedBy = updatedBy },
                cancellationToken: cancellationToken));
        }

        await DualWriteLegacyAsync(connection, tenantId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetEffectiveMapAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        var policies = await GetCompanyPoliciesAsync(tenantId, cancellationToken);
        return policies.ToDictionary(p => p.PolicyKey, p => p.EffectiveValue, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<SecurityCompanySummaryDto> GetSafeSummaryAsync(
        int tenantId,
        DateTime? passwordChangedAt = null,
        CancellationToken cancellationToken = default)
    {
        var map = await GetEffectiveMapAsync(tenantId, cancellationToken);
        var maxAge = GetInt(map, SecurityPolicyKeys.PasswordMaxAgeDays, 90);
        var idle = GetInt(map, SecurityPolicyKeys.SessionIdleTimeoutMinutes, 30);
        var absolute = GetInt(map, SecurityPolicyKeys.SessionAbsoluteTimeoutMinutes, 0);
        var maxAttempts = GetInt(map, SecurityPolicyKeys.LockoutMaxAttempts, 5);
        var auditLevel = GetString(map, SecurityPolicyKeys.AuditLevel, "Always");
        var mfa = GetBool(map, SecurityPolicyKeys.ComplianceMfaRequired, false);

        int? daysRemaining = null;
        var expired = false;
        if (maxAge > 0 && passwordChangedAt.HasValue)
        {
            var age = (DateTime.UtcNow.Date - passwordChangedAt.Value.Date).TotalDays;
            daysRemaining = Math.Max(0, maxAge - (int)age);
            expired = age >= maxAge;
        }

        return new SecurityCompanySummaryDto(
            maxAge > 0 ? maxAge : null,
            idle > 0 ? idle : null,
            absolute > 0 ? absolute : null,
            auditLevel,
            maxAttempts > 0,
            mfa,
            expired,
            daysRemaining);
    }

    public int GetInt(IReadOnlyDictionary<string, string> map, string key, int fallback)
    {
        if (map.TryGetValue(key, out var raw) && int.TryParse(raw, out var n)) return n;
        return fallback;
    }

    public bool GetBool(IReadOnlyDictionary<string, string> map, string key, bool fallback)
    {
        if (!map.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw)) return fallback;
        if (bool.TryParse(raw, out var b)) return b;
        if (raw is "1" or "yes" or "Yes" or "YES") return true;
        if (raw is "0" or "no" or "No" or "NO") return false;
        return fallback;
    }

    public string GetString(IReadOnlyDictionary<string, string> map, string key, string fallback)
        => map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw.Trim() : fallback;

    public bool IsPasswordExpired(DateTime? passwordChangedAt, int maxAgeDays)
    {
        if (maxAgeDays <= 0 || passwordChangedAt is null) return false;
        return (DateTime.UtcNow.Date - passwordChangedAt.Value.Date).TotalDays >= maxAgeDays;
    }

    public bool IsClientIpAllowed(string? clientIp, bool restrictEnabled, string? allowedCidrs)
    {
        if (!restrictEnabled) return true;
        if (string.IsNullOrWhiteSpace(allowedCidrs)) return true;
        if (string.IsNullOrWhiteSpace(clientIp)) return true; // soft: missing IP does not hard-block

        if (!IPAddress.TryParse(clientIp, out var ip)) return true;

        foreach (var line in allowedCidrs.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (CidrContains(line, ip)) return true;
        }

        return false;
    }

    public bool ShouldWriteAudit(string auditLevel, bool isError = false, bool isCritical = false)
    {
        return (auditLevel ?? "Always").Trim() switch
        {
            "Disabled" => false,
            "Critical" => isCritical,
            "Errors" => isError || isCritical,
            _ => true // Always
        };
    }

    private static async Task DualWriteLegacyAsync(
        System.Data.IDbConnection connection, int tenantId, CancellationToken cancellationToken)
    {
        var map = (await connection.QueryAsync<(string PolicyKey, string EffectiveValue)>(new CommandDefinition("""
            SELECT d.PolicyKey,
                   COALESCE(NULLIF(LTRIM(RTRIM(p.PolicyValue)), ''), d.DefaultValue) AS EffectiveValue
            FROM SecurityPolicyDefinitions d
            LEFT JOIN TenantSecurityPolicies p ON p.PolicyKey = d.PolicyKey AND p.TenantId = @TenantId
            WHERE d.PolicyKey IN (
                @MaxAge, @Idle, @Gdpr, @Mfa, @Vat, @Audit)
            """,
            new
            {
                TenantId = tenantId,
                MaxAge = SecurityPolicyKeys.PasswordMaxAgeDays,
                Idle = SecurityPolicyKeys.SessionIdleTimeoutMinutes,
                Gdpr = SecurityPolicyKeys.ComplianceGdprLogging,
                Mfa = SecurityPolicyKeys.ComplianceMfaRequired,
                Vat = SecurityPolicyKeys.ComplianceVatEnabled,
                Audit = SecurityPolicyKeys.AuditLevel
            },
            cancellationToken: cancellationToken)))
            .ToDictionary(x => x.PolicyKey, x => x.EffectiveValue, StringComparer.OrdinalIgnoreCase);

        static bool AsBool(string? v) =>
            bool.TryParse(v, out var b) ? b : v is "1" or "true" or "True";

        static int? AsInt(string? v) => int.TryParse(v, out var n) ? n : null;

        var auditLevel = map.GetValueOrDefault(SecurityPolicyKeys.AuditLevel, "Always");
        var auditEnabled = !string.Equals(auditLevel, "Disabled", StringComparison.OrdinalIgnoreCase);

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
                IsMfaRequired = AsBool(map.GetValueOrDefault(SecurityPolicyKeys.ComplianceMfaRequired)),
                PasswordExpiryDays = AsInt(map.GetValueOrDefault(SecurityPolicyKeys.PasswordMaxAgeDays)) ?? 90,
                SessionTimeoutMinutes = AsInt(map.GetValueOrDefault(SecurityPolicyKeys.SessionIdleTimeoutMinutes)) ?? 30,
                IsGdprEnabled = AsBool(map.GetValueOrDefault(SecurityPolicyKeys.ComplianceGdprLogging, "true")),
                IsAuditLoggingEnabled = auditEnabled,
                IsVatEnabled = AsBool(map.GetValueOrDefault(SecurityPolicyKeys.ComplianceVatEnabled))
            },
            cancellationToken: cancellationToken));
    }

    private static bool CidrContains(string cidrOrIp, IPAddress address)
    {
        if (IPAddress.TryParse(cidrOrIp, out var single))
            return single.Equals(address);

        var parts = cidrOrIp.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var network) || !int.TryParse(parts[1], out var prefix))
            return false;

        if (network.AddressFamily != address.AddressFamily) return false;

        var networkBytes = network.GetAddressBytes();
        var addressBytes = address.GetAddressBytes();
        if (networkBytes.Length != addressBytes.Length) return false;

        var fullBytes = prefix / 8;
        var remBits = prefix % 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (networkBytes[i] != addressBytes[i]) return false;
        }

        if (remBits == 0) return true;
        var mask = (byte)(0xFF << (8 - remBits));
        return (networkBytes[fullBytes] & mask) == (addressBytes[fullBytes] & mask);
    }
}
