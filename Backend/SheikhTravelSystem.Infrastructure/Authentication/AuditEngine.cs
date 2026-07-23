using System.Data;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Authentication;

/// <summary>
/// Stage 14 Audit Engine — writes AuditEvents (+ dual-write AuditLogs) and query helpers.
/// Respects Stage 13 audit.level / audit.login_events.
/// </summary>
public class AuditEngine(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    ISecurityEngine securityEngine,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditEngine> logger) : IAuditEngine
{
    private const int MaxPayloadChars = 4000;

    public async Task RecordAsync(AuditEventWrite write, CancellationToken cancellationToken = default)
    {
        var eventKey = string.IsNullOrWhiteSpace(write.EventKey)
            ? AuditEventKeys.GenericAction
            : write.EventKey.Trim();

        var tenantId = write.TenantId ?? TryTenantId();
        if (tenantId is not int tid)
        {
            logger.LogDebug(
                "Skipping audit write {EventKey}: no tenant context (refusing TenantId fallback)",
                eventKey);
            return;
        }

        var userId = write.UserId ?? currentUser.UserId;
        var http = httpContextAccessor.HttpContext;
        var ip = write.IpAddress ?? http?.Connection.RemoteIpAddress?.ToString();
        var ua = Truncate(write.UserAgent ?? http?.Request.Headers.UserAgent.ToString(), 256);
        var correlation = write.CorrelationId
            ?? http?.TraceIdentifier
            ?? http?.Request.Headers["X-Correlation-Id"].FirstOrDefault();

        try
        {
            var map = await securityEngine.GetEffectiveMapAsync(tid, cancellationToken);
            var level = securityEngine.GetString(map, SecurityPolicyKeys.AuditLevel, "Always");

            if (AuditEngineRules.ShouldSkipAuthEvent(
                    eventKey,
                    securityEngine.GetBool(map, SecurityPolicyKeys.AuditLoginEvents, true)))
                return;

            var isError = !write.Success
                || eventKey.Contains("fail", StringComparison.OrdinalIgnoreCase)
                || eventKey.Contains("error", StringComparison.OrdinalIgnoreCase);
            var isCritical = eventKey.Contains("delete", StringComparison.OrdinalIgnoreCase)
                || eventKey.Contains("lockout", StringComparison.OrdinalIgnoreCase)
                || eventKey.Contains("security", StringComparison.OrdinalIgnoreCase)
                || string.Equals(write.EntityType, "SecurityPolicy", StringComparison.OrdinalIgnoreCase);

            if (!securityEngine.ShouldWriteAudit(level, isError, isCritical))
                return;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Audit policy gate skipped for tenant {TenantId}", tid);
        }

        var oldValues = Truncate(write.OldValues, MaxPayloadChars);
        var newValues = Truncate(write.NewValues, MaxPayloadChars);
        var action = Truncate(write.Action, 100);
        var entityType = Truncate(write.EntityType, 100);
        var message = Truncate(write.Message, 500);

        using var connection = dbFactory.CreateConnection();
        try
        {
            await EnsureEventKeyAsync(connection, eventKey, cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO AuditEvents (
                    TenantId, UserId, EventKey, EntityType, EntityId, Action,
                    OldValues, NewValues, IpAddress, UserAgent, CorrelationId,
                    Success, Message, CreatedOn)
                VALUES (
                    @TenantId, @UserId, @EventKey, @EntityType, @EntityId, @Action,
                    @OldValues, @NewValues, @IpAddress, @UserAgent, @CorrelationId,
                    @Success, @Message, SYSUTCDATETIME());
                """,
                new
                {
                    TenantId = tid,
                    UserId = userId,
                    EventKey = eventKey,
                    EntityType = entityType,
                    write.EntityId,
                    Action = action,
                    OldValues = oldValues,
                    NewValues = newValues,
                    IpAddress = Truncate(ip, 64),
                    UserAgent = ua,
                    CorrelationId = Truncate(correlation, 64),
                    write.Success,
                    Message = message
                },
                cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write AuditEvents for {EventKey}", eventKey);
        }

        // Dual-write slim legacy AuditLogs row.
        try
        {
            var legacyAction = action ?? eventKey;
            var legacyEntity = entityType ?? "AuditEvent";
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AuditLogs')
                INSERT INTO AuditLogs (TenantId, [Action], EntityName, EntityId, UserId, IpAddress, CreatedAt, IsDeleted)
                VALUES (@TenantId, @Action, @EntityName, @EntityId, @UserId, @IpAddress, SYSUTCDATETIME(), 0);
                """,
                new
                {
                    TenantId = tid,
                    Action = Truncate(legacyAction, 200) ?? "Action",
                    EntityName = Truncate(legacyEntity, 100) ?? "Entity",
                    write.EntityId,
                    UserId = userId,
                    IpAddress = Truncate(ip, 64)
                },
                cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Legacy AuditLogs dual-write skipped");
        }
    }

    public async Task<IReadOnlyList<AuditEventDefinitionDto>> GetCatalogAsync(
        bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT EventKey, DisplayName, Category, Severity, Description,
                   SortOrder, Visible, IsActive, IsSystem
            FROM AuditEventDefinitions
            """;
        if (activeOnly)
            sql += " WHERE IsActive = 1 AND Visible = 1";
        sql += " ORDER BY SortOrder, Category, DisplayName";
        var rows = await connection.QueryAsync<AuditEventDefinitionDto>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<(IReadOnlyList<AuditEventListItemDto> Items, int Total)> SearchAsync(
        AuditEventSearchFilter filter, CancellationToken cancellationToken = default)
    {
        if (filter.TenantId is not int tenantId)
            throw new ArgumentException("TenantId is required for audit search.", nameof(filter));

        using var connection = dbFactory.CreateConnection();
        var page = Math.Max(1, filter.Page);
        var pageSize = AuditEngineRules.ClampPageSize(filter.PageSize, filter.ForExport);
        var offset = (page - 1) * pageSize;

        var where = new List<string> { "e.TenantId = @TenantId" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Offset", offset);
        p.Add("PageSize", pageSize);

        if (filter.UserId is int uid)
        {
            where.Add("e.UserId = @UserId");
            p.Add("UserId", uid);
        }
        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            where.Add("d.Category = @Category");
            p.Add("Category", filter.Category);
        }
        if (!string.IsNullOrWhiteSpace(filter.EventKey))
        {
            where.Add("e.EventKey = @EventKey");
            p.Add("EventKey", filter.EventKey);
        }
        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            where.Add("e.EntityType = @EntityType");
            p.Add("EntityType", filter.EntityType);
        }
        if (filter.EntityId is int eid)
        {
            where.Add("e.EntityId = @EntityId");
            p.Add("EntityId", eid);
        }
        if (!string.IsNullOrWhiteSpace(filter.Severity))
        {
            where.Add("d.Severity = @Severity");
            p.Add("Severity", filter.Severity);
        }
        if (filter.Success is bool success)
        {
            where.Add("e.Success = @Success");
            p.Add("Success", success);
        }
        if (filter.FromDate is DateTime from)
        {
            where.Add("e.CreatedOn >= @FromDate");
            p.Add("FromDate", from);
        }
        if (filter.ToDate is DateTime to)
        {
            where.Add("e.CreatedOn <= @ToDate");
            p.Add("ToDate", to);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            where.Add("(e.Action LIKE @Search OR e.EntityType LIKE @Search OR e.Message LIKE @Search OR e.EventKey LIKE @Search)");
            p.Add("Search", "%" + filter.Search.Trim() + "%");
        }

        var whereSql = string.Join(" AND ", where);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT COUNT(1)
            FROM AuditEvents e
            INNER JOIN AuditEventDefinitions d ON d.EventKey = e.EventKey
            WHERE {whereSql}
            """, p, cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<AuditEventListItemDto>(new CommandDefinition($"""
            SELECT e.Id, e.TenantId, t.Name AS CompanyName, e.UserId, u.FullName AS UserName,
                   e.EventKey, d.DisplayName, d.Category, d.Severity,
                   e.EntityType, e.EntityId, e.Action, e.Success, e.Message, e.IpAddress, e.CreatedOn
            FROM AuditEvents e
            INNER JOIN AuditEventDefinitions d ON d.EventKey = e.EventKey
            LEFT JOIN Tenants t ON t.Id = e.TenantId
            LEFT JOIN Users u ON u.Id = e.UserId
            WHERE {whereSql}
            ORDER BY e.CreatedOn DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken))).ToList();

        return (items, total);
    }

    public async Task<AuditEventDetailDto?> GetByIdAsync(
        int id, int? tenantId = null, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT e.Id, e.TenantId, t.Name AS CompanyName, e.UserId, u.FullName AS UserName,
                   e.EventKey, d.DisplayName, d.Category, d.Severity,
                   e.EntityType, e.EntityId, e.Action, e.OldValues, e.NewValues,
                   e.IpAddress, e.UserAgent, e.CorrelationId, e.Success, e.Message, e.CreatedOn
            FROM AuditEvents e
            INNER JOIN AuditEventDefinitions d ON d.EventKey = e.EventKey
            LEFT JOIN Tenants t ON t.Id = e.TenantId
            LEFT JOIN Users u ON u.Id = e.UserId
            WHERE e.Id = @Id
            """;
        if (tenantId is int tid)
            sql += " AND e.TenantId = @TenantId";

        return await connection.QuerySingleOrDefaultAsync<AuditEventDetailDto>(
            new CommandDefinition(sql, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AuditEventListItemDto>> GetRecentAsync(
        int tenantId, int? userId = null, int take = 20, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT TOP (@Take) e.Id, e.TenantId, t.Name AS CompanyName, e.UserId, u.FullName AS UserName,
                   e.EventKey, d.DisplayName, d.Category, d.Severity,
                   e.EntityType, e.EntityId, e.Action, e.Success, e.Message, e.IpAddress, e.CreatedOn
            FROM AuditEvents e
            INNER JOIN AuditEventDefinitions d ON d.EventKey = e.EventKey
            LEFT JOIN Tenants t ON t.Id = e.TenantId
            LEFT JOIN Users u ON u.Id = e.UserId
            WHERE e.TenantId = @TenantId
            """;
        if (userId is int)
            sql += " AND e.UserId = @UserId";
        sql += " ORDER BY e.CreatedOn DESC";

        var rows = await connection.QueryAsync<AuditEventListItemDto>(
            new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, Take = take },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<AuditRetentionDto> GetRetentionAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        var map = await securityEngine.GetEffectiveMapAsync(tenantId, cancellationToken);
        var level = securityEngine.GetString(map, SecurityPolicyKeys.AuditLevel, "Always");
        var days = securityEngine.GetInt(map, SecurityPolicyKeys.ComplianceDataRetentionDays, 90);
        return new AuditRetentionDto(days, !string.Equals(level, "Disabled", StringComparison.OrdinalIgnoreCase), level);
    }

    public async Task<AuditCompanySummaryDto> GetSafeSummaryAsync(
        int tenantId, CancellationToken cancellationToken = default)
    {
        var retention = await GetRetentionAsync(tenantId, cancellationToken);
        return new AuditCompanySummaryDto(retention.AuditEnabled, retention.RetentionDays);
    }

    private static async Task EnsureEventKeyAsync(
        IDbConnection connection, string eventKey, CancellationToken cancellationToken)
    {
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM AuditEventDefinitions WHERE EventKey = @EventKey) THEN 1 ELSE 0 END",
            new { EventKey = eventKey },
            cancellationToken: cancellationToken));
        if (exists == 1) return;

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AuditEventDefinitions (EventKey, DisplayName, Category, Severity, Description, SortOrder, Visible, IsActive, IsSystem)
            VALUES (@EventKey, @DisplayName, N'Administration', N'Information', NULL, 999, 1, 1, 0);
            """,
            new { EventKey = eventKey, DisplayName = eventKey },
            cancellationToken: cancellationToken));
    }

    private int? TryTenantId()
    {
        try { return tenantContext.TenantId; }
        catch { return null; }
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}
