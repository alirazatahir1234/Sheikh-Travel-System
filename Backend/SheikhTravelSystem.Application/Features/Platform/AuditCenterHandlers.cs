using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Platform;

public record GetAuditCatalogQuery(bool ActiveOnly = false)
    : IRequest<ApiResponse<IReadOnlyList<AuditEventDefinitionDto>>>;

public record SearchAuditEventsQuery(
    int Page = 1,
    int PageSize = 20,
    int? TenantId = null,
    int? UserId = null,
    string? Category = null,
    string? EventKey = null,
    string? EntityType = null,
    int? EntityId = null,
    string? Severity = null,
    bool? Success = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Search = null)
    : IRequest<ApiResponse<PagedResult<AuditEventListItemDto>>>;

public record GetAuditEventByIdQuery(int Id, int? TenantId = null)
    : IRequest<ApiResponse<AuditEventDetailDto>>;

public record GetAuditRetentionQuery(int? TenantId = null)
    : IRequest<ApiResponse<AuditRetentionDto>>;

public record GetRecentAuditEventsQuery(int? TenantId = null, int? UserId = null, int Take = 20)
    : IRequest<ApiResponse<IReadOnlyList<AuditEventListItemDto>>>;

public record ExportAuditEventsQuery(
    int? TenantId = null,
    int? UserId = null,
    string? Category = null,
    string? EventKey = null,
    string? EntityType = null,
    string? Severity = null,
    bool? Success = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Search = null,
    string Format = "csv")
    : IRequest<ApiResponse<AuditExportResultDto>>;

public record AuditExportResultDto(string FileName, string ContentType, byte[] Content);

public class GetAuditCatalogQueryHandler(IAuditEngine auditEngine)
    : IRequestHandler<GetAuditCatalogQuery, ApiResponse<IReadOnlyList<AuditEventDefinitionDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AuditEventDefinitionDto>>> Handle(
        GetAuditCatalogQuery request, CancellationToken cancellationToken)
    {
        var catalog = await auditEngine.GetCatalogAsync(request.ActiveOnly, cancellationToken);
        return ApiResponse<IReadOnlyList<AuditEventDefinitionDto>>.SuccessResponse(catalog);
    }
}

public class SearchAuditEventsQueryHandler(
    IAuditEngine auditEngine,
    ITenantContext tenantContext,
    IPlatformScope platformScope)
    : IRequestHandler<SearchAuditEventsQuery, ApiResponse<PagedResult<AuditEventListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<AuditEventListItemDto>>> Handle(
        SearchAuditEventsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenant(request.TenantId, tenantContext, platformScope);
        var (items, total) = await auditEngine.SearchAsync(new AuditEventSearchFilter(
            request.Page, request.PageSize, tenantId, request.UserId, request.Category,
            request.EventKey, request.EntityType, request.EntityId, request.Severity,
            request.Success, request.FromDate, request.ToDate, request.Search), cancellationToken);

        return ApiResponse<PagedResult<AuditEventListItemDto>>.SuccessResponse(
            new PagedResult<AuditEventListItemDto>
            {
                Items = items.ToList(),
                TotalCount = total,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }

    internal static int ResolveTenant(int? requested, ITenantContext tenantContext, IPlatformScope platformScope)
    {
        if (requested is int tid)
        {
            platformScope.EnsureTenantAccess(tid);
            return tid;
        }
        return tenantContext.GetRequiredTenantId();
    }
}

public class GetAuditEventByIdQueryHandler(
    IAuditEngine auditEngine,
    ITenantContext tenantContext,
    IPlatformScope platformScope)
    : IRequestHandler<GetAuditEventByIdQuery, ApiResponse<AuditEventDetailDto>>
{
    public async Task<ApiResponse<AuditEventDetailDto>> Handle(
        GetAuditEventByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId;
        if (tenantId is int tid)
            platformScope.EnsureTenantAccess(tid);
        else
            tenantId = tenantContext.GetRequiredTenantId();

        var detail = await auditEngine.GetByIdAsync(request.Id, tenantId, cancellationToken);
        if (detail is null)
            return ApiResponse<AuditEventDetailDto>.FailResponse("Audit event not found.");
        return ApiResponse<AuditEventDetailDto>.SuccessResponse(detail);
    }
}

public class GetAuditRetentionQueryHandler(
    IAuditEngine auditEngine,
    ITenantContext tenantContext,
    IPlatformScope platformScope)
    : IRequestHandler<GetAuditRetentionQuery, ApiResponse<AuditRetentionDto>>
{
    public async Task<ApiResponse<AuditRetentionDto>> Handle(
        GetAuditRetentionQuery request, CancellationToken cancellationToken)
    {
        var tenantId = SearchAuditEventsQueryHandler.ResolveTenant(
            request.TenantId, tenantContext, platformScope);
        var dto = await auditEngine.GetRetentionAsync(tenantId, cancellationToken);
        return ApiResponse<AuditRetentionDto>.SuccessResponse(dto);
    }
}

public class GetRecentAuditEventsQueryHandler(
    IAuditEngine auditEngine,
    ITenantContext tenantContext,
    IPlatformScope platformScope)
    : IRequestHandler<GetRecentAuditEventsQuery, ApiResponse<IReadOnlyList<AuditEventListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AuditEventListItemDto>>> Handle(
        GetRecentAuditEventsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = SearchAuditEventsQueryHandler.ResolveTenant(
            request.TenantId, tenantContext, platformScope);
        var rows = await auditEngine.GetRecentAsync(
            tenantId, request.UserId, request.Take, cancellationToken);
        return ApiResponse<IReadOnlyList<AuditEventListItemDto>>.SuccessResponse(rows);
    }
}

