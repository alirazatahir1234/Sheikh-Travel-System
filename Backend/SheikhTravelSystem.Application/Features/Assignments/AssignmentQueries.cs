using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Assignments;

public record ListAssignmentsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Status = null,
    string? AssignmentType = null,
    int? VehicleId = null,
    int? DriverId = null,
    int? BranchId = null,
    int? DepartmentId = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null)
    : IRequest<ApiResponse<PagedResult<AssignmentListItemDto>>>;

public record GetAssignmentStatsQuery : IRequest<ApiResponse<AssignmentStatsDto>>;

public record GetAssignmentChangelogQuery(int AssignmentId) : IRequest<ApiResponse<IReadOnlyList<AssignmentChangelogDto>>>;

public record ValidateAssignmentQuery(ValidateAssignmentRequest Body) : IRequest<ApiResponse<AssignmentValidationResultDto>>;

public record GetAssignmentCalendarQuery(
    DateTime From,
    DateTime To,
    string View = "vehicles",
    int? BranchId = null)
    : IRequest<ApiResponse<IReadOnlyList<AssignmentCalendarItemDto>>>;

public record GetAssignmentUtilizationReportQuery : IRequest<ApiResponse<AssignmentUtilizationReportDto>>;

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
            {AssignmentSql.ListFrom}
            WHERE {filter.Condition}
            """;

        var dataSql = $"""
            SELECT {AssignmentSql.ListSelect}
            {AssignmentSql.ListFrom}
            WHERE {filter.Condition}
            ORDER BY a.StartAt DESC
            OFFSET {offset} ROWS FETCH NEXT {request.PageSize} ROWS ONLY
            """;

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, filter.Params, cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<AssignmentListItemDto>(
            new CommandDefinition(dataSql, filter.Params, cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<AssignmentListItemDto>>.SuccessResponse(new PagedResult<AssignmentListItemDto>
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
            var status = q.Status.Trim();
            if (status.Equals("Overdue", StringComparison.OrdinalIgnoreCase))
                clauses.Add("a.Status = N'Active' AND a.EndAt IS NOT NULL AND a.EndAt < GETUTCDATE()");
            else if (status.Equals("Assigned", StringComparison.OrdinalIgnoreCase))
                clauses.Add("a.Status = N'Active' AND a.StartAt > GETUTCDATE()");
            else
            {
                clauses.Add("a.Status = @Status");
                p["Status"] = status;
            }
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

        if (q.BranchId.HasValue)
        {
            clauses.Add("(v.BranchId = @BranchId OR d.BranchId = @BranchId)");
            p["BranchId"] = q.BranchId.Value;
        }

        if (q.DepartmentId.HasValue)
        {
            clauses.Add("(v.DepartmentId = @DepartmentId OR d.DepartmentId = @DepartmentId)");
            p["DepartmentId"] = q.DepartmentId.Value;
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
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled')) AS ActiveAssignments,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = N'Completed') AS CompletedAssignments,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = N'Cancelled') AS CancelledAssignments,
                (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5
                    AND Id NOT IN (SELECT VehicleId FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled'))) AS UnassignedVehicles,
                (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = 1
                    AND Id NOT IN (SELECT VehicleId FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled'))) AS AvailableVehicles,
                (SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND IsActive = 1 AND Status = 1
                    AND Id NOT IN (SELECT DriverId FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled') AND DriverId IS NOT NULL)) AS AvailableDrivers,
                (SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND IsActive = 1
                    AND LicenseExpiryDate IS NOT NULL AND LicenseExpiryDate <= DATEADD(DAY, 30, GETUTCDATE())) AS ExpiringLicenses,
                (SELECT COUNT(*) FROM Bookings WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = @Started) AS OngoingTrips,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId
                    AND (Status = N'Scheduled' OR (Status = N'Active' AND StartAt BETWEEN GETUTCDATE() AND DATEADD(hour, 24, GETUTCDATE())))) AS UpcomingAssignments,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId
                    AND Status = N'Active' AND EndAt IS NOT NULL AND EndAt < GETUTCDATE()) AS OverdueReturns,
                (SELECT COUNT(*) FROM (
                    SELECT v.Id FROM Vehicles v WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId
                      AND v.InsuranceExpiryDate IS NOT NULL AND v.InsuranceExpiryDate < CAST(GETUTCDATE() AS DATE)
                    UNION
                    SELECT d.Id FROM Drivers d WHERE d.IsDeleted = 0 AND d.TenantId = @TenantId
                      AND d.LicenseExpiryDate < CAST(GETUTCDATE() AS DATE)
                ) x) AS ExpiredDocuments,
                (SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = @OnLeave) AS DriversOnLeave,
                (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = @Maintenance) AS VehiclesUnderMaintenance,
                CAST(CASE WHEN (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5) = 0 THEN 0
                    ELSE (SELECT COUNT(DISTINCT VehicleId) * 100.0 / NULLIF((SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5), 0)
                          FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled'))
                END AS DECIMAL(5,2)) AS AssignmentUtilizationPct
            """, new
        {
            TenantId = tenantId,
            Started = (int)BookingStatus.Started,
            OnLeave = (int)DriverStatus.OnLeave,
            Maintenance = (int)VehicleStatus.Maintenance
        }, cancellationToken: cancellationToken));

        return ApiResponse<AssignmentStatsDto>.SuccessResponse(dto);
    }
}

