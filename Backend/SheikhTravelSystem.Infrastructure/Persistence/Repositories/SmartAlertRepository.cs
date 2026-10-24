using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.SmartOps;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public class SmartAlertRepository(IDbConnectionFactory dbFactory) : ISmartAlertRepository
{
    private const string SelectCols = """
        Id, TenantId, RuleType, Severity, Message, RelatedEntityType, RelatedEntityId,
        Status, CreatedAt, AcknowledgedAt, AcknowledgedBy, ResolvedAt, ResolvedBy, DeduplicationKey
        """;

    public async Task<(SmartAlertDto Alert, bool Created)> TryInsertAsync(
        CreateSmartAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var existing = await connection.QuerySingleOrDefaultAsync<SmartAlertRow>(new CommandDefinition($"""
            SELECT TOP 1 {SelectCols}
            FROM SmartAlerts
            WHERE TenantId = @TenantId AND DeduplicationKey = @DeduplicationKey
            """, new { request.TenantId, request.DeduplicationKey }, cancellationToken: cancellationToken));

        if (existing is not null)
            return (Map(existing), false);

        try
        {
            var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition("""
                INSERT INTO SmartAlerts
                    (TenantId, RuleType, Severity, Message, RelatedEntityType, RelatedEntityId, Status, DeduplicationKey)
                OUTPUT INSERTED.Id
                VALUES
                    (@TenantId, @RuleType, @Severity, @Message, @RelatedEntityType, @RelatedEntityId, N'New', @DeduplicationKey);
                """, new
            {
                request.TenantId,
                RuleType = SmartAlertRuleTypeNames.ToName(request.RuleType),
                Severity = SmartAlertRuleTypeNames.ToName(request.Severity),
                request.Message,
                request.RelatedEntityType,
                request.RelatedEntityId,
                request.DeduplicationKey
            }, cancellationToken: cancellationToken));

            var created = await connection.QuerySingleAsync<SmartAlertRow>(new CommandDefinition($"""
                SELECT {SelectCols} FROM SmartAlerts WHERE Id = @Id AND TenantId = @TenantId
                """, new { Id = id, request.TenantId }, cancellationToken: cancellationToken));

            return (Map(created), true);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // Concurrent insert raced on unique (TenantId, DeduplicationKey).
            var raced = await connection.QuerySingleAsync<SmartAlertRow>(new CommandDefinition($"""
                SELECT TOP 1 {SelectCols}
                FROM SmartAlerts
                WHERE TenantId = @TenantId AND DeduplicationKey = @DeduplicationKey
                """, new { request.TenantId, request.DeduplicationKey }, cancellationToken: cancellationToken));
            return (Map(raced), false);
        }
    }

    public async Task<IReadOnlyList<SmartAlertDto>> GetActiveAsync(
        int tenantId,
        SmartAlertRuleType? ruleType = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var take = Math.Clamp(limit, 1, 200);
        var ruleName = ruleType.HasValue ? SmartAlertRuleTypeNames.ToName(ruleType.Value) : null;

        var rows = await connection.QueryAsync<SmartAlertRow>(new CommandDefinition($"""
            SELECT TOP (@Take) {SelectCols}
            FROM SmartAlerts
            WHERE TenantId = @TenantId
              AND Status IN (N'New', N'Acknowledged')
              AND (@RuleType IS NULL OR RuleType = @RuleType)
            ORDER BY
                CASE Severity WHEN N'Critical' THEN 0 WHEN N'Warning' THEN 1 ELSE 2 END,
                CreatedAt DESC
            """, new { TenantId = tenantId, Take = take, RuleType = ruleName }, cancellationToken: cancellationToken));

        return rows.Select(Map).ToList();
    }

    public async Task<int> CountNewAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM SmartAlerts
            WHERE TenantId = @TenantId AND Status = N'New'
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<SmartAlertDto?> GetByIdAsync(int tenantId, long id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SmartAlertRow>(new CommandDefinition($"""
            SELECT {SelectCols} FROM SmartAlerts WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }

    public async Task<bool> AcknowledgeAsync(int tenantId, long id, string acknowledgedBy, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var n = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE SmartAlerts
            SET Status = N'Acknowledged',
                AcknowledgedAt = SYSUTCDATETIME(),
                AcknowledgedBy = @By
            WHERE Id = @Id AND TenantId = @TenantId AND Status = N'New'
            """, new { Id = id, TenantId = tenantId, By = acknowledgedBy }, cancellationToken: cancellationToken));
        return n > 0;
    }

    public async Task<bool> ResolveAsync(int tenantId, long id, string resolvedBy, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var n = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE SmartAlerts
            SET Status = N'Resolved',
                ResolvedAt = SYSUTCDATETIME(),
                ResolvedBy = @By
            WHERE Id = @Id AND TenantId = @TenantId AND Status IN (N'New', N'Acknowledged')
            """, new { Id = id, TenantId = tenantId, By = resolvedBy }, cancellationToken: cancellationToken));
        return n > 0;
    }

    public async Task<bool> DismissAsync(int tenantId, long id, string dismissedBy, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var n = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE SmartAlerts
            SET Status = N'Dismissed',
                ResolvedAt = SYSUTCDATETIME(),
                ResolvedBy = @By
            WHERE Id = @Id AND TenantId = @TenantId AND Status IN (N'New', N'Acknowledged')
            """, new { Id = id, TenantId = tenantId, By = dismissedBy }, cancellationToken: cancellationToken));
        return n > 0;
    }

    private static SmartAlertDto Map(SmartAlertRow r) => new(
        r.Id, r.TenantId, r.RuleType, r.Severity, r.Message,
        r.RelatedEntityType, r.RelatedEntityId, r.Status,
        r.CreatedAt, r.AcknowledgedAt, r.AcknowledgedBy, r.ResolvedAt, r.ResolvedBy,
        r.DeduplicationKey);

    private sealed class SmartAlertRow
    {
        public long Id { get; init; }
        public int TenantId { get; init; }
        public string RuleType { get; init; } = "";
        public string Severity { get; init; } = "";
        public string Message { get; init; } = "";
        public string? RelatedEntityType { get; init; }
        public int? RelatedEntityId { get; init; }
        public string Status { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public DateTime? AcknowledgedAt { get; init; }
        public string? AcknowledgedBy { get; init; }
        public DateTime? ResolvedAt { get; init; }
        public string? ResolvedBy { get; init; }
        public string DeduplicationKey { get; init; } = "";
    }
}
