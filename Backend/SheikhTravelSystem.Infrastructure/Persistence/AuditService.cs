using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence;

/// <summary>
/// Writes audit log entries to the AuditLogs table, capturing the current
/// authenticated user and client IP address automatically.
/// Soft-respects Stage 13 audit.level policy.
/// </summary>
public class AuditService(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext,
    ISecurityEngine securityEngine,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditService> logger) : IAuditService
{
    public async Task LogAsync(string action, string entityName, int? entityId, CancellationToken cancellationToken = default)
    {
        var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        int? tenantId = null;
        try { tenantId = tenantContext.TenantId; } catch { /* unauthenticated pipeline */ }

        var resolvedTenantId = tenantId ?? 1;
        try
        {
            var map = await securityEngine.GetEffectiveMapAsync(resolvedTenantId, cancellationToken);
            var level = securityEngine.GetString(map, SecurityPolicyKeys.AuditLevel, "Always");
            var isError = action.Contains("Fail", StringComparison.OrdinalIgnoreCase)
                || action.Contains("Error", StringComparison.OrdinalIgnoreCase)
                || action.Contains("Deny", StringComparison.OrdinalIgnoreCase);
            var isCritical = action.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                || action.Contains("Reset", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entityName, "SecurityPolicy", StringComparison.OrdinalIgnoreCase);
            if (!securityEngine.ShouldWriteAudit(level, isError, isCritical))
                return;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Security audit gate skipped for tenant {TenantId}", resolvedTenantId);
        }

        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO AuditLogs (TenantId, [Action], EntityName, EntityId, UserId, IpAddress, CreatedAt, IsDeleted)
                  VALUES (@TenantId, @Action, @EntityName, @EntityId, @UserId, @IpAddress, @CreatedAt, 0)",
                new
                {
                    TenantId = resolvedTenantId,
                    Action = action,
                    EntityName = entityName,
                    EntityId = entityId,
                    UserId = currentUserService.UserId,
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));
    }
}
