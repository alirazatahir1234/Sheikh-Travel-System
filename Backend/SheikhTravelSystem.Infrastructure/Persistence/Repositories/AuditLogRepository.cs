using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.AuditLogs.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository(IDbConnectionFactory dbFactory) : IAuditLogRepository
{
    public async Task<PagedResult<AuditLogDto>> GetPagedAsync(
        int page,
        int pageSize,
        int tenantId,
        string? action,
        string? entityName,
        int? userId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string> { "a.IsDeleted = 0", "a.TenantId = @TenantId" };
        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId);
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        if (!string.IsNullOrWhiteSpace(action))
        {
            whereConditions.Add("a.Action = @Action");
            parameters.Add("Action", action);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            whereConditions.Add("a.EntityName = @EntityName");
            parameters.Add("EntityName", entityName);
        }

        if (userId.HasValue)
        {
            whereConditions.Add("a.UserId = @UserId");
            parameters.Add("UserId", userId.Value);
        }

        if (fromDate.HasValue)
        {
            whereConditions.Add("a.CreatedAt >= @FromDate");
            parameters.Add("FromDate", fromDate.Value);
        }

        if (toDate.HasValue)
        {
            whereConditions.Add("a.CreatedAt <= @ToDate");
            parameters.Add("ToDate", toDate.Value.Date.AddDays(1));
        }

        var whereClause = string.Join(" AND ", whereConditions);

        var logs = await connection.QueryAsync<AuditLogDto>(
            new CommandDefinition(
                $@"SELECT a.Id, a.Action, a.EntityName, a.EntityId,
                          a.OldValues, a.NewValues, a.UserId,
                          u.FullName AS UserName, a.IpAddress, a.CreatedAt
                   FROM AuditLogs a
                   LEFT JOIN Users u ON a.UserId = u.Id
                   WHERE {whereClause}
                   ORDER BY a.CreatedAt DESC
                   OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                parameters,
                cancellationToken: cancellationToken));

        var countResult = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $"SELECT COUNT(*) FROM AuditLogs a WHERE {whereClause}",
                parameters,
                cancellationToken: cancellationToken));

        return new PagedResult<AuditLogDto>
        {
            Items = logs.ToList(),
            TotalCount = countResult,
            Page = page,
            PageSize = pageSize
        };
    }
}