public class ExportAuditEventsQueryHandler(
    IAuditEngine auditEngine,
    ITenantContext tenantContext,
    IPlatformScope platformScope)
    : IRequestHandler<ExportAuditEventsQuery, ApiResponse<AuditExportResultDto>>
{
    private const int ExportCap = 10_000;

    public async Task<ApiResponse<AuditExportResultDto>> Handle(
        ExportAuditEventsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = SearchAuditEventsQueryHandler.ResolveTenant(
            request.TenantId, tenantContext, platformScope);
        var (items, _) = await auditEngine.SearchAsync(new AuditEventSearchFilter(
            Page: 1,
            PageSize: ExportCap,
            TenantId: tenantId,
            UserId: request.UserId,
            Category: request.Category,
            EventKey: request.EventKey,
            EntityType: request.EntityType,
            Severity: request.Severity,
            Success: request.Success,
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            Search: request.Search,
            ForExport: true), cancellationToken);

        var csv = BuildCsv(items);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return ApiResponse<AuditExportResultDto>.SuccessResponse(new AuditExportResultDto(
            $"audit-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv",
            "text/csv",
            bytes));
    }

    internal static string BuildCsv(IEnumerable<AuditEventListItemDto> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Id,CreatedOn,Company,User,Category,Event,Action,Entity,EntityId,Success,IpAddress,Message");
        foreach (var r in rows)
        {
            sb.Append(r.Id).Append(',')
                .Append(r.CreatedOn.ToString("O")).Append(',')
                .Append(Csv(r.CompanyName)).Append(',')
                .Append(Csv(r.UserName)).Append(',')
                .Append(Csv(r.Category)).Append(',')
                .Append(Csv(r.DisplayName)).Append(',')
                .Append(Csv(r.Action)).Append(',')
                .Append(Csv(r.EntityType)).Append(',')
                .Append(r.EntityId?.ToString() ?? "").Append(',')
                .Append(r.Success ? "true" : "false").Append(',')
                .Append(Csv(r.IpAddress)).Append(',')
                .Append(Csv(r.Message))
                .AppendLine();
        }
        return sb.ToString();

        static string Csv(string? v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            return $"\"{v.Replace("\"", "\"\"")}\"";
        }
    }
}
