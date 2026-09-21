using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.AuditLogs.DTOs;

namespace SheikhTravelSystem.Application.Features.AuditLogs.Queries;

public record GetAuditLogsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Action = null,
    string? EntityName = null,
    int? UserId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int? TenantId = null
) : IRequest<ApiResponse<PagedResult<AuditLogDto>>>;

public class GetAuditLogsQueryHandler(
    IAuditLogRepository auditLogRepository,
    IPlatformScope platformScope) : IRequestHandler<GetAuditLogsQuery, ApiResponse<PagedResult<AuditLogDto>>>
{
    public async Task<ApiResponse<PagedResult<AuditLogDto>>> Handle(
        GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantFilter(request.TenantId);
        var result = await auditLogRepository.GetPagedAsync(
            request.Page,
            request.PageSize,
            tenantId,
            request.Action,
            request.EntityName,
            request.UserId,
            request.FromDate,
            request.ToDate,
            cancellationToken);

        return ApiResponse<PagedResult<AuditLogDto>>.SuccessResponse(result);
    }

    private int ResolveTenantFilter(int? requestedTenantId)
    {
        if (requestedTenantId.HasValue)
        {
            platformScope.EnsureTenantAccess(requestedTenantId.Value);
            return requestedTenantId.Value;
        }

        return platformScope.TenantId;
    }
}
