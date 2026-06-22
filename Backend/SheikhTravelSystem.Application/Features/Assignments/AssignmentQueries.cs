using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Assignments;

public record ListAssignmentsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Status = null,
    string? AssignmentType = null,
    int? VehicleId = null,
    int? DriverId = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null)
    : IRequest<ApiResponse<PagedResult<AssignmentListItemDto>>>;

public record GetAssignmentStatsQuery : IRequest<ApiResponse<AssignmentStatsDto>>;

public record GetAssignmentChangelogQuery(int AssignmentId) : IRequest<ApiResponse<IReadOnlyList<AssignmentChangelogDto>>>;

public class ListAssignmentsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ListAssignmentsQuery, ApiResponse<PagedResult<AssignmentListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<AssignmentListItemDto>>> Handle(ListAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var offset = (Math.Max(1, request.Page) - 1) * request.PageSize;

        var filter = BuildFilter(request, tenantId);

        var countSql = $"""
            SELECT COUNT(*)
            FROM AssignmentHistory a
            INNER JOIN Vehicles v ON a.VehicleId = v.Id
            LEFT JOIN Drivers d ON a.DriverId = d.Id
            WHERE {filter.Condition}
            """;

        var dataSql = $"""
            SELECT
                a.Id,
                ISNULL(a.AssignmentNo, CONCAT(N'ASN-', RIGHT(CONCAT(N'000000', CAST(a.Id AS NVARCHAR)), 6))) AS AssignmentNo,
                a.VehicleId,
                v.Name AS VehicleName,
                v.RegistrationNumber AS VehicleRegistration,
                v.VehicleCode,
                a.DriverId,
                d.FullName AS DriverName,
                d.DriverCode,
                a.AssignmentType,
                a.Status,
                a.StartAt,
                a.EndAt,
                a.Reason,
                a.Notes,
                a.CreatedBy,
                a.CreatedAt
            FROM AssignmentHistory a
            INNER JOIN Vehicles v ON a.VehicleId = v.Id
            LEFT JOIN Drivers d ON a.DriverId = d.Id
            WHERE {filter.Condition}
            ORDER BY a.StartAt DESC
            OFFSET {offset} ROWS FETCH NEXT {request.PageSize} ROWS ONLY
            """;

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, filter.Params, cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<AssignmentListItemDto>(
            new CommandDefinition(dataSql, filter.Params, cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<AssignmentListItemDto>>.SuccessResponse(
            new PagedResult<AssignmentListItemDto>
            {
                Items = rows.ToList(),
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }

    private static (string Condition, object Params) BuildFilter(ListAssignmentsQuery q, int tenantId)
    {
        var clauses = new List<string> { "a.IsDeleted = 0", "v.TenantId = @TenantId" };
        var p = new System.Dynamic.ExpandoObject() as IDictionary<string, object?>;
        p["TenantId"] = tenantId;

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            clauses.Add("a.Status = @Status");
            p["Status"] = q.Status.Trim();
        }

        if (!string.IsNullOrWhiteSpace(q.AssignmentType))
        {
            clauses.Add("a.AssignmentType = @AssignmentType");
            p["AssignmentType"] = q.AssignmentType.Trim();
        }

        if (q.VehicleId.HasValue)
        {
            clauses.Add("a.VehicleId = @VehicleId");
            p["VehicleId"] = q.VehicleId.Value;
        }

        if (q.DriverId.HasValue)
        {
            clauses.Add("a.DriverId = @DriverId");
            p["DriverId"] = q.DriverId.Value;
        }

        if (q.DateFrom.HasValue)
        {
            clauses.Add("a.StartAt >= @DateFrom");
            p["DateFrom"] = q.DateFrom.Value;
        }

        if (q.DateTo.HasValue)
        {
            clauses.Add("a.StartAt <= @DateTo");
            p["DateTo"] = q.DateTo.Value;
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            clauses.Add("(v.RegistrationNumber LIKE @Search OR v.Name LIKE @Search OR d.FullName LIKE @Search OR a.AssignmentNo LIKE @Search)");
            p["Search"] = $"%{q.Search.Trim()}%";
        }

        return (string.Join(" AND ", clauses), p);
    }
}

public class GetAssignmentStatsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetAssignmentStatsQuery, ApiResponse<AssignmentStatsDto>>
{
    public async Task<ApiResponse<AssignmentStatsDto>> Handle(GetAssignmentStatsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var dto = await connection.QuerySingleAsync<AssignmentStatsDto>(new CommandDefinition("""
            SELECT
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId) AS TotalAssignments,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = N'Active') AS ActiveAssignments,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = N'Completed') AS CompletedAssignments,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = N'Cancelled') AS CancelledAssignments,
                (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5
                    AND Id NOT IN (SELECT VehicleId FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = N'Active')) AS UnassignedVehicles,
                (SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND IsActive = 1 AND Status = 1
                    AND Id NOT IN (SELECT DriverId FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = N'Active' AND DriverId IS NOT NULL)) AS AvailableDrivers,
                (SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND IsActive = 1
                    AND LicenseExpiryDate IS NOT NULL AND LicenseExpiryDate <= DATEADD(DAY, 30, GETUTCDATE())) AS ExpiringLicenses
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<AssignmentStatsDto>.SuccessResponse(dto);
    }
}

public class GetAssignmentChangelogQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetAssignmentChangelogQuery, ApiResponse<IReadOnlyList<AssignmentChangelogDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AssignmentChangelogDto>>> Handle(GetAssignmentChangelogQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<AssignmentChangelogDto>(new CommandDefinition("""
            SELECT
                c.Id, c.ActionType,
                c.OldVehicleId, ov.Name AS OldVehicleName,
                c.NewVehicleId, nv.Name AS NewVehicleName,
                c.OldDriverId, od.FullName AS OldDriverName,
                c.NewDriverId, nd.FullName AS NewDriverName,
                c.Reason, c.CreatedBy, c.CreatedAt
            FROM FleetAssignmentChangelog c
            INNER JOIN AssignmentHistory a ON c.AssignmentId = a.Id
            INNER JOIN Vehicles av ON a.VehicleId = av.Id
            LEFT JOIN Vehicles ov ON c.OldVehicleId = ov.Id
            LEFT JOIN Vehicles nv ON c.NewVehicleId = nv.Id
            LEFT JOIN Drivers od ON c.OldDriverId = od.Id
            LEFT JOIN Drivers nd ON c.NewDriverId = nd.Id
            WHERE c.AssignmentId = @AssignmentId
              AND av.TenantId = @TenantId
            ORDER BY c.CreatedAt DESC
            """, new { request.AssignmentId, TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<AssignmentChangelogDto>>.SuccessResponse(rows.ToList());
    }
}