public class ValidateAssignmentQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ValidateAssignmentQuery, ApiResponse<AssignmentValidationResultDto>>
{
    public async Task<ApiResponse<AssignmentValidationResultDto>> Handle(ValidateAssignmentQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var result = await AssignmentValidation.ValidateAsync(connection, tenantId, request.Body, cancellationToken);
        return ApiResponse<AssignmentValidationResultDto>.SuccessResponse(result);
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

public class GetAssignmentCalendarQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetAssignmentCalendarQuery, ApiResponse<IReadOnlyList<AssignmentCalendarItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AssignmentCalendarItemDto>>> Handle(
        GetAssignmentCalendarQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var where = "a.IsDeleted = 0 AND v.TenantId = @TenantId AND a.StartAt <= @To AND (a.EndAt IS NULL OR a.EndAt >= @From)";
        if (request.BranchId.HasValue)
            where += " AND (v.BranchId = @BranchId OR d.BranchId = @BranchId)";

        var rows = await connection.QueryAsync<AssignmentCalendarItemDto>(new CommandDefinition($"""
            SELECT a.Id,
                ISNULL(a.AssignmentNo, CONCAT(N'ASN-', RIGHT(CONCAT(N'000000', CAST(a.Id AS NVARCHAR)), 6))) AS AssignmentNo,
                a.VehicleId, v.Name AS VehicleName, a.DriverId, d.FullName AS DriverName,
                a.Status, a.StartAt, a.EndAt, a.AssignmentType
            FROM AssignmentHistory a
            INNER JOIN Vehicles v ON a.VehicleId = v.Id
            LEFT JOIN Drivers d ON a.DriverId = d.Id
            WHERE {where}
            ORDER BY a.StartAt
            """, new { TenantId = tenantId, request.From, request.To, request.BranchId }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<AssignmentCalendarItemDto>>.SuccessResponse(rows.ToList());
    }
}

public class GetAssignmentUtilizationReportQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetAssignmentUtilizationReportQuery, ApiResponse<AssignmentUtilizationReportDto>>
{
    public async Task<ApiResponse<AssignmentUtilizationReportDto>> Handle(
        GetAssignmentUtilizationReportQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var dto = await connection.QuerySingleAsync<AssignmentUtilizationReportDto>(new CommandDefinition("""
            SELECT
                (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5) AS TotalVehicles,
                (SELECT COUNT(DISTINCT VehicleId) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled')) AS AssignedVehicles,
                CAST(CASE WHEN (SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5) = 0 THEN 0
                    ELSE (SELECT COUNT(DISTINCT VehicleId) * 100.0 / NULLIF((SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status <> 5), 0)
                          FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled'))
                END AS DECIMAL(5,2)) AS UtilizationPct,
                (SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND IsActive = 1) AS TotalDrivers,
                (SELECT COUNT(DISTINCT DriverId) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled') AND DriverId IS NOT NULL) AS AssignedDrivers,
                CAST(CASE WHEN (SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND IsActive = 1) = 0 THEN 0
                    ELSE (SELECT COUNT(DISTINCT DriverId) * 100.0 / NULLIF((SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0 AND TenantId = @TenantId AND IsActive = 1), 0)
                          FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled') AND DriverId IS NOT NULL)
                END AS DECIMAL(5,2)) AS DriverUtilizationPct,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status IN (N'Active', N'Scheduled')) AS ActiveAssignments,
                (SELECT COUNT(*) FROM AssignmentHistory WHERE IsDeleted = 0 AND TenantId = @TenantId AND Status = N'Completed'
                    AND EndAt >= DATEADD(month, DATEDIFF(month, 0, GETUTCDATE()), 0)) AS CompletedThisMonth
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<AssignmentUtilizationReportDto>.SuccessResponse(dto);
    }
}
