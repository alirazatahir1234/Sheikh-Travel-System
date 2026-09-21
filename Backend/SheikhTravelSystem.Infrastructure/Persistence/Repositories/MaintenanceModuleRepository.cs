using Dapper;
using FluentValidation;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.MaintenanceModule;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;
using System.Text.Json;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class MaintenanceModuleRepository(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IFileStorageService fileStorage,
    ILogger<MaintenanceModuleRepository> logger) : IMaintenanceModuleRepository
{

    public async Task<ApiResponse<MaintenanceRequestStatsDto>> GetMaintenanceRequestStatsAsync(GetMaintenanceRequestStatsQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var stats = await connection.QuerySingleAsync<MaintenanceRequestStatsDto>(new CommandDefinition("""
            SELECT
                (SELECT COUNT(*) FROM MaintenanceRequests WHERE TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Open') AS [Open],
                (SELECT COUNT(*) FROM MaintenanceRequests WHERE TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Approved') AS [Approved],
                (SELECT COUNT(*) FROM MaintenanceRequests WHERE TenantId = @TenantId AND IsDeleted = 0 AND Status IN (N'InProgress', N'Converted')
                    AND WorkOrderId IS NOT NULL) AS [InProgress],
                (SELECT COUNT(*) FROM MaintenanceRequests WHERE TenantId = @TenantId AND IsDeleted = 0 AND Status = N'PendingApproval') AS [PendingApproval]
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<MaintenanceRequestStatsDto>.SuccessResponse(stats);
    
    }

    public async Task<ApiResponse<bool>> ApproveMaintenanceRequestAsync(ApproveMaintenanceRequestCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var current = await connection.QuerySingleOrDefaultAsync<(string Status, int VehicleId)>(
            new CommandDefinition(
                "SELECT Status, VehicleId FROM MaintenanceRequests WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (string.IsNullOrEmpty(current.Status))
            throw new NotFoundException("MaintenanceRequest", request.Id);

        if (!MaintenanceRequestValidation.CanApprove(current.Status))
            throw new ConflictException($"Cannot approve request in status {current.Status}.");

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE MaintenanceRequests SET Status = N'Approved', ApprovedBy = @By, ApprovedAt = GETUTCDATE(), UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { request.Id, TenantId = tenantId, By = currentUser.UserId?.ToString() ?? "system" },
            cancellationToken: cancellationToken));

        await MaintenanceAlertHelper.InsertAlertAsync(connection, tenantId, current.VehicleId, "RequestApproved", "Info",
            "Maintenance request approved", $"Request #{request.Id} was approved.", "MaintenanceRequest", request.Id, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<bool>> RejectMaintenanceRequestAsync(RejectMaintenanceRequestCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var current = await connection.QuerySingleOrDefaultAsync<(string Status, string Priority, int VehicleId)>(
            new CommandDefinition(
                "SELECT Status, Priority, VehicleId FROM MaintenanceRequests WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (string.IsNullOrEmpty(current.Status))
            throw new NotFoundException("MaintenanceRequest", request.Id);

        if (!MaintenanceRequestValidation.CanReject(current.Status))
            throw new ConflictException($"Cannot reject request in status {current.Status}.");

        MaintenanceRequestValidation.ValidateRejectReason(current.Priority, request.Body.Reason);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE MaintenanceRequests SET Status = N'Rejected', RejectionReason = @Reason,
                RejectedBy = @By, RejectedAt = GETUTCDATE(), UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            request.Id,
            TenantId = tenantId,
            Reason = request.Body.Reason.Trim(),
            By = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE MaintenanceAlerts SET IsDismissed = 1
            WHERE TenantId = @TenantId AND ReferenceType = N'MaintenanceRequest' AND ReferenceId = @Id
            """, new { TenantId = tenantId, request.Id }, cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<IReadOnlyList<MaintenanceSearchResultDto>>> SearchMaintenanceAsync(SearchMaintenanceQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var q = $"%{request.Q.Trim()}%";
        using var connection = dbFactory.CreateConnection();

        var results = new List<MaintenanceSearchResultDto>();

        var requests = await connection.QueryAsync<MaintenanceSearchResultDto>(new CommandDefinition("""
            SELECT TOP (@Limit) N'Request' AS EntityType, r.Id,
                r.RequestNumber AS Title,
                CONCAT(v.Name, N' — ', r.IssueCategory) AS Subtitle,
                CONCAT(N'/fleet/maintenance/requests?id=', r.Id) AS RouteHint
            FROM MaintenanceRequests r
            INNER JOIN Vehicles v ON v.Id = r.VehicleId
            LEFT JOIN Drivers d ON d.Id = r.DriverId
            WHERE r.TenantId = @TenantId AND r.IsDeleted = 0
              AND (r.RequestNumber LIKE @Q OR v.Name LIKE @Q OR v.RegistrationNumber LIKE @Q OR d.FullName LIKE @Q)
            ORDER BY r.CreatedAt DESC
            """, new { TenantId = tenantId, Q = q, request.Limit }, cancellationToken: cancellationToken));

        results.AddRange(requests);

        var workOrders = await connection.QueryAsync<MaintenanceSearchResultDto>(new CommandDefinition("""
            SELECT TOP (@Limit) N'WorkOrder' AS EntityType, wo.Id,
                wo.WorkOrderNumber AS Title,
                CONCAT(v.Name, N' — ', ISNULL(wo.ServiceTypeName, N'')) AS Subtitle,
                CONCAT(N'/fleet/maintenance/work-orders?id=', wo.Id) AS RouteHint
            FROM WorkOrders wo
            INNER JOIN Vehicles v ON v.Id = wo.VehicleId
            LEFT JOIN Workshops w ON w.Id = wo.WorkshopId
            WHERE wo.TenantId = @TenantId AND wo.IsDeleted = 0
              AND (wo.WorkOrderNumber LIKE @Q OR v.Name LIKE @Q OR v.RegistrationNumber LIKE @Q OR w.Name LIKE @Q)
            ORDER BY wo.CreatedAt DESC
            """, new { TenantId = tenantId, Q = q, request.Limit }, cancellationToken: cancellationToken));

        results.AddRange(workOrders);

        return ApiResponse<IReadOnlyList<MaintenanceSearchResultDto>>.SuccessResponse(results.Take(request.Limit).ToList());
    
    }

    public async Task<ApiResponse<bool>> DismissMaintenanceAlertAsync(DismissMaintenanceAlertCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE MaintenanceAlerts SET IsDismissed = 1 WHERE Id = @Id AND TenantId = @TenantId",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (affected == 0) throw new NotFoundException("MaintenanceAlert", request.Id);
        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<ComplianceSummaryDto>> GetMaintenanceComplianceSummaryAsync(GetMaintenanceComplianceSummaryQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var summary = await connection.QuerySingleAsync<ComplianceSummaryDto>(new CommandDefinition("""
            SELECT
                (SELECT COUNT(*) FROM ComplianceDocuments WHERE TenantId = @TenantId AND IsDeleted = 0 AND EntityType = N'Vehicle'
                    AND ExpiryDate < GETUTCDATE()) AS Expired,
                (SELECT COUNT(*) FROM ComplianceDocuments WHERE TenantId = @TenantId AND IsDeleted = 0 AND EntityType = N'Vehicle'
                    AND ExpiryDate >= GETUTCDATE() AND ExpiryDate <= DATEADD(DAY, 7, GETUTCDATE())) AS Expiring7Days,
                (SELECT COUNT(*) FROM ComplianceDocuments WHERE TenantId = @TenantId AND IsDeleted = 0 AND EntityType = N'Vehicle'
                    AND ExpiryDate > DATEADD(DAY, 7, GETUTCDATE()) AND ExpiryDate <= DATEADD(DAY, 15, GETUTCDATE())) AS Expiring15Days,
                (SELECT COUNT(*) FROM ComplianceDocuments WHERE TenantId = @TenantId AND IsDeleted = 0 AND EntityType = N'Vehicle'
                    AND ExpiryDate > DATEADD(DAY, 15, GETUTCDATE()) AND ExpiryDate <= DATEADD(DAY, 30, GETUTCDATE())) AS Expiring30Days
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<ComplianceSummaryDto>.SuccessResponse(summary);
    
    }

    public async Task<ApiResponse<string>> UploadMaintenanceRequestAttachmentAsync(UploadMaintenanceRequestAttachmentCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var photosJson = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT PhotosJson FROM MaintenanceRequests WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = request.RequestId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (photosJson is null && !await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM MaintenanceRequests WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { Id = request.RequestId, TenantId = tenantId }, cancellationToken: cancellationToken)))
            throw new NotFoundException("MaintenanceRequest", request.RequestId);

        var folder = $"maintenance/requests/{tenantId}/{request.RequestId}";
        var stored = await fileStorage.SaveAsync(request.FileStream, request.FileName, request.ContentType, folder, cancellationToken);
        var url = stored.ReadUrl;

        var urls = string.IsNullOrWhiteSpace(photosJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(photosJson) ?? new List<string>();
        urls.Add(url);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE MaintenanceRequests SET PhotosJson = @Json, UpdatedAt = GETUTCDATE() WHERE Id = @Id AND TenantId = @TenantId",
            new { Json = JsonSerializer.Serialize(urls), Id = request.RequestId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        return ApiResponse<string>.SuccessResponse(url);
    
    }

    public async Task<ApiResponse<MaintenanceDashboardDto>> GetMaintenanceDashboardAsync(GetMaintenanceDashboardQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var (from, to) = ResolveRange(request.From, request.To, request.Period);
        using var connection = dbFactory.CreateConnection();

        var branchClause = request.BranchId.HasValue ? " AND v.BranchId = @BranchId" : "";
        var p = new { TenantId = tenantId, From = from, To = to, BranchId = request.BranchId };

        var kpis = await connection.QuerySingleAsync<MaintenanceKpiDto>(new CommandDefinition($"""
            SELECT
                (SELECT COUNT(*) FROM Vehicles v WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId AND v.Status <> @Retired {branchClause}) AS TotalVehicles,
                (SELECT COUNT(DISTINCT v.Id) FROM Vehicles v
                    LEFT JOIN Maintenance m ON m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.Status IN (1, 2)
                    LEFT JOIN VehicleMaintenanceSchedules s ON s.VehicleId = v.Id AND s.IsDeleted = 0 AND s.IsActive = 1
                    WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
                      AND (
                        (m.NextDueDate IS NOT NULL AND m.NextDueDate <= DATEADD(DAY, 15, GETUTCDATE()) AND m.NextDueDate >= GETUTCDATE())
                        OR (s.NextDueDate IS NOT NULL AND s.NextDueDate <= DATEADD(DAY, 15, GETUTCDATE()) AND s.NextDueDate >= GETUTCDATE())
                      )) AS DueForService,
                (SELECT COUNT(DISTINCT v.Id) FROM Vehicles v
                    LEFT JOIN WorkOrders wo ON wo.VehicleId = v.Id AND wo.IsDeleted = 0 AND wo.Status IN (N'Assigned', N'InProgress', N'WaitingParts')
                    WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
                      AND (v.Status = @Maintenance OR wo.Id IS NOT NULL)) AS UnderMaintenance,
                (SELECT COUNT(DISTINCT v.Id) FROM Vehicles v
                    LEFT JOIN Maintenance m ON m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.Status IN (1, 2)
                    LEFT JOIN VehicleMaintenanceSchedules s ON s.VehicleId = v.Id AND s.IsDeleted = 0 AND s.IsActive = 1
                    WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
                      AND (
                        (m.NextDueDate IS NOT NULL AND m.NextDueDate < GETUTCDATE())
                        OR (s.NextDueDate IS NOT NULL AND s.NextDueDate < GETUTCDATE())
                      )) AS OverdueServices,
                (SELECT ISNULL(SUM(ISNULL(m.Cost,0) + ISNULL(m.LaborCost,0) + ISNULL(m.PartsCost,0)), 0)
                    FROM Maintenance m
                    INNER JOIN Vehicles v ON v.Id = m.VehicleId
                    WHERE m.IsDeleted = 0 AND v.TenantId = @TenantId
                      AND m.MaintenanceDate >= @From AND m.MaintenanceDate < @To {branchClause})
                  + (SELECT ISNULL(SUM(wo.LaborCost + wo.PartsCost), 0)
                    FROM WorkOrders wo
                    INNER JOIN Vehicles v ON v.Id = wo.VehicleId
                    WHERE wo.IsDeleted = 0 AND v.TenantId = @TenantId
                      AND wo.CreatedAt >= @From AND wo.CreatedAt < @To {branchClause}) AS MonthlyMaintenanceCost,
                (SELECT COUNT(*) FROM WorkOrders wo
                    INNER JOIN Vehicles v ON v.Id = wo.VehicleId
                    WHERE wo.IsDeleted = 0 AND v.TenantId = @TenantId
                      AND wo.Status IN (N'Draft', N'Open', N'Assigned', N'InProgress', N'WaitingParts') {branchClause}) AS ActiveWorkOrders,
                (SELECT COUNT(*) FROM MaintenanceRequests r
                    INNER JOIN Vehicles v ON v.Id = r.VehicleId
                    WHERE r.IsDeleted = 0 AND r.TenantId = @TenantId
                      AND r.Status IN (N'Open', N'PendingApproval') {branchClause.Replace("v.", "v.")}) AS PendingRequests
            """, new
        {
            TenantId = tenantId,
            From = from,
            To = to,
            BranchId = request.BranchId,
            Retired = (int)VehicleStatus.Retired,
            Maintenance = (int)VehicleStatus.Maintenance
        }, cancellationToken: cancellationToken));

        var costTrend = await GetCostTrendAsync(connection, tenantId, from, to, request.Granularity, request.BranchId, cancellationToken);

        var health = await connection.QuerySingleAsync<VehicleHealthDto>(new CommandDefinition($"""
            SELECT
                (SELECT COUNT(*) FROM Vehicles v WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
                    AND v.Status NOT IN (@Maintenance, @Retired)
                    AND NOT EXISTS (
                        SELECT 1 FROM Maintenance m WHERE m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.Status IN (1,2)
                          AND m.NextDueDate IS NOT NULL AND m.NextDueDate <= DATEADD(DAY, 15, GETUTCDATE()))
                    AND NOT EXISTS (
                        SELECT 1 FROM VehicleMaintenanceSchedules s WHERE s.VehicleId = v.Id AND s.IsDeleted = 0 AND s.IsActive = 1
                          AND s.NextDueDate IS NOT NULL AND s.NextDueDate <= DATEADD(DAY, 15, GETUTCDATE()))
                    AND NOT EXISTS (
                        SELECT 1 FROM WorkOrders wo WHERE wo.VehicleId = v.Id AND wo.IsDeleted = 0
                          AND wo.Status IN (N'Assigned', N'InProgress', N'WaitingParts'))) AS Healthy,
                (SELECT COUNT(DISTINCT v.Id) FROM Vehicles v
                    LEFT JOIN Maintenance m ON m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.Status IN (1,2)
                    LEFT JOIN VehicleMaintenanceSchedules s ON s.VehicleId = v.Id AND s.IsDeleted = 0 AND s.IsActive = 1
                    WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
                      AND v.Status NOT IN (@Maintenance, @Retired)
                      AND (
                        (m.NextDueDate IS NOT NULL AND m.NextDueDate <= DATEADD(DAY, 15, GETUTCDATE()) AND m.NextDueDate >= GETUTCDATE())
                        OR (s.NextDueDate IS NOT NULL AND s.NextDueDate <= DATEADD(DAY, 15, GETUTCDATE()) AND s.NextDueDate >= GETUTCDATE())
                      )) AS ServiceDueSoon,
                (SELECT COUNT(DISTINCT v.Id) FROM Vehicles v
                    LEFT JOIN Maintenance m ON m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.Status IN (1,2)
                    LEFT JOIN VehicleMaintenanceSchedules s ON s.VehicleId = v.Id AND s.IsDeleted = 0 AND s.IsActive = 1
                    WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
                      AND (
                        (m.NextDueDate IS NOT NULL AND m.NextDueDate < GETUTCDATE())
                        OR (s.NextDueDate IS NOT NULL AND s.NextDueDate < GETUTCDATE())
                      )) AS Overdue,
                (SELECT COUNT(DISTINCT v.Id) FROM Vehicles v
                    LEFT JOIN WorkOrders wo ON wo.VehicleId = v.Id AND wo.IsDeleted = 0 AND wo.Status IN (N'Assigned', N'InProgress', N'WaitingParts')
                    WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
                      AND (v.Status = @Maintenance OR wo.Id IS NOT NULL)) AS InWorkshop
            """, new
        {
            TenantId = tenantId,
            BranchId = request.BranchId,
            Maintenance = (int)VehicleStatus.Maintenance,
            Retired = (int)VehicleStatus.Retired
        }, cancellationToken: cancellationToken));

        var alerts = await connection.QueryAsync<MaintenanceAlertDto>(new CommandDefinition($"""
            SELECT TOP 10 a.Id, a.VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS VehicleRegistration,
                   a.AlertType, a.Severity, a.Title, a.Message, a.CreatedAt
            FROM MaintenanceAlerts a
            LEFT JOIN Vehicles v ON v.Id = a.VehicleId
            WHERE a.TenantId = @TenantId AND a.IsDismissed = 0
              AND a.Severity IN (N'Critical', N'Error', N'Warning')
            ORDER BY a.CreatedAt DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        var recentWorkOrders = await connection.QueryAsync<WorkOrderListItemDto>(new CommandDefinition($"""
            SELECT TOP 5 {MaintenanceSql.WorkOrderListSelect}
            {MaintenanceSql.WorkOrderListFrom}
            WHERE wo.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause.Replace("v.", "v.")}
            ORDER BY wo.CreatedAt DESC
            """, new { TenantId = tenantId, BranchId = request.BranchId }, cancellationToken: cancellationToken));

        var upcoming = await connection.QueryAsync<UpcomingServiceDto>(new CommandDefinition($"""
            SELECT TOP 10 * FROM (
                SELECT s.Id AS ScheduleId, s.VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS VehicleRegistration,
                       s.ServiceTypeName AS ServiceType, s.NextDueDate AS DueDate, s.NextDueMileage AS DueMileage, s.Priority
                FROM VehicleMaintenanceSchedules s
                INNER JOIN Vehicles v ON v.Id = s.VehicleId AND v.IsDeleted = 0
                WHERE s.IsDeleted = 0 AND s.IsActive = 1 AND s.TenantId = @TenantId
                  AND s.NextDueDate IS NOT NULL {branchClause}
                UNION ALL
                SELECT NULL, m.VehicleId, v.Name, v.RegistrationNumber,
                       ISNULL(m.MaintenanceType, m.Description), m.NextDueDate, m.NextDueMileage, ISNULL(m.Priority, N'Medium')
                FROM Maintenance m
                INNER JOIN Vehicles v ON v.Id = m.VehicleId AND v.IsDeleted = 0
                WHERE m.IsDeleted = 0 AND v.TenantId = @TenantId AND m.Status IN (1,2)
                  AND m.NextDueDate IS NOT NULL {branchClause}
            ) u
            ORDER BY DueDate ASC
            """, new { TenantId = tenantId, BranchId = request.BranchId }, cancellationToken: cancellationToken));

        var fuelSummary = await GetFuelSummaryAsync(connection, tenantId, from, to, request.BranchId, cancellationToken);

        return ApiResponse<MaintenanceDashboardDto>.SuccessResponse(new MaintenanceDashboardDto(
            kpis, costTrend, health, alerts.ToList(), recentWorkOrders.ToList(), upcoming.ToList(), fuelSummary));
    
    }

    public async Task<ApiResponse<IReadOnlyList<MaintenanceAlertDto>>> GetMaintenanceAlertsAsync(GetMaintenanceAlertsQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<MaintenanceAlertDto>(new CommandDefinition($"""
            SELECT TOP (@Limit) a.Id, a.VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS VehicleRegistration,
                   a.AlertType, a.Severity, a.Title, a.Message, a.CreatedAt
            FROM MaintenanceAlerts a
            LEFT JOIN Vehicles v ON v.Id = a.VehicleId
            WHERE a.TenantId = @TenantId AND a.IsDismissed = 0
            ORDER BY a.CreatedAt DESC
            """, new { TenantId = tenantId, request.Limit }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<MaintenanceAlertDto>>.SuccessResponse(rows.ToList());
    
    }

    public async Task<ApiResponse<IReadOnlyList<VendorDto>>> ListVendorsAsync(ListVendorsQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<VendorRow>(new CommandDefinition(
            $"""
            SELECT {MaintenanceSql.VendorSelect}
            FROM Vendors v
            WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId
            ORDER BY v.Name
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<VendorDto>>.SuccessResponse(rows.Select(r => r.ToDto()).ToList());
    
    }

    public async Task<ApiResponse<VendorDto>> GetVendorByIdAsync(GetVendorByIdQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<VendorRow>(new CommandDefinition(
            $"""
            SELECT {MaintenanceSql.VendorSelect}
            FROM Vendors v
            WHERE v.Id = @Id AND v.TenantId = @TenantId AND v.IsDeleted = 0
            """,
            new { request.Id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (row is null)
            throw new NotFoundException("Vendor", request.Id);

        return ApiResponse<VendorDto>.SuccessResponse(row.ToDto());
    
    }

    public async Task<ApiResponse<int>> CreateMaintenanceScheduleAsync(CreateMaintenanceScheduleCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;
        using var connection = dbFactory.CreateConnection();

        // Use a concrete class — Dapper cannot reliably map Nullable<ValueTuple>, which
        // previously made existing vehicles look "not found" on schedule create.
        var vehicle = await connection.QuerySingleOrDefaultAsync<VehicleLookupRow>(
            new CommandDefinition(
                """
                SELECT CurrentMileage, ISNULL(CurrentEngineHours, 0) AS CurrentEngineHours, Status
                FROM Vehicles
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
                """,
                new { Id = body.VehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (vehicle is null)
        {
            var orphan = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT TenantId FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                new { Id = body.VehicleId },
                cancellationToken: cancellationToken));

            if (orphan is null)
                throw new NotFoundException("Vehicle", body.VehicleId);

            throw new ValidationException(
                "Selected vehicle belongs to a different organization. Refresh the page and choose a vehicle from the current tenant.");
        }

        if (vehicle.Status == (int)VehicleStatus.Draft)
            throw new ValidationException("This vehicle is still a draft. Publish it in Fleet Management before scheduling maintenance.");

        if (vehicle.Status == (int)VehicleStatus.Retired)
            throw new ValidationException("Cannot schedule maintenance for a retired vehicle.");

        var lastMileage = body.LastServiceMileage ?? vehicle.CurrentMileage;
        var lastEngineHours = body.LastServiceEngineHours ?? vehicle.CurrentEngineHours;
        var lastDate = body.LastServiceDate ?? DateTime.UtcNow.Date;

        var (nextMileage, nextEngineHours, nextDate) = MaintenanceScheduleHelper.RecomputeNextDue(
            body.IntervalType, body.IntervalValue, lastDate, lastMileage, lastEngineHours);

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO VehicleMaintenanceSchedules
                (TenantId, VehicleId, ServiceTypeId, ServiceTypeName, IntervalType, IntervalValue,
                 LastServiceDate, LastServiceMileage, LastServiceEngineHours,
                 NextDueDate, NextDueMileage, NextDueEngineHours, Priority)
            VALUES
                (@TenantId, @VehicleId, @ServiceTypeId, @ServiceTypeName, @IntervalType, @IntervalValue,
                 @LastServiceDate, @LastServiceMileage, @LastServiceEngineHours,
                 @NextDueDate, @NextDueMileage, @NextDueEngineHours, @Priority);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            body.VehicleId,
            body.ServiceTypeId,
            body.ServiceTypeName,
            body.IntervalType,
            body.IntervalValue,
            LastServiceDate = lastDate,
            LastServiceMileage = lastMileage,
            LastServiceEngineHours = lastEngineHours,
            NextDueDate = nextDate,
            NextDueMileage = nextMileage,
            NextDueEngineHours = nextEngineHours,
            body.Priority
        }, cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id);
    
    }

    public async Task<ApiResponse<IReadOnlyList<WorkshopDto>>> ListWorkshopsAsync(ListWorkshopsQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<WorkshopDto>(new CommandDefinition($"""
            SELECT {MaintenanceSql.WorkshopSelect}
            FROM Workshops w
            WHERE w.IsDeleted = 0 AND w.TenantId = @TenantId
            ORDER BY w.Name
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<WorkshopDto>>.SuccessResponse(rows.ToList());
    
    }

    public async Task<ApiResponse<int>> CreateWorkshopAsync(CreateWorkshopCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;
        using var connection = dbFactory.CreateConnection();

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Workshops
                (TenantId, Name, WorkshopType, Location, ContactPerson, ContactPhone, ContactEmail,
                 Capacity, VendorType, ContractDetails, SLA, Rating, CreatedBy, CreatedAt)
            VALUES
                (@TenantId, @Name, @WorkshopType, @Location, @ContactPerson, @ContactPhone, @ContactEmail,
                 @Capacity, @VendorType, @ContractDetails, @SLA, @Rating, @CreatedBy, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            body.Name,
            body.WorkshopType,
            body.Location,
            body.ContactPerson,
            body.ContactPhone,
            body.ContactEmail,
            body.Capacity,
            body.VendorType,
            body.ContractDetails,
            body.SLA,
            body.Rating,
            CreatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id);
    
    }

    public async Task<ApiResponse<IReadOnlyList<ServiceTypeDto>>> ListServiceTypesAsync(ListServiceTypesQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<ServiceTypeDto>(new CommandDefinition("""
            SELECT Id, Code, Name, IsPreventive FROM ServiceTypes
            WHERE IsDeleted = 0 AND (TenantId IS NULL OR TenantId = @TenantId) AND IsActive = 1
            ORDER BY SortOrder, Name
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<ServiceTypeDto>>.SuccessResponse(rows.ToList());
    
    }

    public async Task<ApiResponse<int>> CreateVendorAsync(CreateVendorCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;
        using var connection = dbFactory.CreateConnection();

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Vendors
                (TenantId, Name, Category, ContactPerson, ContactPhone, ContactEmail,
                 ProductsJson, Rating, IsPreferred, CreatedBy, CreatedAt)
            VALUES
                (@TenantId, @Name, @Category, @ContactPerson, @ContactPhone, @ContactEmail,
                 @ProductsJson, @Rating, @IsPreferred, @CreatedBy, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            body.Name,
            body.Category,
            body.ContactPerson,
            body.ContactPhone,
            body.ContactEmail,
            ProductsJson = VendorHelper.SerializeProducts(body.Products),
            body.Rating,
            body.IsPreferred,
            CreatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id);
    
    }

    public async Task<ApiResponse<bool>> UpdateVendorAsync(UpdateVendorCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Vendors WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (exists == 0) throw new NotFoundException("Vendor", request.Id);

        var body = request.Body;
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Vendors SET
                Name = COALESCE(@Name, Name),
                Category = COALESCE(@Category, Category),
                ContactPerson = COALESCE(@ContactPerson, ContactPerson),
                ContactPhone = COALESCE(@ContactPhone, ContactPhone),
                ContactEmail = COALESCE(@ContactEmail, ContactEmail),
                ProductsJson = COALESCE(@ProductsJson, ProductsJson),
                Rating = COALESCE(@Rating, Rating),
                IsPreferred = COALESCE(@IsPreferred, IsPreferred),
                IsActive = COALESCE(@IsActive, IsActive),
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            request.Id,
            TenantId = tenantId,
            body.Name,
            body.Category,
            body.ContactPerson,
            body.ContactPhone,
            body.ContactEmail,
            ProductsJson = body.Products is null ? null : VendorHelper.SerializeProducts(body.Products),
            body.Rating,
            body.IsPreferred,
            body.IsActive,
            UpdatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(rows > 0);
    
    }

    public async Task<ApiResponse<bool>> SetVendorActiveAsync(SetVendorActiveCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Vendors SET IsActive = @IsActive, UpdatedBy = @UpdatedBy, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new
        {
            request.Id,
            TenantId = tenantId,
            request.IsActive,
            UpdatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        if (rows == 0) throw new NotFoundException("Vendor", request.Id);
        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<int>> CreateWorkOrderAsync(CreateWorkOrderCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;
        using var connection = dbFactory.CreateConnection();

        var vehicleId = body.VehicleId;
        if (vehicleId <= 0)
            throw new ConflictException("Please select a vehicle.");

        var vehicleExists = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT 1 FROM Vehicles WHERE Id = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0",
            new { VehicleId = vehicleId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (vehicleExists is null)
            throw new NotFoundException("Vehicle", vehicleId);

        var workshopId = WorkOrderValidation.NormalizeOptionalId(body.WorkshopId);
        if (workshopId.HasValue)
        {
            var workshopExists = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT 1 FROM Workshops WHERE Id = @WorkshopId AND TenantId = @TenantId AND IsDeleted = 0",
                new { WorkshopId = workshopId.Value, TenantId = tenantId }, cancellationToken: cancellationToken));

            if (workshopExists is null)
                throw new NotFoundException("Workshop", workshopId.Value);
        }

        var technicianId = WorkOrderValidation.NormalizeOptionalId(body.TechnicianId);
        if (technicianId.HasValue)
        {
            var technicianExists = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT 1 FROM Technicians WHERE Id = @TechnicianId AND TenantId = @TenantId AND IsDeleted = 0",
                new { TechnicianId = technicianId.Value, TenantId = tenantId }, cancellationToken: cancellationToken));

            if (technicianExists is null)
                throw new NotFoundException("Technician", technicianId.Value);
        }

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO WorkOrders
                (TenantId, WorkOrderNumber, RequestId, ScheduleId, VehicleId, WorkshopId, TechnicianId,
                 ServiceTypeId, ServiceTypeName, MaintenanceType, StartDate, EstimatedCompletionDate,
                 LaborCost, PartsCost, EstimatedLaborCost, EstimatedPartsCost,
                 Priority, Notes, Status, CreatedBy, CreatedAt)
            VALUES
                (@TenantId, N'WO-PENDING', @RequestId, @ScheduleId, @VehicleId, @WorkshopId, @TechnicianId,
                 @ServiceTypeId, @ServiceTypeName, @MaintenanceType, @StartDate, @EstimatedCompletionDate,
                 @LaborCost, @PartsCost, @EstimatedLaborCost, @EstimatedPartsCost,
                 @Priority, @Notes, N'Open', @CreatedBy, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            body.RequestId,
            body.ScheduleId,
            body.VehicleId,
            WorkshopId = workshopId,
            TechnicianId = technicianId,
            body.ServiceTypeId,
            ServiceTypeName = body.ServiceTypeName?.Trim(),
            MaintenanceType = WorkOrderValidation.NormalizeMaintenanceType(body.MaintenanceType),
            StartDate = WorkOrderValidation.NormalizeDate(body.StartDate),
            EstimatedCompletionDate = WorkOrderValidation.NormalizeDate(body.EstimatedCompletionDate),
            body.LaborCost,
            body.PartsCost,
            EstimatedLaborCost = body.LaborCost,
            EstimatedPartsCost = body.PartsCost,
            Priority = body.Priority?.Trim(),
            Notes = string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim(),
            CreatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE WorkOrders SET WorkOrderNumber = @No WHERE Id = @Id",
            new { No = $"WO-{id:D5}", Id = id }, cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id);
    
    }

    public async Task<ApiResponse<bool>> UpdateWorkOrderStatusAsync(UpdateWorkOrderStatusCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var current = await connection.QuerySingleOrDefaultAsync<(string Status, int VehicleId)>(
            new CommandDefinition(
                "SELECT Status, VehicleId FROM WorkOrders WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (string.IsNullOrEmpty(current.Status))
            throw new NotFoundException("WorkOrder", request.Id);

        var newStatus = request.Body.Status.Trim();
        if (!MaintenanceValidation.CanTransition(current.Status, newStatus))
            throw new ConflictException($"Cannot transition work order from {current.Status} to {newStatus}.");

        var completedAt = MaintenanceValidation.IsTerminalWorkOrderStatus(newStatus) &&
                          newStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            ? DateTime.UtcNow : (DateTime?)null;

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WorkOrders SET
                Status = @Status,
                TechnicianNotes = COALESCE(@TechnicianNotes, TechnicianNotes),
                CompletedAt = COALESCE(@CompletedAt, CompletedAt),
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            request.Id,
            TenantId = tenantId,
            Status = newStatus,
            request.Body.TechnicianNotes,
            CompletedAt = completedAt,
            UpdatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        if (MaintenanceValidation.ShouldSetVehicleMaintenance(newStatus))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE Vehicles SET Status = @Status, UpdatedAt = GETUTCDATE() WHERE Id = @VehicleId AND TenantId = @TenantId",
                new { Status = MaintenanceValidation.VehicleMaintenanceStatus, current.VehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));
        }
        else if (MaintenanceValidation.IsTerminalWorkOrderStatus(newStatus))
        {
            var openCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM WorkOrders
                WHERE VehicleId = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0
                  AND Id <> @Id AND Status IN (N'Assigned', N'InProgress', N'WaitingParts')
                """, new { current.VehicleId, TenantId = tenantId, request.Id }, cancellationToken: cancellationToken));

            if (openCount == 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE Vehicles SET Status = @Status, UpdatedAt = GETUTCDATE() WHERE Id = @VehicleId AND TenantId = @TenantId AND Status = @Maintenance",
                    new
                    {
                        Status = MaintenanceValidation.VehicleAvailableStatus,
                        current.VehicleId,
                        TenantId = tenantId,
                        Maintenance = MaintenanceValidation.VehicleMaintenanceStatus
                    }, cancellationToken: cancellationToken));
            }

            if (newStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            {
                await MaintenanceAlertHelper.InsertAlertAsync(
                    connection, tenantId, current.VehicleId, "WorkOrderCompleted", "Info",
                    "Work order completed", $"Work order #{request.Id} has been completed.", "WorkOrder", request.Id, cancellationToken);
            }
        }

        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<bool>> UpdateWorkOrderAsync(UpdateWorkOrderCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var current = await connection.QuerySingleOrDefaultAsync<(string Status, int VehicleId)>(
            new CommandDefinition(
                "SELECT Status, VehicleId FROM WorkOrders WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (string.IsNullOrEmpty(current.Status))
            throw new NotFoundException("WorkOrder", request.Id);

        var body = request.Body;
        var newStatus = body.Status?.Trim();
        if (!string.IsNullOrWhiteSpace(newStatus) &&
            !MaintenanceValidation.CanTransition(current.Status, newStatus))
            throw new ConflictException($"Cannot transition work order from {current.Status} to {newStatus}.");

        var statusToSet = string.IsNullOrWhiteSpace(newStatus) ? current.Status : newStatus;
        if (body.WorkshopId.HasValue && statusToSet.Equals("Open", StringComparison.OrdinalIgnoreCase))
            statusToSet = "Assigned";

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WorkOrders SET
                WorkshopId = COALESCE(@WorkshopId, WorkshopId),
                TechnicianId = COALESCE(@TechnicianId, TechnicianId),
                Status = @Status,
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            request.Id,
            TenantId = tenantId,
            body.WorkshopId,
            body.TechnicianId,
            Status = statusToSet,
            UpdatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<WorkshopDto>> GetWorkshopByIdAsync(GetWorkshopByIdQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<WorkshopDto>(new CommandDefinition(
            $"""
            SELECT {MaintenanceSql.WorkshopSelect}
            FROM Workshops w
            WHERE w.Id = @Id AND w.TenantId = @TenantId AND w.IsDeleted = 0
            """,
            new { request.Id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (row is null)
            throw new NotFoundException("Workshop", request.Id);

        return ApiResponse<WorkshopDto>.SuccessResponse(row);
    
    }

    public async Task<ApiResponse<bool>> UpdateWorkshopAsync(UpdateWorkshopCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Workshops WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (exists == 0)
            throw new NotFoundException("Workshop", request.Id);

        var body = request.Body;
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Workshops SET
                Name = COALESCE(@Name, Name),
                WorkshopType = COALESCE(@WorkshopType, WorkshopType),
                Location = COALESCE(@Location, Location),
                ContactPerson = COALESCE(@ContactPerson, ContactPerson),
                ContactPhone = COALESCE(@ContactPhone, ContactPhone),
                ContactEmail = COALESCE(@ContactEmail, ContactEmail),
                Capacity = COALESCE(@Capacity, Capacity),
                VendorType = COALESCE(@VendorType, VendorType),
                ContractDetails = COALESCE(@ContractDetails, ContractDetails),
                SLA = COALESCE(@SLA, SLA),
                Rating = COALESCE(@Rating, Rating),
                IsActive = COALESCE(@IsActive, IsActive),
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            request.Id,
            TenantId = tenantId,
            body.Name,
            body.WorkshopType,
            body.Location,
            body.ContactPerson,
            body.ContactPhone,
            body.ContactEmail,
            body.Capacity,
            body.VendorType,
            body.ContractDetails,
            body.SLA,
            body.Rating,
            body.IsActive,
            UpdatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        if (rows == 0) throw new NotFoundException("Workshop", request.Id);
        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<bool>> SetWorkshopActiveAsync(SetWorkshopActiveCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Workshops SET IsActive = @IsActive, UpdatedBy = @UpdatedBy, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new
        {
            request.Id,
            TenantId = tenantId,
            request.IsActive,
            UpdatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        if (rows == 0) throw new NotFoundException("Workshop", request.Id);
        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<int>> CreateMaintenanceRequestAsync(CreateMaintenanceRequestCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;
        using var connection = dbFactory.CreateConnection();

        var vehicleExists = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT 1 FROM Vehicles WHERE Id = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0",
            new { body.VehicleId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (vehicleExists is null)
            throw new NotFoundException("Vehicle", body.VehicleId);

        var vehicleMeta = await connection.QuerySingleOrDefaultAsync<(int? BranchId, int? DepartmentId)>(
            new CommandDefinition(
                "SELECT BranchId, DepartmentId FROM Vehicles WHERE Id = @VehicleId AND TenantId = @TenantId",
                new { body.VehicleId, TenantId = tenantId }, cancellationToken: cancellationToken));

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO MaintenanceRequests
                (TenantId, RequestNumber, VehicleId, DriverId, BranchId, DepartmentId, RequestType, Priority, IssueCategory,
                 Description, BreakdownLocation, DriverRemarks, PhotosJson, DocumentsJson, Status, CreatedBy, CreatedAt)
            VALUES
                (@TenantId, N'MR-PENDING', @VehicleId, @DriverId, @BranchId, @DepartmentId, @RequestType, @Priority, @IssueCategory,
                 @Description, @BreakdownLocation, @DriverRemarks, @PhotosJson, @DocumentsJson, N'Open', @CreatedBy, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            body.VehicleId,
            body.DriverId,
            BranchId = vehicleMeta.BranchId,
            DepartmentId = vehicleMeta.DepartmentId,
            RequestType = body.RequestType.Trim(),
            Priority = body.Priority.Trim(),
            IssueCategory = body.IssueCategory.Trim(),
            Description = body.Description.Trim(),
            body.BreakdownLocation,
            body.DriverRemarks,
            body.PhotosJson,
            body.DocumentsJson,
            CreatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        var requestNumber = $"MR-{id:D5}";
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE MaintenanceRequests SET RequestNumber = @No WHERE Id = @Id",
            new { No = requestNumber, Id = id }, cancellationToken: cancellationToken));

        await MaintenanceAlertHelper.InsertAlertAsync(connection, tenantId, body.VehicleId, "RequestCreated",
            body.Priority.Equals("Critical", StringComparison.OrdinalIgnoreCase) ? "Critical" : "Info",
            $"New maintenance request {requestNumber}",
            $"Request created: {body.IssueCategory} — {body.Description[..Math.Min(200, body.Description.Length)]}",
            "MaintenanceRequest", id, cancellationToken);

        if (body.IssueCategory.Equals("Breakdown", StringComparison.OrdinalIgnoreCase) ||
            body.RequestType.Equals("Breakdown", StringComparison.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO VehicleBreakdowns (TenantId, VehicleId, DriverId, RequestId, BreakdownLocation, FaultReport, DriverRemarks, Status)
                VALUES (@TenantId, @VehicleId, @DriverId, @RequestId, @BreakdownLocation, @FaultReport, @DriverRemarks, N'Reported')
                """, new
            {
                TenantId = tenantId,
                body.VehicleId,
                body.DriverId,
                RequestId = id,
                body.BreakdownLocation,
                FaultReport = body.Description,
                body.DriverRemarks
            }, cancellationToken: cancellationToken));

            await MaintenanceAlertHelper.InsertAlertAsync(connection, tenantId, body.VehicleId, "Breakdown",
                "Critical", "Breakdown reported", body.Description, "MaintenanceRequest", id, cancellationToken);
        }

        return ApiResponse<int>.SuccessResponse(id);
    
    }

    public async Task<ApiResponse<bool>> UpdateMaintenanceRequestAsync(UpdateMaintenanceRequestCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();
        var body = request.Body;

        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE MaintenanceRequests SET
                Priority = COALESCE(@Priority, Priority),
                IssueCategory = COALESCE(@IssueCategory, IssueCategory),
                Description = COALESCE(@Description, Description),
                BreakdownLocation = COALESCE(@BreakdownLocation, BreakdownLocation),
                DriverRemarks = COALESCE(@DriverRemarks, DriverRemarks),
                Status = COALESCE(@Status, Status),
                UpdatedBy = @UpdatedBy,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new
        {
            request.Id,
            TenantId = tenantId,
            body.Priority,
            body.IssueCategory,
            body.Description,
            body.BreakdownLocation,
            body.DriverRemarks,
            body.Status,
            UpdatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        if (affected == 0)
            throw new NotFoundException("MaintenanceRequest", request.Id);

        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<int>> ConvertMaintenanceRequestAsync(ConvertMaintenanceRequestCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var req = await connection.QuerySingleOrDefaultAsync<(int VehicleId, string Priority, string IssueCategory, string Description, int? WorkOrderId, string Status)>(
            new CommandDefinition(
                "SELECT VehicleId, Priority, IssueCategory, Description, WorkOrderId, Status FROM MaintenanceRequests WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (req.VehicleId == 0)
            throw new NotFoundException("MaintenanceRequest", request.Id);

        if (req.WorkOrderId.HasValue)
            throw new ConflictException("This request has already been converted to a work order.");

        if (!MaintenanceRequestValidation.CanConvert(req.Status))
            throw new ConflictException($"Cannot convert request in status {req.Status}.");

        var body = request.Body;
        var serviceType = body.ServiceTypeName ?? req.IssueCategory;

        DriverAssignmentOps.OpenConnection(connection);
        using var transaction = connection.BeginTransaction();
        try
        {
            var woId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO WorkOrders
                    (TenantId, WorkOrderNumber, RequestId, VehicleId, WorkshopId, TechnicianId,
                     ServiceTypeId, ServiceTypeName, StartDate, EstimatedCompletionDate,
                     Priority, Notes, Status, CreatedBy, CreatedAt)
                VALUES
                    (@TenantId, N'WO-PENDING', @RequestId, @VehicleId, @WorkshopId, @TechnicianId,
                     @ServiceTypeId, @ServiceTypeName, @StartDate, @EstimatedCompletionDate,
                     @Priority, @Description, N'Open', @CreatedBy, GETUTCDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new
            {
                TenantId = tenantId,
                RequestId = request.Id,
                req.VehicleId,
                body.WorkshopId,
                body.TechnicianId,
                body.ServiceTypeId,
                ServiceTypeName = serviceType,
                StartDate = body.StartDate?.ToUniversalTime(),
                EstimatedCompletionDate = body.EstimatedCompletionDate?.ToUniversalTime(),
                Priority = req.Priority,
                Description = req.Description,
                CreatedBy = currentUser.UserId?.ToString() ?? "system"
            }, transaction: transaction, cancellationToken: cancellationToken));

            var woNumber = $"WO-{woId:D5}";

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE WorkOrders SET WorkOrderNumber = @No WHERE Id = @Id",
                new { No = woNumber, Id = woId }, transaction: transaction, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE MaintenanceRequests SET WorkOrderId = @WorkOrderId, Status = N'Converted', UpdatedAt = GETUTCDATE()
                WHERE Id = @Id AND TenantId = @TenantId
                """, new { WorkOrderId = woId, request.Id, TenantId = tenantId },
                transaction: transaction, cancellationToken: cancellationToken));

            transaction.Commit();

            await MaintenanceAlertHelper.InsertAlertAsync(
                connection, tenantId, req.VehicleId, "WorkOrderCreated", "Info",
                $"Work order {woNumber} created", $"Converted from maintenance request.", "WorkOrder", woId, cancellationToken);

            return ApiResponse<int>.SuccessResponse(woId);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    
    }

    public async Task<ApiResponse<IReadOnlyList<PartDto>>> ListPartsAsync(ListPartsQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var sql = """
            SELECT p.Id, p.PartNumber, p.PartName, p.Category, p.Brand, p.Supplier, p.UnitCost, p.MinStockLevel,
                   ISNULL(inv.StockQuantity, 0) AS StockQuantity,
                   p.VehicleCompatibilityJson,
                   inv.Location
            FROM Parts p
            LEFT JOIN (
                SELECT PartId, SUM(StockQuantity) AS StockQuantity,
                       MIN(NULLIF(Location, '')) AS Location
                FROM PartInventory
                WHERE TenantId = @TenantId
                GROUP BY PartId
            ) inv ON inv.PartId = p.Id
            WHERE p.IsDeleted = 0 AND p.TenantId = @TenantId
            """;

        if (!string.IsNullOrWhiteSpace(request.Search))
            sql += " AND (p.PartNumber LIKE @Search OR p.PartName LIKE @Search)";

        sql += " ORDER BY p.PartName";

        var search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search.Trim()}%";
        var rows = await connection.QueryAsync<PartRow>(new CommandDefinition(
            sql, new { TenantId = tenantId, Search = search }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<PartDto>>.SuccessResponse(rows.Select(r => r.ToDto()).ToList());
    
    }

    public async Task<ApiResponse<int>> CreatePartAsync(CreatePartCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;
        using var connection = dbFactory.CreateConnection();

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Parts (TenantId, PartNumber, PartName, Category, Brand, Supplier, UnitCost, MinStockLevel, VehicleCompatibilityJson)
            VALUES (@TenantId, @PartNumber, @PartName, @Category, @Brand, @Supplier, @UnitCost, @MinStockLevel, @VehicleCompatibilityJson);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            body.PartNumber,
            body.PartName,
            body.Category,
            body.Brand,
            body.Supplier,
            body.UnitCost,
            body.MinStockLevel,
            VehicleCompatibilityJson = PartStockHelper.SerializeCompatibility(body.VehicleCompatibility)
        }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO PartInventory (TenantId, PartId, StockQuantity, Location)
            VALUES (@TenantId, @PartId, @Stock, @Location)
            """, new
        {
            TenantId = tenantId,
            PartId = id,
            Stock = body.InitialStock,
            Location = body.Location
        }, cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id);
    
    }

    public async Task<ApiResponse<bool>> AddPartStockAsync(AddPartStockCommand request, CancellationToken cancellationToken = default)
    {
        if (request.Body.Quantity <= 0)
            throw new ValidationException("Quantity must be greater than zero.");

        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Parts WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = request.PartId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (exists == 0)
            throw new NotFoundException("Part", request.PartId);

        var invId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 Id FROM PartInventory
            WHERE PartId = @PartId AND TenantId = @TenantId
            ORDER BY Id
            """, new { PartId = request.PartId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (invId is null)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO PartInventory (TenantId, PartId, StockQuantity, Location)
                VALUES (@TenantId, @PartId, @Qty, @Location)
                """, new
            {
                TenantId = tenantId,
                PartId = request.PartId,
                Qty = request.Body.Quantity,
                Location = request.Body.Location
            }, cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE PartInventory
                SET StockQuantity = StockQuantity + @Qty,
                    Location = COALESCE(@Location, Location),
                    UpdatedAt = GETUTCDATE()
                WHERE Id = @Id
                """, new
            {
                Id = invId,
                Qty = request.Body.Quantity,
                Location = request.Body.Location
            }, cancellationToken: cancellationToken));
        }

        var createdBy = currentUser.UserId?.ToString() ?? "system";
        await PartStockSql.InsertMovementAsync(
            connection, tenantId, request.PartId, "Receipt", request.Body.Quantity,
            null, request.Body.Location, null, null, request.Body.Notes, createdBy, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<int>> IssuePartAsync(IssuePartCommand request, CancellationToken cancellationToken = default)
    {
        if (request.Body.Quantity <= 0)
            throw new ValidationException("Quantity must be greater than zero.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var workOrderId = request.Body.WorkOrderId is > 0 ? request.Body.WorkOrderId : null;
        using var connection = dbFactory.CreateConnection();

        var part = await connection.QuerySingleOrDefaultAsync<PartIssueRow>(new CommandDefinition(
            "SELECT UnitCost, PartName FROM Parts WHERE Id = @PartId AND TenantId = @TenantId AND IsDeleted = 0",
            new { PartId = request.PartId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (part is null)
            throw new NotFoundException("Part", request.PartId);

        var vehicleExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = request.Body.VehicleId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (vehicleExists == 0)
            throw new NotFoundException("Vehicle", request.Body.VehicleId);

        if (workOrderId is int woId)
        {
            var woExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM WorkOrders WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = woId, TenantId = tenantId }, cancellationToken: cancellationToken));
            if (woExists == 0)
                throw new NotFoundException("WorkOrder", woId);
        }

        var stock = await PartStockSql.GetTotalStockAsync(connection, tenantId, request.PartId, cancellationToken);
        if (stock < request.Body.Quantity)
            throw new ValidationException("Insufficient stock for this issue.");

        var total = part.UnitCost * request.Body.Quantity;
        var createdBy = currentUser.UserId?.ToString() ?? "system";

        var usageId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO PartUsage (TenantId, VehicleId, WorkOrderId, PartId, Quantity, UnitCost, CreatedBy)
            VALUES (@TenantId, @VehicleId, @WorkOrderId, @PartId, @Quantity, @UnitCost, @CreatedBy);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            request.Body.VehicleId,
            WorkOrderId = workOrderId,
            PartId = request.PartId,
            request.Body.Quantity,
            part.UnitCost,
            CreatedBy = createdBy
        }, cancellationToken: cancellationToken));

        await DeductStockAsync(connection, tenantId, request.PartId, request.Body.Quantity, cancellationToken);

        if (workOrderId is int linkedWorkOrderId)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE WorkOrders SET PartsCost = ISNULL(PartsCost, 0) + @Total, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { Total = total, Id = linkedWorkOrderId }, cancellationToken: cancellationToken));
        }

        await PartStockSql.InsertMovementAsync(
            connection, tenantId, request.PartId, "Issue", request.Body.Quantity,
            null, null, request.Body.VehicleId, workOrderId, request.Body.Notes, createdBy, cancellationToken);

        var newStock = await PartStockSql.GetTotalStockAsync(connection, tenantId, request.PartId, cancellationToken);
        var minStock = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT MinStockLevel FROM Parts WHERE Id = @PartId",
            new { PartId = request.PartId }, cancellationToken: cancellationToken));

        await PartStockSql.MaybeInsertLowStockAlertAsync(
            connection, tenantId, request.PartId, part.PartName, newStock, minStock, cancellationToken);

        return ApiResponse<int>.SuccessResponse(usageId);
    
    }

    public async Task<ApiResponse<bool>> TransferPartStockAsync(TransferPartStockCommand request, CancellationToken cancellationToken = default)
    {
        var body = request.Body;
        if (body.Quantity <= 0)
            throw new ValidationException("Quantity must be greater than zero.");
        if (string.IsNullOrWhiteSpace(body.ToLocation))
            throw new ValidationException("Destination location is required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var partExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Parts WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = request.PartId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (partExists == 0)
            throw new NotFoundException("Part", request.PartId);

        var fromLocation = body.FromLocation?.Trim() ?? string.Empty;
        var toLocation = body.ToLocation.Trim();

        var source = await connection.QuerySingleOrDefaultAsync<(int Id, int StockQuantity, string? Location)>(
            new CommandDefinition("""
                SELECT TOP 1 Id, StockQuantity, Location FROM PartInventory
                WHERE PartId = @PartId AND TenantId = @TenantId
                  AND LOWER(LTRIM(RTRIM(ISNULL(Location, '')))) = LOWER(LTRIM(RTRIM(@FromLocation)))
                ORDER BY Id
                """, new { PartId = request.PartId, TenantId = tenantId, FromLocation = fromLocation },
                cancellationToken: cancellationToken));

        if (source.Id == 0 || source.StockQuantity < body.Quantity)
            throw new ValidationException("Insufficient stock at the source location.");

        if (body.Quantity == source.StockQuantity && string.Equals(fromLocation, toLocation, StringComparison.OrdinalIgnoreCase))
            return ApiResponse<bool>.SuccessResponse(true);

        if (body.Quantity == source.StockQuantity)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE PartInventory SET Location = @ToLocation, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { ToLocation = toLocation, source.Id }, cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE PartInventory SET StockQuantity = StockQuantity - @Qty, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { Qty = body.Quantity, source.Id }, cancellationToken: cancellationToken));

            var dest = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition("""
                SELECT TOP 1 Id FROM PartInventory
                WHERE PartId = @PartId AND TenantId = @TenantId
                  AND LOWER(LTRIM(RTRIM(ISNULL(Location, '')))) = LOWER(LTRIM(RTRIM(@ToLocation)))
                """, new { PartId = request.PartId, TenantId = tenantId, ToLocation = toLocation },
                cancellationToken: cancellationToken));

            if (dest is null)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO PartInventory (TenantId, PartId, StockQuantity, Location)
                    VALUES (@TenantId, @PartId, @Qty, @ToLocation)
                    """, new { TenantId = tenantId, PartId = request.PartId, Qty = body.Quantity, ToLocation = toLocation },
                    cancellationToken: cancellationToken));
            }
            else
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE PartInventory SET StockQuantity = StockQuantity + @Qty, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                    new { Qty = body.Quantity, Id = dest }, cancellationToken: cancellationToken));
            }
        }

        var createdBy = currentUser.UserId?.ToString() ?? "system";
        await PartStockSql.InsertMovementAsync(
            connection, tenantId, request.PartId, "Transfer", body.Quantity,
            string.IsNullOrEmpty(fromLocation) ? null : fromLocation,
            toLocation, null, null, body.Notes, createdBy, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<int>> RecordPartUsageAsync(RecordPartUsageCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var wo = await connection.QuerySingleOrDefaultAsync<int>(
            new CommandDefinition(
                "SELECT VehicleId FROM WorkOrders WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = request.WorkOrderId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (wo == 0)
            throw new NotFoundException("WorkOrder", request.WorkOrderId);

        return await IssuePartAsync(
            new IssuePartCommand(request.PartId, new IssuePartDto(wo, request.Quantity, request.WorkOrderId, null)),
            cancellationToken);
    
    }

    public async Task<ApiResponse<PagedResult<MaintenanceRequestDto>>> ListMaintenanceRequestsAsync(ListMaintenanceRequestsQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();
        var offset = (Math.Max(1, request.Page) - 1) * request.PageSize;

        var clauses = new List<string> { "r.IsDeleted = 0", "r.TenantId = @TenantId" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Offset", offset);
        p.Add("PageSize", request.PageSize);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            clauses.Add("r.Status = @Status");
            p.Add("Status", request.Status.Trim());
        }
        if (request.VehicleId.HasValue)
        {
            clauses.Add("r.VehicleId = @VehicleId");
            p.Add("VehicleId", request.VehicleId.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            clauses.Add("(r.RequestNumber LIKE @Search OR r.Description LIKE @Search OR v.Name LIKE @Search)");
            p.Add("Search", $"%{request.Search.Trim()}%");
        }

        var where = string.Join(" AND ", clauses);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) {MaintenanceSql.RequestListFrom} WHERE {where}", p, cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<MaintenanceRequestDto>(new CommandDefinition($"""
            SELECT {MaintenanceSql.RequestListSelect}
            {MaintenanceSql.RequestListFrom}
            WHERE {where}
            ORDER BY r.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<MaintenanceRequestDto>>.SuccessResponse(new PagedResult<MaintenanceRequestDto>
        {
            Items = rows.ToList(),
            TotalCount = count,
            Page = request.Page,
            PageSize = request.PageSize
        });
    
    }

    public async Task<ApiResponse<MaintenanceRequestDto>> GetMaintenanceRequestByIdAsync(GetMaintenanceRequestByIdQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<MaintenanceRequestDto>(new CommandDefinition($"""
            SELECT {MaintenanceSql.RequestListSelect}
            {MaintenanceSql.RequestListFrom}
            WHERE r.Id = @Id AND r.TenantId = @TenantId AND r.IsDeleted = 0
            """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row is null)
            throw new NotFoundException("MaintenanceRequest", request.Id);

        return ApiResponse<MaintenanceRequestDto>.SuccessResponse(row);
    
    }




    private static (DateTime From, DateTime To) ResolveRange(DateTime? from, DateTime? to, string period)
    {
        if (from.HasValue && to.HasValue)
            return (from.Value.ToUniversalTime(), to.Value.ToUniversalTime());

        var now = DateTime.UtcNow;
        return period.ToLowerInvariant() switch
        {
            "today" => (now.Date, now.Date.AddDays(1)),
            "week" => (now.Date.AddDays(-(int)now.DayOfWeek), now.Date.AddDays(1)),
            "quarter" => (new DateTime(now.Year, ((now.Month - 1) / 3) * 3 + 1, 1), now.Date.AddDays(1)),
            "year" => (new DateTime(now.Year, 1, 1), now.Date.AddDays(1)),
            _ => (new DateTime(now.Year, now.Month, 1), now.Date.AddDays(1))
        };
    }

    private static async Task<IReadOnlyList<MaintenanceCostTrendPointDto>> GetCostTrendAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to,
        string granularity, int? branchId, CancellationToken ct)
    {
        var branchClause = branchId.HasValue ? " AND v.BranchId = @BranchId" : "";
        static string DateBucket(string column, string granularity) =>
            granularity.Equals("Month", StringComparison.OrdinalIgnoreCase)
                ? $"FORMAT({column}, 'yyyy-MM')"
                : granularity.Equals("Week", StringComparison.OrdinalIgnoreCase)
                    ? $"CONCAT(DATEPART(YEAR, {column}), '-W', DATEPART(WEEK, {column}))"
                    : $"FORMAT({column}, 'yyyy-MM-dd')";

        var maintBucket = DateBucket("m.MaintenanceDate", granularity);
        var woBucket = DateBucket("wo.CreatedAt", granularity);

        var rows = await connection.QueryAsync<(string Label, decimal Preventive, decimal Corrective, decimal Breakdown)>(
            new CommandDefinition($"""
                SELECT Label,
                    SUM(Preventive) AS Preventive,
                    SUM(Corrective) AS Corrective,
                    SUM(Breakdown) AS Breakdown
                FROM (
                    SELECT {maintBucket} AS Label,
                        CASE WHEN ISNULL(m.IsPreventive, 0) = 1
                            THEN ISNULL(m.Cost,0)+ISNULL(m.LaborCost,0)+ISNULL(m.PartsCost,0) ELSE 0 END AS Preventive,
                        CASE WHEN ISNULL(m.IsPreventive, 0) = 0 AND ISNULL(m.Category, N'') <> N'Breakdown'
                            THEN ISNULL(m.Cost,0)+ISNULL(m.LaborCost,0)+ISNULL(m.PartsCost,0) ELSE 0 END AS Corrective,
                        CASE WHEN m.Category = N'Breakdown'
                            THEN ISNULL(m.Cost,0)+ISNULL(m.LaborCost,0)+ISNULL(m.PartsCost,0) ELSE 0 END AS Breakdown
                    FROM Maintenance m
                    INNER JOIN Vehicles v ON v.Id = m.VehicleId
                    WHERE m.IsDeleted = 0 AND v.TenantId = @TenantId
                      AND m.MaintenanceDate >= @From AND m.MaintenanceDate < @To {branchClause}

                    UNION ALL

                    SELECT {woBucket} AS Label,
                        CASE WHEN wo.MaintenanceType = N'Preventive'
                            THEN ISNULL(wo.LaborCost,0)+ISNULL(wo.PartsCost,0) ELSE 0 END,
                        CASE WHEN wo.MaintenanceType = N'Corrective'
                            THEN ISNULL(wo.LaborCost,0)+ISNULL(wo.PartsCost,0) ELSE 0 END,
                        CASE WHEN wo.MaintenanceType = N'Emergency'
                            THEN ISNULL(wo.LaborCost,0)+ISNULL(wo.PartsCost,0) ELSE 0 END
                    FROM WorkOrders wo
                    INNER JOIN Vehicles v ON v.Id = wo.VehicleId AND v.IsDeleted = 0
                    WHERE wo.IsDeleted = 0 AND v.TenantId = @TenantId
                      AND wo.CreatedAt >= @From AND wo.CreatedAt < @To {branchClause}
                ) buckets
                GROUP BY Label
                ORDER BY Label
                """, new { TenantId = tenantId, From = from, To = to, BranchId = branchId }, cancellationToken: ct));

        return rows.Select(r => new MaintenanceCostTrendPointDto(r.Label, r.Preventive, r.Corrective, r.Breakdown)).ToList();
    }

    private static async Task<FuelMaintenanceSummaryDto?> GetFuelSummaryAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to,
        int? branchId, CancellationToken ct)
    {
        var branchClause = branchId.HasValue ? " AND v.BranchId = @BranchId" : "";

        var monthly = await connection.QueryAsync<(string Label, decimal Fuel, decimal Maint)>(new CommandDefinition($"""
            SELECT FORMAT(d.MonthDate, 'MMM') AS Label,
                ISNULL(SUM(f.TotalCost), 0) AS Fuel,
                ISNULL(SUM(m.Cost + ISNULL(m.LaborCost,0) + ISNULL(m.PartsCost,0)), 0) AS Maint
            FROM (
                SELECT DATEFROMPARTS(YEAR(@From), MONTH(@From), 1) AS MonthDate
                UNION ALL SELECT DATEADD(MONTH, 1, DATEFROMPARTS(YEAR(@From), MONTH(@From), 1))
                UNION ALL SELECT DATEADD(MONTH, 2, DATEFROMPARTS(YEAR(@From), MONTH(@From), 1))
                UNION ALL SELECT DATEADD(MONTH, 3, DATEFROMPARTS(YEAR(@From), MONTH(@From), 1))
                UNION ALL SELECT DATEADD(MONTH, 4, DATEFROMPARTS(YEAR(@From), MONTH(@From), 1))
                UNION ALL SELECT DATEADD(MONTH, 5, DATEFROMPARTS(YEAR(@From), MONTH(@From), 1))
            ) d
            LEFT JOIN FuelLogs f ON DATEFROMPARTS(YEAR(f.FuelDate), MONTH(f.FuelDate), 1) = d.MonthDate AND f.IsDeleted = 0
            LEFT JOIN Vehicles vf ON vf.Id = f.VehicleId AND vf.TenantId = @TenantId {branchClause.Replace("v.", "vf.")}
            LEFT JOIN Maintenance m ON DATEFROMPARTS(YEAR(m.MaintenanceDate), MONTH(m.MaintenanceDate), 1) = d.MonthDate AND m.IsDeleted = 0
            LEFT JOIN Vehicles vm ON vm.Id = m.VehicleId AND vm.TenantId = @TenantId {branchClause.Replace("v.", "vm.")}
            GROUP BY d.MonthDate, FORMAT(d.MonthDate, 'MMM')
            ORDER BY d.MonthDate
            """, new { TenantId = tenantId, From = from, To = to, BranchId = branchId }, cancellationToken: ct));

        var list = monthly.ToList();
        if (list.Count == 0) return null;

        var highCost = await connection.QueryAsync<HighCostVehicleDto>(new CommandDefinition($"""
            SELECT TOP 5 VehicleId, VehicleName, FuelCost, MaintenanceCost
            FROM (
                SELECT v.Id AS VehicleId, v.Name AS VehicleName,
                    ISNULL((SELECT SUM(f.TotalCost) FROM FuelLogs f WHERE f.VehicleId = v.Id AND f.IsDeleted = 0
                        AND f.FuelDate >= @From AND f.FuelDate < @To), 0) AS FuelCost,
                    ISNULL((SELECT SUM(m.Cost + ISNULL(m.LaborCost,0) + ISNULL(m.PartsCost,0)) FROM Maintenance m
                        WHERE m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.MaintenanceDate >= @From AND m.MaintenanceDate < @To), 0) AS MaintenanceCost
                FROM Vehicles v
                WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId {branchClause}
            ) ranked
            ORDER BY FuelCost + MaintenanceCost DESC
            """, new { TenantId = tenantId, From = from, To = to, BranchId = branchId }, cancellationToken: ct));

        return new FuelMaintenanceSummaryDto(
            list.Select(x => x.Label).ToList(),
            list.Select(x => x.Fuel).ToList(),
            list.Select(x => x.Maint).ToList(),
            highCost.ToList());
    }



    private sealed record PartIssueRow(decimal UnitCost, string PartName);

    private static async Task DeductStockAsync(
        System.Data.IDbConnection connection, int tenantId, int partId, int quantity, CancellationToken ct)
    {
        var remaining = quantity;
        var rows = await connection.QueryAsync<(int Id, int StockQuantity)>(new CommandDefinition("""
            SELECT Id, StockQuantity FROM PartInventory
            WHERE PartId = @PartId AND TenantId = @TenantId AND StockQuantity > 0
            ORDER BY Id
            """, new { PartId = partId, TenantId = tenantId }, cancellationToken: ct));

        foreach (var row in rows)
        {
            if (remaining <= 0) break;
            var deduct = Math.Min(remaining, row.StockQuantity);
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE PartInventory SET StockQuantity = StockQuantity - @Deduct, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { Deduct = deduct, row.Id }, cancellationToken: ct));
            remaining -= deduct;
        }

        if (remaining > 0)
            throw new ValidationException("Insufficient stock for this issue.");
    }

    public async Task<ApiResponse<MaintenanceReportDto>> GetMaintenanceReportAsync(GetMaintenanceReportQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var reportType = MaintenanceReportHelper.NormalizeReportType(request.ReportType);
        var (from, to) = MaintenanceReportHelper.ResolveDateRange(request.From, request.To);
        using var connection = dbFactory.CreateConnection();

        var report = reportType switch
        {
            "vehicle-maintenance" => await BuildVehicleMaintenanceAsync(connection, tenantId, from, to,
                request.VehicleId, request.BranchId, request.Status, cancellationToken),
            "service-due" => await BuildServiceDueAsync(connection, tenantId, request.VehicleId, request.BranchId,
                request.Status, cancellationToken),
            "overdue-maintenance" => await BuildOverdueAsync(connection, tenantId, request.VehicleId, request.BranchId,
                request.Status, cancellationToken),
            "workshop-performance" => await BuildWorkshopPerformanceAsync(connection, tenantId, from, to,
                request.Status, cancellationToken),
            "vendor-performance" => await BuildVendorPerformanceAsync(connection, tenantId, from, to, cancellationToken),
            "breakdown" => await BuildBreakdownAsync(connection, tenantId, from, to, request.VehicleId,
                request.BranchId, request.Status, cancellationToken),
            _ => await BuildCostAnalysisAsync(connection, tenantId, from, to, request.VehicleId, request.BranchId,
                cancellationToken)
        };

        return ApiResponse<MaintenanceReportDto>.SuccessResponse(report);
    
    }

    public async Task<ApiResponse<WorkshopVendorStatsDto>> GetWorkshopVendorStatsAsync(GetWorkshopVendorStatsQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var stats = await connection.QuerySingleAsync<WorkshopVendorStatsDto>(new CommandDefinition("""
            SELECT
                (SELECT COUNT(*) FROM Workshops WHERE TenantId = @TenantId AND IsDeleted = 0) AS TotalWorkshops,
                (SELECT COUNT(*) FROM Workshops WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1) AS ActiveWorkshops,
                (SELECT COUNT(*) FROM Vendors WHERE TenantId = @TenantId AND IsDeleted = 0) AS TotalVendors,
                (SELECT COUNT(*) FROM Vendors WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1 AND IsPreferred = 1) AS PreferredVendors
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<WorkshopVendorStatsDto>.SuccessResponse(stats);
    
    }

    public async Task<ApiResponse<IReadOnlyList<VehicleServiceHistoryItemDto>>> GetMaintenanceHistoryAsync(GetMaintenanceHistoryQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var clauses = new List<string>();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);

        if (request.VehicleId.HasValue) { clauses.Add("h.VehicleId = @VehicleId"); p.Add("VehicleId", request.VehicleId); }
        if (request.From.HasValue) { clauses.Add("h.ServiceDate >= @From"); p.Add("From", request.From); }
        if (request.To.HasValue) { clauses.Add("h.ServiceDate < @To"); p.Add("To", request.To); }
        if (!string.IsNullOrWhiteSpace(request.ServiceType))
        {
            clauses.Add("h.ServiceType LIKE @ServiceType");
            p.Add("ServiceType", $"%{request.ServiceType.Trim()}%");
        }
        if (request.MinCost.HasValue) { clauses.Add("h.TotalCost >= @MinCost"); p.Add("MinCost", request.MinCost); }
        if (request.MaxCost.HasValue) { clauses.Add("h.TotalCost <= @MaxCost"); p.Add("MaxCost", request.MaxCost); }

        var where = clauses.Count > 0 ? $"WHERE {string.Join(" AND ", clauses)}" : "";
        var rows = await connection.QueryAsync<ServiceHistoryRow>(new CommandDefinition($"""
            SELECT {MaintenanceSql.ServiceHistorySelect}
            FROM (
                {MaintenanceSql.ServiceHistoryUnion}
            ) h
            {where}
            ORDER BY h.ServiceDate DESC
            """, p, cancellationToken: cancellationToken));

        var items = rows.Select(MaintenanceServiceHistoryHelper.MapRow).ToList();
        return ApiResponse<IReadOnlyList<VehicleServiceHistoryItemDto>>.SuccessResponse(items);
    
    }

    public async Task<ApiResponse<IReadOnlyList<MaintenanceReportScheduleDto>>> ListMaintenanceReportSchedulesAsync(ListMaintenanceReportSchedulesQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<ScheduleRow>(new CommandDefinition("""
            SELECT Id, ReportType, FiltersJson, Frequency, Recipients, NextRunAt, LastRunAt, LastRunStatus, IsActive
            FROM MaintenanceReportSchedules
            WHERE TenantId = @TenantId AND IsDeleted = 0
            ORDER BY CreatedAt DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<MaintenanceReportScheduleDto>>.SuccessResponse(
            rows.Select(r => r.ToDto()).ToList());
    
    }

    public async Task<ApiResponse<int>> CreateMaintenanceReportScheduleAsync(CreateMaintenanceReportScheduleCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;
        var nextRun = MaintenanceReportHelper.ComputeNextRunAt(body.Frequency);
        var filtersJson = MaintenanceReportHelper.SerializeFilters(body.Filters);
        var createdBy = currentUser.UserId?.ToString() ?? "system";

        using var connection = dbFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO MaintenanceReportSchedules
                (TenantId, ReportType, FiltersJson, Frequency, Recipients, NextRunAt, LastRunStatus, CreatedBy)
            VALUES
                (@TenantId, @ReportType, @FiltersJson, @Frequency, @Recipients, @NextRunAt, N'Pending', @CreatedBy);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            ReportType = MaintenanceReportHelper.NormalizeReportType(body.ReportType),
            FiltersJson = filtersJson,
            body.Frequency,
            body.Recipients,
            NextRunAt = nextRun,
            CreatedBy = createdBy
        }, cancellationToken: cancellationToken));

        logger.LogInformation("Maintenance report schedule {ScheduleId} queued (email delivery stubbed)", id);
        return ApiResponse<int>.SuccessResponse(id);
    
    }

    public async Task<ApiResponse<bool>> UpdateMaintenanceReportScheduleAsync(UpdateMaintenanceReportScheduleCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM MaintenanceReportSchedules WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (exists == 0) throw new NotFoundException("MaintenanceReportSchedule", request.Id);

        var body = request.Body;
        var nextRun = body.Frequency is not null
            ? MaintenanceReportHelper.ComputeNextRunAt(body.Frequency)
            : (DateTime?)null;

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE MaintenanceReportSchedules SET
                Frequency = COALESCE(@Frequency, Frequency),
                Recipients = COALESCE(@Recipients, Recipients),
                FiltersJson = COALESCE(@FiltersJson, FiltersJson),
                IsActive = COALESCE(@IsActive, IsActive),
                NextRunAt = COALESCE(@NextRunAt, NextRunAt),
                UpdatedAt = GETUTCDATE(),
                UpdatedBy = @UpdatedBy
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            request.Id,
            TenantId = tenantId,
            body.Frequency,
            body.Recipients,
            FiltersJson = body.Filters is null ? null : MaintenanceReportHelper.SerializeFilters(body.Filters),
            body.IsActive,
            NextRunAt = nextRun,
            UpdatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<bool>> DeleteMaintenanceReportScheduleAsync(DeleteMaintenanceReportScheduleCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE MaintenanceReportSchedules SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (affected == 0) throw new NotFoundException("MaintenanceReportSchedule", request.Id);
        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<WorkOrderStatsDto>> GetWorkOrderStatsAsync(GetWorkOrderStatsQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var stats = await connection.QuerySingleAsync<WorkOrderStatsDto>(new CommandDefinition("""
            SELECT
                (SELECT COUNT(*) FROM WorkOrders wo
                    WHERE wo.TenantId = @TenantId AND wo.IsDeleted = 0
                      AND wo.Status IN (N'Draft', N'Open', N'Assigned')) AS [Open],
                (SELECT COUNT(*) FROM WorkOrders wo
                    WHERE wo.TenantId = @TenantId AND wo.IsDeleted = 0
                      AND wo.Status IN (N'InProgress', N'WaitingParts')) AS [InProgress],
                (SELECT COUNT(*) FROM WorkOrders wo
                    WHERE wo.TenantId = @TenantId AND wo.IsDeleted = 0
                      AND wo.Status IN (N'Completed', N'Closed')) AS [Completed],
                (SELECT COUNT(*) FROM WorkOrders wo
                    WHERE wo.TenantId = @TenantId AND wo.IsDeleted = 0
                      AND wo.Status = N'Cancelled') AS [Cancelled]
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<WorkOrderStatsDto>.SuccessResponse(stats);
    
    }

    public async Task<ApiResponse<PagedResult<WorkOrderListItemDto>>> ListWorkOrdersAsync(ListWorkOrdersQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();
        var offset = (Math.Max(1, request.Page) - 1) * request.PageSize;

        var clauses = new List<string> { "wo.IsDeleted = 0", "wo.TenantId = @TenantId" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Offset", offset);
        p.Add("PageSize", request.PageSize);

        if (!string.IsNullOrWhiteSpace(request.Statuses))
        {
            var statuses = request.Statuses
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (statuses.Length > 0)
            {
                clauses.Add("wo.Status IN @Statuses");
                p.Add("Statuses", statuses);
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Status))
        {
            clauses.Add("wo.Status = @Status");
            p.Add("Status", request.Status.Trim());
        }
        if (request.VehicleId.HasValue)
        {
            clauses.Add("wo.VehicleId = @VehicleId");
            p.Add("VehicleId", request.VehicleId.Value);
        }
        if (request.WorkshopId.HasValue)
        {
            clauses.Add("wo.WorkshopId = @WorkshopId");
            p.Add("WorkshopId", request.WorkshopId.Value);
        }
        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            clauses.Add("wo.Priority = @Priority");
            p.Add("Priority", request.Priority.Trim());
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            clauses.Add("(wo.WorkOrderNumber LIKE @Search OR v.Name LIKE @Search OR wo.ServiceTypeName LIKE @Search)");
            p.Add("Search", $"%{request.Search.Trim()}%");
        }

        var where = string.Join(" AND ", clauses);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) {MaintenanceSql.WorkOrderListFrom} WHERE {where}", p, cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<WorkOrderListItemDto>(new CommandDefinition($"""
            SELECT {MaintenanceSql.WorkOrderListSelect}
            {MaintenanceSql.WorkOrderListFrom}
            WHERE {where}
            ORDER BY wo.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<WorkOrderListItemDto>>.SuccessResponse(new PagedResult<WorkOrderListItemDto>
        {
            Items = rows.ToList(),
            TotalCount = count,
            Page = request.Page,
            PageSize = request.PageSize
        });
    
    }

    public async Task<ApiResponse<WorkOrderDetailDto>> GetWorkOrderByIdAsync(GetWorkOrderByIdQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<WorkOrderDetailRow>(new CommandDefinition("""
            SELECT wo.Id, wo.WorkOrderNumber, wo.RequestId, wo.VehicleId, v.Name AS VehicleName,
                   v.RegistrationNumber AS VehicleRegistration,
                   r.DriverId, d.FullName AS DriverName,
                   COALESCE(wo.BranchId, v.BranchId) AS BranchId, b.Name AS BranchName,
                   wo.WorkshopId, w.Name AS WorkshopName, wo.TechnicianId, t.FullName AS TechnicianName,
                   wo.ServiceTypeId, wo.ServiceTypeName, wo.MaintenanceType,
                   wo.StartDate, wo.EstimatedCompletionDate, wo.CompletedAt,
                   wo.LaborCost, wo.PartsCost, wo.TotalCost,
                   wo.EstimatedLaborCost, wo.EstimatedPartsCost,
                   wo.Status, wo.Priority, wo.Notes, wo.TechnicianNotes, wo.CreatedAt
            FROM WorkOrders wo
            INNER JOIN Vehicles v ON v.Id = wo.VehicleId
            LEFT JOIN MaintenanceRequests r ON r.Id = wo.RequestId
            LEFT JOIN Drivers d ON d.Id = r.DriverId
            LEFT JOIN Branches b ON b.Id = COALESCE(wo.BranchId, v.BranchId)
            LEFT JOIN Workshops w ON w.Id = wo.WorkshopId
            LEFT JOIN Technicians t ON t.Id = wo.TechnicianId
            WHERE wo.Id = @Id AND wo.TenantId = @TenantId AND wo.IsDeleted = 0
            """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row is null)
            throw new NotFoundException("WorkOrder", request.Id);

        var parts = await connection.QueryAsync<WorkOrderPartUsageDto>(new CommandDefinition("""
            SELECT pu.PartId, p.PartName, pu.Quantity, pu.UnitCost,
                   (pu.Quantity * pu.UnitCost) AS TotalCost
            FROM PartUsage pu
            INNER JOIN Parts p ON p.Id = pu.PartId AND p.IsDeleted = 0
            WHERE pu.WorkOrderId = @Id AND pu.TenantId = @TenantId
            ORDER BY pu.UsedAt DESC
            """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        var detail = new WorkOrderDetailDto(
            row.Id, row.WorkOrderNumber, row.RequestId, row.VehicleId, row.VehicleName,
            row.VehicleRegistration, row.DriverId, row.DriverName, row.BranchId, row.BranchName,
            row.WorkshopId, row.WorkshopName, row.TechnicianId, row.TechnicianName,
            row.ServiceTypeId, row.ServiceTypeName, row.MaintenanceType,
            row.StartDate, row.EstimatedCompletionDate, row.CompletedAt,
            row.LaborCost, row.PartsCost, row.TotalCost,
            row.EstimatedLaborCost, row.EstimatedPartsCost,
            row.Status, row.Priority, row.Notes, row.TechnicianNotes, row.CreatedAt,
            parts.ToList());
        return ApiResponse<WorkOrderDetailDto>.SuccessResponse(detail);
    
    }

    public async Task<ApiResponse<IReadOnlyList<TechnicianListItemDto>>> ListTechniciansAsync(ListTechniciansQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var clauses = new List<string> { "t.IsDeleted = 0", "t.IsActive = 1", "t.TenantId = @TenantId" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);

        if (request.WorkshopId.HasValue)
        {
            clauses.Add("t.WorkshopId = @WorkshopId");
            p.Add("WorkshopId", request.WorkshopId.Value);
        }

        var where = string.Join(" AND ", clauses);
        var rows = await connection.QueryAsync<TechnicianListItemDto>(new CommandDefinition($"""
            SELECT t.Id, t.FullName, t.WorkshopId, w.Name AS WorkshopName
            FROM Technicians t
            LEFT JOIN Workshops w ON w.Id = t.WorkshopId
            WHERE {where}
            ORDER BY t.FullName
            """, p, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<TechnicianListItemDto>>.SuccessResponse(rows.ToList());
    
    }

    public async Task<ApiResponse<IReadOnlyList<MaintenanceScheduleListItemDto>>> ListMaintenanceSchedulesAsync(ListMaintenanceSchedulesQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var clauses = new List<string> { "s.IsDeleted = 0", "s.TenantId = @TenantId", "s.IsActive = 1" };
        if (request.VehicleId.HasValue) clauses.Add("s.VehicleId = @VehicleId");
        if (!string.IsNullOrWhiteSpace(request.Search))
            clauses.Add("(v.Name LIKE @Search OR v.RegistrationNumber LIKE @Search OR s.ServiceTypeName LIKE @Search)");

        var sql = $"""
            SELECT {MaintenanceSql.ScheduleListSelect}
            {MaintenanceSql.ScheduleListFrom}
            WHERE {string.Join(" AND ", clauses)}
            ORDER BY s.NextDueDate ASC, s.ServiceTypeName ASC
            """;

        var rows = await connection.QueryAsync<ScheduleListRow>(new CommandDefinition(
            sql,
            new
            {
                TenantId = tenantId,
                request.VehicleId,
                Search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search.Trim()}%"
            },
            cancellationToken: cancellationToken));

        var items = rows.Select(MapListItem).ToList();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var filter = request.Status.Trim();
            items = items.Where(i => i.Status.Equals(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return ApiResponse<IReadOnlyList<MaintenanceScheduleListItemDto>>.SuccessResponse(items);
    
    }

    public async Task<ApiResponse<IReadOnlyList<MaintenanceScheduleCalendarItemDto>>> GetMaintenanceScheduleCalendarAsync(GetMaintenanceScheduleCalendarQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var from = request.From.Date;
        var to = request.To.Date;
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<ScheduleListRow>(new CommandDefinition(
            $"""
            SELECT {MaintenanceSql.ScheduleListSelect}
            {MaintenanceSql.ScheduleListFrom}
            WHERE s.IsDeleted = 0 AND s.TenantId = @TenantId AND s.IsActive = 1
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        var today = DateTime.UtcNow.Date;
        var items = new List<MaintenanceScheduleCalendarItemDto>();

        foreach (var row in rows)
        {
            var status = MaintenanceScheduleHelper.ComputeStatus(
                row.IntervalType, row.DueDate, row.NextServiceMileage, row.NextDueEngineHours,
                row.CurrentMileage, row.CurrentEngineHours, today);

            var displayDate = ResolveCalendarDate(row, status, today);
            if (!displayDate.HasValue || displayDate.Value < from || displayDate.Value > to)
                continue;

            items.Add(new MaintenanceScheduleCalendarItemDto(
                row.Id, row.VehicleId, row.VehicleName ?? $"Vehicle #{row.VehicleId}",
                row.ServiceTypeName, displayDate, status, row.IntervalType,
                row.NextServiceMileage, row.NextDueEngineHours));
        }

        return ApiResponse<IReadOnlyList<MaintenanceScheduleCalendarItemDto>>.SuccessResponse(
            items.OrderBy(i => i.DueDate).ThenBy(i => i.VehicleName).ToList());
    
    }

    public async Task<ApiResponse<IReadOnlyList<MaintenanceSchedulableVehicleDto>>> ListSchedulableMaintenanceVehiclesAsync(ListSchedulableMaintenanceVehiclesQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var rows = await connection.QueryAsync<MaintenanceSchedulableVehicleDto>(new CommandDefinition(
            """
            SELECT
                v.Id AS VehicleId,
                v.Name,
                v.RegistrationNumber,
                v.VehicleCode,
                v.Status
            FROM Vehicles v
            WHERE v.TenantId = @TenantId
              AND v.IsDeleted = 0
              AND v.Status NOT IN (@Retired, @Draft)
            ORDER BY v.Name
            """,
            new
            {
                TenantId = tenantId,
                Retired = (int)VehicleStatus.Retired,
                Draft = (int)VehicleStatus.Draft
            },
            cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<MaintenanceSchedulableVehicleDto>>.SuccessResponse(rows.ToList());
    
    }

    public async Task<ApiResponse<bool>> RescheduleMaintenanceScheduleAsync(RescheduleMaintenanceScheduleCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var existing = await connection.QuerySingleOrDefaultAsync<ScheduleListRow>(new CommandDefinition(
            $"""
            SELECT {MaintenanceSql.ScheduleListSelect}
            {MaintenanceSql.ScheduleListFrom}
            WHERE s.Id = @Id AND s.TenantId = @TenantId AND s.IsDeleted = 0
            """,
            new { request.Id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (existing is null)
            throw new NotFoundException("MaintenanceSchedule", request.Id);

        var body = request.Body;
        var intervalType = body.IntervalType ?? existing.IntervalType;
        var intervalValue = body.IntervalValue ?? existing.IntervalValue;
        var lastDate = body.LastServiceDate ?? existing.LastServiceDate;
        var lastMileage = body.LastServiceMileage ?? existing.LastServiceMileage;
        var lastEngineHours = body.LastServiceEngineHours ?? existing.LastServiceEngineHours;

        var (nextMileage, nextEngineHours, nextDate) = MaintenanceScheduleHelper.RecomputeNextDue(
            intervalType, intervalValue, lastDate, lastMileage, lastEngineHours);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE VehicleMaintenanceSchedules SET
                IntervalType = @IntervalType,
                IntervalValue = @IntervalValue,
                LastServiceDate = @LastServiceDate,
                LastServiceMileage = @LastServiceMileage,
                LastServiceEngineHours = @LastServiceEngineHours,
                NextDueDate = @NextDueDate,
                NextDueMileage = @NextDueMileage,
                NextDueEngineHours = @NextDueEngineHours,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            request.Id,
            TenantId = tenantId,
            IntervalType = intervalType,
            IntervalValue = intervalValue,
            LastServiceDate = lastDate,
            LastServiceMileage = lastMileage,
            LastServiceEngineHours = lastEngineHours,
            NextDueDate = nextDate,
            NextDueMileage = nextMileage,
            NextDueEngineHours = nextEngineHours
        }, cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<bool>> UpdateMaintenanceScheduleAsync(UpdateMaintenanceScheduleCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var existing = await connection.QuerySingleOrDefaultAsync<(
            string ServiceTypeName, int? ServiceTypeId, string IntervalType, int IntervalValue,
            string Priority, bool IsActive,
            DateTime? LastServiceDate, decimal? LastServiceMileage, decimal? LastServiceEngineHours)>(
            new CommandDefinition("""
                SELECT ServiceTypeName, ServiceTypeId, IntervalType, IntervalValue, Priority, IsActive,
                       LastServiceDate, LastServiceMileage, LastServiceEngineHours
                FROM VehicleMaintenanceSchedules
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
                """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (string.IsNullOrEmpty(existing.IntervalType))
            throw new NotFoundException("MaintenanceSchedule", request.Id);

        var body = request.Body;
        var intervalType = body.IntervalType ?? existing.IntervalType;
        var intervalValue = body.IntervalValue ?? existing.IntervalValue;

        var (nextMileage, nextEngineHours, nextDate) = MaintenanceScheduleHelper.RecomputeNextDue(
            intervalType, intervalValue,
            existing.LastServiceDate, existing.LastServiceMileage, existing.LastServiceEngineHours);

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE VehicleMaintenanceSchedules SET
                ServiceTypeName = COALESCE(@ServiceTypeName, ServiceTypeName),
                ServiceTypeId = COALESCE(@ServiceTypeId, ServiceTypeId),
                IntervalType = @IntervalType,
                IntervalValue = @IntervalValue,
                Priority = COALESCE(@Priority, Priority),
                IsActive = COALESCE(@IsActive, IsActive),
                NextDueDate = @NextDueDate,
                NextDueMileage = @NextDueMileage,
                NextDueEngineHours = @NextDueEngineHours,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
        {
            request.Id,
            TenantId = tenantId,
            body.ServiceTypeName,
            body.ServiceTypeId,
            IntervalType = intervalType,
            IntervalValue = intervalValue,
            body.Priority,
            body.IsActive,
            NextDueDate = nextDate,
            NextDueMileage = nextMileage,
            NextDueEngineHours = nextEngineHours
        }, cancellationToken: cancellationToken));

        if (rows == 0)
            throw new NotFoundException("MaintenanceSchedule", request.Id);

        return ApiResponse<bool>.SuccessResponse(true);
    
    }

    public async Task<ApiResponse<int>> CreateWorkOrderFromScheduleAsync(CreateWorkOrderFromScheduleCommand request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var schedule = await connection.QuerySingleOrDefaultAsync<(
            int VehicleId, int? ServiceTypeId, string ServiceTypeName, string Priority)>(
            new CommandDefinition("""
                SELECT VehicleId, ServiceTypeId, ServiceTypeName, Priority
                FROM VehicleMaintenanceSchedules
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
                """, new { Id = request.ScheduleId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (string.IsNullOrEmpty(schedule.ServiceTypeName))
            throw new NotFoundException("MaintenanceSchedule", request.ScheduleId);

        var seq = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) + 1 FROM WorkOrders WHERE TenantId = @TenantId",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO WorkOrders
                (TenantId, WorkOrderNumber, ScheduleId, VehicleId, ServiceTypeId, ServiceTypeName,
                 Priority, Notes, Status, CreatedBy, CreatedAt)
            VALUES
                (@TenantId, @WorkOrderNumber, @ScheduleId, @VehicleId, @ServiceTypeId, @ServiceTypeName,
                 @Priority, @Notes, N'Open', @CreatedBy, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            WorkOrderNumber = $"WO-{seq:D5}",
            ScheduleId = request.ScheduleId,
            schedule.VehicleId,
            schedule.ServiceTypeId,
            schedule.ServiceTypeName,
            schedule.Priority,
            Notes = $"Created from preventive schedule #{request.ScheduleId}",
            CreatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id);
    
    }

    public async Task<ApiResponse<PartsInventoryStatsDto>> GetPartsInventoryStatsAsync(GetPartsInventoryStatsQuery request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var stats = await connection.QuerySingleAsync<PartsInventoryStatsDto>(new CommandDefinition("""
            SELECT
                COUNT(*) AS TotalParts,
                SUM(CASE WHEN ISNULL(inv.StockQuantity, 0) > 0
                          AND ISNULL(inv.StockQuantity, 0) < p.MinStockLevel THEN 1 ELSE 0 END) AS LowStock,
                SUM(CASE WHEN ISNULL(inv.StockQuantity, 0) <= 0 THEN 1 ELSE 0 END) AS OutOfStock,
                ISNULL(SUM(ISNULL(inv.StockQuantity, 0) * p.UnitCost), 0) AS InventoryValue
            FROM Parts p
            LEFT JOIN (
                SELECT PartId, SUM(StockQuantity) AS StockQuantity
                FROM PartInventory
                WHERE TenantId = @TenantId
                GROUP BY PartId
            ) inv ON inv.PartId = p.Id
            WHERE p.IsDeleted = 0 AND p.TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<PartsInventoryStatsDto>.SuccessResponse(stats);
    
    }


    private static async Task<MaintenanceReportDto> BuildVehicleMaintenanceAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to,
        int? vehicleId, int? branchId, string? status, CancellationToken ct)
    {
        var columns = new[]
        {
            new MaintenanceReportColumnDto("plate", "Plate", "text"),
            new MaintenanceReportColumnDto("branch", "Branch", "text"),
            new MaintenanceReportColumnDto("serviceCount", "Services", "number"),
            new MaintenanceReportColumnDto("totalCost", "Total Cost", "currency"),
            new MaintenanceReportColumnDto("lastServiceDate", "Last Service", "date"),
            new MaintenanceReportColumnDto("status", "Status", "text")
        };

        var clauses = new List<string> { "v.IsDeleted = 0", "v.TenantId = @TenantId" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("From", from);
        p.Add("To", to);
        MaintenanceReportSql.ApplyVehicleBranchFilters(p, vehicleId, branchId, "v", null, clauses);
        var where = MaintenanceReportSql.BuildWhere(clauses);

        var raw = await connection.QueryAsync<dynamic>(new CommandDefinition($"""
            SELECT v.Id, v.Name AS Label, v.RegistrationNumber AS Plate, ISNULL(b.Name, N'') AS BranchName,
                COUNT(m.Id) AS ServiceCount,
                ISNULL(SUM(m.Cost + ISNULL(m.LaborCost,0) + ISNULL(m.PartsCost,0)), 0) AS TotalCost,
                MAX(m.MaintenanceDate) AS LastServiceDate,
                CASE WHEN EXISTS (
                    SELECT 1 FROM WorkOrders wo
                    WHERE wo.VehicleId = v.Id AND wo.IsDeleted = 0
                      AND wo.Status IN (N'Open', N'Assigned', N'InProgress', N'WaitingParts')
                ) THEN N'Open' ELSE N'Completed' END AS RowStatus
            FROM Vehicles v
            LEFT JOIN Branches b ON b.Id = v.BranchId
            LEFT JOIN Maintenance m ON m.VehicleId = v.Id AND m.IsDeleted = 0
                AND m.MaintenanceDate >= @From AND m.MaintenanceDate < @To
            {where}
            GROUP BY v.Id, v.Name, v.RegistrationNumber, b.Name
            ORDER BY TotalCost DESC
            """, p, cancellationToken: ct));

        var rows = raw.Select(r =>
        {
            string rowStatus = r.RowStatus;
            return MaintenanceReportHelper.Row(
                ((int)r.Id).ToString(), (string)r.Label, (int)r.ServiceCount, (decimal)r.TotalCost,
                ("plate", (object?)r.Plate),
                ("branch", (object?)r.BranchName),
                ("serviceCount", (object?)(int)r.ServiceCount),
                ("totalCost", (object?)(decimal)r.TotalCost),
                ("lastServiceDate", (object?)r.LastServiceDate),
                ("status", (object?)rowStatus));
        }).Where(r => MaintenanceReportHelper.MatchesStatusFilter(status, r.Fields.GetValueOrDefault("status")?.ToString() ?? ""))
          .ToList();

        return new MaintenanceReportDto("vehicle-maintenance", MaintenanceReportHelper.TitleFor("vehicle-maintenance"),
            columns, rows, rows.Sum(r => r.TotalCost),
            new Dictionary<string, object?> { ["rowCount"] = rows.Count });
    }

    private static async Task<MaintenanceReportDto> BuildServiceDueAsync(
        System.Data.IDbConnection connection, int tenantId, int? vehicleId, int? branchId, string? status, CancellationToken ct)
    {
        var columns = new[]
        {
            new MaintenanceReportColumnDto("vehicle", "Vehicle", "text"),
            new MaintenanceReportColumnDto("serviceType", "Service Type", "text"),
            new MaintenanceReportColumnDto("dueDate", "Due Date", "date"),
            new MaintenanceReportColumnDto("dueMileage", "Due Mileage", "number"),
            new MaintenanceReportColumnDto("daysUntilDue", "Days Until Due", "number"),
            new MaintenanceReportColumnDto("status", "Status", "text")
        };

        var clauses = new List<string> { "s.IsDeleted = 0", "s.IsActive = 1", "v.TenantId = @TenantId", "v.IsDeleted = 0" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        MaintenanceReportSql.ApplyVehicleBranchFilters(p, vehicleId, branchId, "v", null, clauses);
        var where = MaintenanceReportSql.BuildWhere(clauses);

        var raw = await connection.QueryAsync<dynamic>(new CommandDefinition($"""
            SELECT s.Id, v.Name AS VehicleName, COALESCE(st.Name, s.ServiceTypeName) AS ServiceType,
                s.NextDueDate, s.NextDueMileage, v.CurrentMileage,
                DATEDIFF(day, GETUTCDATE(), s.NextDueDate) AS DaysUntilDue
            FROM VehicleMaintenanceSchedules s
            INNER JOIN Vehicles v ON v.Id = s.VehicleId
            LEFT JOIN ServiceTypes st ON st.Id = s.ServiceTypeId
            {where}
            """, p, cancellationToken: ct));

        var rows = new List<MaintenanceReportRowDto>();
        foreach (var r in raw)
        {
            DateTime? dueDate = r.NextDueDate;
            var scheduleStatus = dueDate.HasValue
                ? MaintenanceScheduleHelper.DateStatus(dueDate.Value.Date, DateTime.UtcNow.Date)
                : MaintenanceScheduleHelper.StatusUpcoming;

            if (status?.Equals("Scheduled", StringComparison.OrdinalIgnoreCase) == true &&
                scheduleStatus != MaintenanceScheduleHelper.StatusUpcoming)
                continue;
            if (!MaintenanceReportHelper.MatchesStatusFilter(status, scheduleStatus))
                continue;

            rows.Add(MaintenanceReportHelper.Row(
                ((int)r.Id).ToString(), (string)r.VehicleName, 1, 0m,
                ("vehicle", (object?)r.VehicleName),
                ("serviceType", (object?)r.ServiceType),
                ("dueDate", (object?)r.NextDueDate),
                ("dueMileage", (object?)r.NextDueMileage),
                ("daysUntilDue", (object?)r.DaysUntilDue),
                ("status", scheduleStatus)));
        }

        return new MaintenanceReportDto("service-due", MaintenanceReportHelper.TitleFor("service-due"),
            columns, rows, 0,
            new Dictionary<string, object?> { ["rowCount"] = rows.Count });
    }

    private static async Task<MaintenanceReportDto> BuildOverdueAsync(
        System.Data.IDbConnection connection, int tenantId, int? vehicleId, int? branchId, string? status, CancellationToken ct)
    {
        var columns = new[]
        {
            new MaintenanceReportColumnDto("vehicle", "Vehicle", "text"),
            new MaintenanceReportColumnDto("branch", "Branch", "text"),
            new MaintenanceReportColumnDto("overdueItem", "Overdue Item", "text"),
            new MaintenanceReportColumnDto("dueDate", "Due Date", "date"),
            new MaintenanceReportColumnDto("daysOverdue", "Days Overdue", "number"),
            new MaintenanceReportColumnDto("status", "Status", "text")
        };

        var clauses = new List<string>
        {
            "v.IsDeleted = 0", "v.TenantId = @TenantId",
            "(s.NextDueDate < GETUTCDATE() OR m.NextDueDate < GETUTCDATE())"
        };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        MaintenanceReportSql.ApplyVehicleBranchFilters(p, vehicleId, branchId, "v", null, clauses);
        var where = MaintenanceReportSql.BuildWhere(clauses);

        var raw = await connection.QueryAsync<dynamic>(new CommandDefinition($"""
            SELECT v.Id, v.Name AS VehicleName, ISNULL(b.Name, N'') AS BranchName,
                COALESCE(st.Name, N'Maintenance') AS OverdueItem,
                COALESCE(s.NextDueDate, m.NextDueDate) AS DueDate,
                DATEDIFF(day, COALESCE(s.NextDueDate, m.NextDueDate), GETUTCDATE()) AS DaysOverdue
            FROM Vehicles v
            LEFT JOIN Branches b ON b.Id = v.BranchId
            LEFT JOIN VehicleMaintenanceSchedules s ON s.VehicleId = v.Id AND s.IsDeleted = 0 AND s.IsActive = 1
            LEFT JOIN ServiceTypes st ON st.Id = s.ServiceTypeId
            LEFT JOIN Maintenance m ON m.VehicleId = v.Id AND m.IsDeleted = 0 AND m.Status IN (1,2)
            {where}
            """, p, cancellationToken: ct));

        var rows = raw.Select(r => MaintenanceReportHelper.Row(
            $"{r.Id}-{r.OverdueItem}", (string)r.VehicleName, 1, 0m,
            ("vehicle", (object?)r.VehicleName),
            ("branch", (object?)r.BranchName),
            ("overdueItem", (object?)r.OverdueItem),
            ("dueDate", (object?)r.DueDate),
            ("daysOverdue", (object?)r.DaysOverdue),
            ("status", MaintenanceScheduleHelper.StatusOverdue)))
            .Where(r => MaintenanceReportHelper.MatchesStatusFilter(status, MaintenanceScheduleHelper.StatusOverdue)
                || MaintenanceReportHelper.MatchesStatusFilter(status, "Overdue"))
            .ToList();

        return new MaintenanceReportDto("overdue-maintenance", MaintenanceReportHelper.TitleFor("overdue-maintenance"),
            columns, rows, 0,
            new Dictionary<string, object?> { ["rowCount"] = rows.Count });
    }

    private static async Task<MaintenanceReportDto> BuildWorkshopPerformanceAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to, string? status, CancellationToken ct)
    {
        var columns = new[]
        {
            new MaintenanceReportColumnDto("workshop", "Workshop", "text"),
            new MaintenanceReportColumnDto("completedCount", "Completed", "number"),
            new MaintenanceReportColumnDto("openCount", "Open", "number"),
            new MaintenanceReportColumnDto("avgDays", "Avg Days", "number"),
            new MaintenanceReportColumnDto("totalCost", "Total Cost", "currency"),
            new MaintenanceReportColumnDto("rating", "Rating", "number")
        };

        var raw = await connection.QueryAsync<dynamic>(new CommandDefinition("""
            SELECT w.Id, w.Name AS WorkshopName, w.Rating,
                SUM(CASE WHEN wo.Status = N'Completed' THEN 1 ELSE 0 END) AS CompletedCount,
                SUM(CASE WHEN wo.Status IN (N'Open', N'Assigned', N'InProgress', N'WaitingParts') THEN 1 ELSE 0 END) AS OpenCount,
                AVG(CASE WHEN wo.CompletedAt IS NOT NULL THEN CAST(DATEDIFF(day, wo.CreatedAt, wo.CompletedAt) AS FLOAT) END) AS AvgDays,
                ISNULL(SUM(wo.LaborCost + wo.PartsCost), 0) AS TotalCost
            FROM Workshops w
            LEFT JOIN WorkOrders wo ON wo.WorkshopId = w.Id AND wo.IsDeleted = 0
                AND wo.CreatedAt >= @From AND wo.CreatedAt < @To
            WHERE w.TenantId = @TenantId AND w.IsDeleted = 0
            GROUP BY w.Id, w.Name, w.Rating
            ORDER BY TotalCost DESC
            """, new { TenantId = tenantId, From = from, To = to }, cancellationToken: ct));

        var rows = raw.Select(r =>
        {
            var open = (int)(r.OpenCount ?? 0);
            var completed = (int)(r.CompletedCount ?? 0);
            var rowStatus = open > 0 ? "Open" : "Completed";
            return (Row: MaintenanceReportHelper.Row(
                ((int)r.Id).ToString(), (string)r.WorkshopName, completed + open, (decimal)r.TotalCost,
                ("workshop", (object?)r.WorkshopName),
                ("completedCount", (object?)completed),
                ("openCount", (object?)open),
                ("avgDays", (object?)r.AvgDays),
                ("totalCost", (object?)(decimal)r.TotalCost),
                ("rating", (object?)r.Rating)),
                Status: rowStatus);
        }).Where(x => MaintenanceReportHelper.MatchesStatusFilter(status, x.Status))
          .Select(x => x.Row).ToList();

        return new MaintenanceReportDto("workshop-performance", MaintenanceReportHelper.TitleFor("workshop-performance"),
            columns, rows, rows.Sum(r => r.TotalCost),
            new Dictionary<string, object?> { ["rowCount"] = rows.Count });
    }

    private static async Task<MaintenanceReportDto> BuildVendorPerformanceAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to, CancellationToken ct)
    {
        var columns = new[]
        {
            new MaintenanceReportColumnDto("vendor", "Vendor", "text"),
            new MaintenanceReportColumnDto("category", "Category", "text"),
            new MaintenanceReportColumnDto("preferred", "Preferred", "text"),
            new MaintenanceReportColumnDto("partsSpend", "Parts Spend", "currency"),
            new MaintenanceReportColumnDto("usageCount", "Usage Count", "number"),
            new MaintenanceReportColumnDto("rating", "Rating", "number")
        };

        var raw = await connection.QueryAsync<dynamic>(new CommandDefinition("""
            SELECT v.Id, v.Name AS VendorName, v.Category, v.IsPreferred, v.Rating,
                (SELECT COUNT(*) FROM PartUsage pu
                    INNER JOIN Parts p ON p.Id = pu.PartId
                    WHERE p.Supplier = v.Name AND pu.TenantId = @TenantId
                      AND pu.UsedAt >= @From AND pu.UsedAt < @To) AS UsageCount,
                (SELECT ISNULL(SUM(pu.Quantity * pu.UnitCost), 0) FROM PartUsage pu
                    INNER JOIN Parts p ON p.Id = pu.PartId
                    WHERE p.Supplier = v.Name AND pu.TenantId = @TenantId
                      AND pu.UsedAt >= @From AND pu.UsedAt < @To) AS PartsSpend
            FROM Vendors v
            WHERE v.TenantId = @TenantId AND v.IsDeleted = 0
            ORDER BY PartsSpend DESC
            """, new { TenantId = tenantId, From = from, To = to }, cancellationToken: ct));

        var rows = raw.Select(r => MaintenanceReportHelper.Row(
            ((int)r.Id).ToString(), (string)r.VendorName, (int)r.UsageCount, (decimal)r.PartsSpend,
            ("vendor", (object?)r.VendorName),
            ("category", (object?)r.Category),
            ("preferred", (object?)((bool)r.IsPreferred ? "Yes" : "No")),
            ("partsSpend", (object?)(decimal)r.PartsSpend),
            ("usageCount", (object?)(int)r.UsageCount),
            ("rating", (object?)r.Rating))).ToList();

        return new MaintenanceReportDto("vendor-performance", MaintenanceReportHelper.TitleFor("vendor-performance"),
            columns, rows, rows.Sum(r => r.TotalCost),
            new Dictionary<string, object?> { ["rowCount"] = rows.Count });
    }

    private static async Task<MaintenanceReportDto> BuildCostAnalysisAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to,
        int? vehicleId, int? branchId, CancellationToken ct)
    {
        var columns = new[]
        {
            new MaintenanceReportColumnDto("category", "Category", "text"),
            new MaintenanceReportColumnDto("count", "Count", "number"),
            new MaintenanceReportColumnDto("maintenanceCost", "Maintenance Cost", "currency"),
            new MaintenanceReportColumnDto("workOrderCost", "Work Order Cost", "currency"),
            new MaintenanceReportColumnDto("totalCost", "Total Cost", "currency")
        };

        var clauses = new List<string> { "v.IsDeleted = 0", "v.TenantId = @TenantId", "m.IsDeleted = 0",
            "m.MaintenanceDate >= @From", "m.MaintenanceDate < @To" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("From", from);
        p.Add("To", to);
        MaintenanceReportSql.ApplyVehicleBranchFilters(p, vehicleId, branchId, "v", null, clauses);
        var where = MaintenanceReportSql.BuildWhere(clauses);

        var maint = await connection.QueryAsync<dynamic>(new CommandDefinition($"""
            SELECT ISNULL(m.Category, N'Other') AS Category, COUNT(*) AS Cnt,
                ISNULL(SUM(m.Cost + ISNULL(m.LaborCost,0) + ISNULL(m.PartsCost,0)), 0) AS MaintenanceCost
            FROM Maintenance m
            INNER JOIN Vehicles v ON v.Id = m.VehicleId
            {where}
            GROUP BY m.Category
            """, p, cancellationToken: ct));

        var woClauses = new List<string> { "wo.IsDeleted = 0", "wo.TenantId = @TenantId",
            "wo.CreatedAt >= @From", "wo.CreatedAt < @To", "wo.Status = N'Completed'" };
        if (vehicleId.HasValue) woClauses.Add("wo.VehicleId = @VehicleId");
        if (branchId.HasValue)
        {
            woClauses.Add("EXISTS (SELECT 1 FROM Vehicles v2 WHERE v2.Id = wo.VehicleId AND v2.BranchId = @BranchId)");
        }
        var woWhere = MaintenanceReportSql.BuildWhere(woClauses);

        var woTotal = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition($"""
            SELECT ISNULL(SUM(wo.LaborCost + wo.PartsCost), 0)
            FROM WorkOrders wo {woWhere}
            """, p, cancellationToken: ct));

        var rows = maint.Select(r => MaintenanceReportHelper.Row(
            (string)r.Category, (string)r.Category, (int)r.Cnt, (decimal)r.MaintenanceCost,
            ("category", (object?)r.Category),
            ("count", (object?)(int)r.Cnt),
            ("maintenanceCost", (object?)(decimal)r.MaintenanceCost),
            ("workOrderCost", (object?)0m),
            ("totalCost", (object?)(decimal)r.MaintenanceCost))).ToList();

        if (woTotal > 0)
        {
            rows.Add(MaintenanceReportHelper.Row("work-orders", "Work Orders", 1, woTotal,
                ("category", "Work Orders"),
                ("count", 1),
                ("maintenanceCost", 0m),
                ("workOrderCost", woTotal),
                ("totalCost", woTotal)));
        }

        var total = rows.Sum(r => r.TotalCost);
        return new MaintenanceReportDto("cost-analysis", MaintenanceReportHelper.TitleFor("cost-analysis"),
            columns, rows, total,
            new Dictionary<string, object?> { ["rowCount"] = rows.Count, ["workOrderCost"] = woTotal });
    }

    private static async Task<MaintenanceReportDto> BuildBreakdownAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to,
        int? vehicleId, int? branchId, string? status, CancellationToken ct)
    {
        var columns = new[]
        {
            new MaintenanceReportColumnDto("vehicle", "Vehicle", "text"),
            new MaintenanceReportColumnDto("breakdownDate", "Breakdown Date", "date"),
            new MaintenanceReportColumnDto("location", "Location", "text"),
            new MaintenanceReportColumnDto("repairCost", "Repair Cost", "currency"),
            new MaintenanceReportColumnDto("downtimeDays", "Downtime Days", "number"),
            new MaintenanceReportColumnDto("status", "Status", "text")
        };

        var clauses = new List<string> { "b.IsDeleted = 0", "v.TenantId = @TenantId", "v.IsDeleted = 0",
            "b.ReportedAt >= @From", "b.ReportedAt < @To" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("From", from);
        p.Add("To", to);
        MaintenanceReportSql.ApplyVehicleBranchFilters(p, vehicleId, branchId, "v", null, clauses);
        var where = MaintenanceReportSql.BuildWhere(clauses);

        var raw = await connection.QueryAsync<dynamic>(new CommandDefinition($"""
            SELECT b.Id, v.Name AS VehicleName, b.BreakdownLocation, b.RepairCost, b.Status AS BreakdownStatus,
                b.ReportedAt,
                DATEDIFF(day, b.ReportedAt, COALESCE(b.ResolvedAt, GETUTCDATE())) AS DowntimeDays
            FROM VehicleBreakdowns b
            INNER JOIN Vehicles v ON v.Id = b.VehicleId
            {where}
            ORDER BY b.ReportedAt DESC
            """, p, cancellationToken: ct));

        var rows = raw.Select(r =>
        {
            var rowStatus = ((string)r.BreakdownStatus).Equals("Resolved", StringComparison.OrdinalIgnoreCase)
                ? "Resolved" : "Open";
            return (Row: MaintenanceReportHelper.Row(
                ((int)r.Id).ToString(), (string)r.VehicleName, 1, (decimal)r.RepairCost,
                ("vehicle", (object?)r.VehicleName),
                ("breakdownDate", (object?)r.ReportedAt),
                ("location", (object?)r.BreakdownLocation),
                ("repairCost", (object?)(decimal)r.RepairCost),
                ("downtimeDays", (object?)(int)r.DowntimeDays),
                ("status", rowStatus)),
                Status: rowStatus);
        }).Where(x => MaintenanceReportHelper.MatchesStatusFilter(status, x.Status))
          .Select(x => x.Row).ToList();

        return new MaintenanceReportDto("breakdown", MaintenanceReportHelper.TitleFor("breakdown"),
            columns, rows, rows.Sum(r => r.TotalCost),
            new Dictionary<string, object?> { ["rowCount"] = rows.Count });
    }


    internal static MaintenanceScheduleListItemDto MapListItem(ScheduleListRow row)
    {
        var status = MaintenanceScheduleHelper.ComputeStatus(
            row.IntervalType, row.DueDate, row.NextServiceMileage, row.NextDueEngineHours,
            row.CurrentMileage, row.CurrentEngineHours);

        return new MaintenanceScheduleListItemDto(
            row.Id, row.VehicleId, row.VehicleName, row.VehicleRegistration,
            row.CurrentMileage, row.NextServiceMileage, row.DueDate,
            row.ServiceTypeName, row.IntervalType, row.IntervalValue,
            status, row.Priority, row.IsActive,
            row.CurrentEngineHours, row.NextDueEngineHours,
            row.LastServiceMileage, row.LastServiceDate, row.LastServiceEngineHours);
    }


    private static DateTime? ResolveCalendarDate(ScheduleListRow row, string status, DateTime today)
    {
        if (row.DueDate.HasValue)
            return row.DueDate.Value.Date;

        if (MaintenanceScheduleHelper.IsMileageInterval(row.IntervalType) ||
            MaintenanceScheduleHelper.IsEngineHoursInterval(row.IntervalType))
        {
            if (status is MaintenanceScheduleHelper.StatusDueSoon or MaintenanceScheduleHelper.StatusOverdue)
                return today;
            if (row.LastServiceDate.HasValue)
                return row.LastServiceDate.Value.Date;
        }

        return null;
    }

    private sealed class VehicleLookupRow
    {
        public decimal CurrentMileage { get; init; }
        public decimal CurrentEngineHours { get; init; }
        public int Status { get; init; }
    }

}
