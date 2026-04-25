using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.AuditLogs.DTOs;

namespace SheikhTravelSystem.Application.Features.AuditLogs.Queries;

public record GetAuditLogsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Action = null,
    string? EntityName = null,
    int? UserId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
) : IRequest<ApiResponse<PagedResult<AuditLogDto>>>;

public class GetAuditLogsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetAuditLogsQuery, ApiResponse<PagedResult<AuditLogDto>>>
{
    public async Task<ApiResponse<PagedResult<AuditLogDto>>> Handle(
        GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var whereConditions = new List<string> { "a.IsDeleted = 0" };
        var parameters = new DynamicParameters();
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", request.PageSize);

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            whereConditions.Add("a.Action = @Action");
            parameters.Add("Action", request.Action);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityName))
        {
            whereConditions.Add("a.EntityName = @EntityName");
            parameters.Add("EntityName", request.EntityName);
        }

        if (request.UserId.HasValue)
        {
            whereConditions.Add("a.UserId = @UserId");
            parameters.Add("UserId", request.UserId.Value);
        }

        if (request.FromDate.HasValue)
        {
            whereConditions.Add("a.CreatedAt >= @FromDate");
            parameters.Add("FromDate", request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            whereConditions.Add("a.CreatedAt <= @ToDate");
            parameters.Add("ToDate", request.ToDate.Value.Date.AddDays(1));
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

        return ApiResponse<PagedResult<AuditLogDto>>.SuccessResponse(
            new PagedResult<AuditLogDto>
            {
                Items = logs.ToList(),
                TotalCount = countResult,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }
}
