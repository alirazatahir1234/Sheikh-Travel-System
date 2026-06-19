using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

public record GetDriversQuery(
    int Page = 1,
    int PageSize = 20,
    string? Q = null,
    DriverStatus? Status = null,
    int? BranchId = null,
    string? LicenseExpiry = null,
    string? VerificationStatus = null) : IRequest<ApiResponse<PagedResult<DriverListItemDto>>>;

public class GetDriversQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDriversQuery, ApiResponse<PagedResult<DriverListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<DriverListItemDto>>> Handle(GetDriversQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;
        var tenantId = tenantContext.GetRequiredTenantId();

        var where = new List<string> { "d.IsDeleted = 0", "d.TenantId = @TenantId" };
        var parameters = new DynamicParameters(new { TenantId = tenantId, Offset = offset, request.PageSize });

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            where.Add(@"(d.FullName LIKE @Search OR d.FirstName LIKE @Search OR d.LastName LIKE @Search OR d.DriverCode LIKE @Search OR d.LicenseNumber LIKE @Search OR d.Phone LIKE @Search)");
            parameters.Add("Search", $"%{request.Q.Trim()}%");
        }

        if (request.Status.HasValue)
        {
            where.Add("d.Status = @Status");
            parameters.Add("Status", (int)request.Status.Value);
        }

        if (request.BranchId.HasValue)
        {
            where.Add("d.BranchId = @BranchId");
            parameters.Add("BranchId", request.BranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.VerificationStatus))
        {
            where.Add("d.VerificationStatus = @VerificationStatus");
            parameters.Add("VerificationStatus", request.VerificationStatus.Trim());
        }

        switch (request.LicenseExpiry?.Trim().ToUpperInvariant())
        {
            case "EXPIRED":
                where.Add("d.LicenseExpiryDate < CAST(GETUTCDATE() AS DATE)");
                break;
            case "EXPIRING":
                where.Add("d.LicenseExpiryDate >= CAST(GETUTCDATE() AS DATE)");
                where.Add("d.LicenseExpiryDate <= DATEADD(day, 30, CAST(GETUTCDATE() AS DATE))");
                break;
            case "VALID":
                where.Add("d.LicenseExpiryDate > DATEADD(day, 30, CAST(GETUTCDATE() AS DATE))");
                break;
        }

        var whereClause = string.Join(" AND ", where);

        var drivers = (await connection.QueryAsync<DriverListItemDto>(
            new CommandDefinition(
                $@"SELECT {DriverSql.ListSelect}
                  {DriverSql.ListFrom}
                  WHERE {whereClause}
                  ORDER BY d.CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                parameters,
                cancellationToken: cancellationToken))).ToList();

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $@"SELECT COUNT(*) {DriverSql.ListFrom} WHERE {whereClause}",
                parameters,
                cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<DriverListItemDto>>.SuccessResponse(new PagedResult<DriverListItemDto>
        {
            Items = drivers,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}

public record GetDriverStatsQuery : IRequest<ApiResponse<DriverStatsDto>>;

public class GetDriverStatsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDriverStatsQuery, ApiResponse<DriverStatsDto>>
{
    public async Task<ApiResponse<DriverStatsDto>> Handle(GetDriverStatsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var stats = await connection.QuerySingleAsync<DriverStatsDto>(
            new CommandDefinition(
                @"SELECT
                    COUNT(*) AS TotalDrivers,
                    SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS Active,
                    SUM(CASE WHEN Status = @OnTrip THEN 1 ELSE 0 END) AS OnTrip,
                    SUM(CASE WHEN Status = @OffDuty THEN 1 ELSE 0 END) AS OffDuty,
                    SUM(CASE WHEN Status = @Available THEN 1 ELSE 0 END) AS Available,
                    SUM(CASE WHEN Status = @OnLeave THEN 1 ELSE 0 END) AS OnLeave,
                    SUM(CASE WHEN Status = @Suspended THEN 1 ELSE 0 END) AS Suspended,
                    SUM(CASE WHEN LicenseExpiryDate >= CAST(GETUTCDATE() AS DATE)
                              AND LicenseExpiryDate <= DATEADD(day, 30, CAST(GETUTCDATE() AS DATE))
                         THEN 1 ELSE 0 END) AS LicensesExpiringSoon,
                    SUM(CASE WHEN LicenseExpiryDate >= CAST(GETUTCDATE() AS DATE)
                              AND LicenseExpiryDate <= DATEADD(day, 7, CAST(GETUTCDATE() AS DATE))
                         THEN 1 ELSE 0 END) AS LicensesExpiringIn7Days,
                    SUM(CASE WHEN LicenseExpiryDate < CAST(GETUTCDATE() AS DATE)
                         THEN 1 ELSE 0 END) AS LicensesExpired,
                    SUM(CASE WHEN VerificationStatus = N'Verified' THEN 1 ELSE 0 END) AS VerifiedDrivers,
                    SUM(CASE WHEN VerificationStatus = N'Pending'  THEN 1 ELSE 0 END) AS PendingVerification,
                    SUM(CASE WHEN EXISTS (
                        SELECT 1 FROM AssignmentHistory ah
                        WHERE ah.DriverId = d.Id AND ah.IsDeleted = 0 AND ah.Status = N'Active'
                    ) THEN 1 ELSE 0 END) AS AssignedDrivers
                  FROM Drivers d
                  WHERE d.IsDeleted = 0 AND d.TenantId = @TenantId",
                new
                {
                    TenantId = tenantId,
                    OnTrip = (int)DriverStatus.OnTrip,
                    OffDuty = (int)DriverStatus.OffDuty,
                    Available = (int)DriverStatus.Available,
                    OnLeave = (int)DriverStatus.OnLeave,
                    Suspended = (int)DriverStatus.Suspended
                },
                cancellationToken: cancellationToken));

        return ApiResponse<DriverStatsDto>.SuccessResponse(stats);
    }
}
