using System.Text.Json;
using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record GetMaintenanceRequestStatsQuery : IRequest<ApiResponse<MaintenanceRequestStatsDto>>;

public record ApproveMaintenanceRequestCommand(int Id) : IRequest<ApiResponse<bool>>;

public record RejectMaintenanceRequestCommand(int Id, RejectMaintenanceRequestDto Body) : IRequest<ApiResponse<bool>>;

public record SearchMaintenanceQuery(string Q, int Limit = 15) : IRequest<ApiResponse<IReadOnlyList<MaintenanceSearchResultDto>>>;

public record DismissMaintenanceAlertCommand(int Id) : IRequest<ApiResponse<bool>>;

public record GetMaintenanceComplianceSummaryQuery : IRequest<ApiResponse<ComplianceSummaryDto>>;

public record UploadMaintenanceRequestAttachmentCommand(int RequestId, Stream FileStream, string FileName, string ContentType, long FileLength)
    : IRequest<ApiResponse<string>>;

public class GetMaintenanceRequestStatsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetMaintenanceRequestStatsQuery, ApiResponse<MaintenanceRequestStatsDto>>
{
    public async Task<ApiResponse<MaintenanceRequestStatsDto>> Handle(GetMaintenanceRequestStatsQuery request, CancellationToken cancellationToken)
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
}

public class ApproveMaintenanceRequestCommandHandler(
    IDbConnectionFactory dbFactory, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<ApproveMaintenanceRequestCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ApproveMaintenanceRequestCommand request, CancellationToken cancellationToken)
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
}

public class RejectMaintenanceRequestCommandHandler(
    IDbConnectionFactory dbFactory, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<RejectMaintenanceRequestCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(RejectMaintenanceRequestCommand request, CancellationToken cancellationToken)
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
}

public class SearchMaintenanceQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<SearchMaintenanceQuery, ApiResponse<IReadOnlyList<MaintenanceSearchResultDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<MaintenanceSearchResultDto>>> Handle(SearchMaintenanceQuery request, CancellationToken cancellationToken)
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
}

public class DismissMaintenanceAlertCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<DismissMaintenanceAlertCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DismissMaintenanceAlertCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE MaintenanceAlerts SET IsDismissed = 1 WHERE Id = @Id AND TenantId = @TenantId",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (affected == 0) throw new NotFoundException("MaintenanceAlert", request.Id);
        return ApiResponse<bool>.SuccessResponse(true);
    }
}

public class GetMaintenanceComplianceSummaryQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetMaintenanceComplianceSummaryQuery, ApiResponse<ComplianceSummaryDto>>
{
    public async Task<ApiResponse<ComplianceSummaryDto>> Handle(GetMaintenanceComplianceSummaryQuery request, CancellationToken cancellationToken)
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
}

public class UploadMaintenanceRequestAttachmentCommandHandler(
    IDbConnectionFactory dbFactory, ITenantContext tenantContext, IFileStorageService fileStorage)
    : IRequestHandler<UploadMaintenanceRequestAttachmentCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(UploadMaintenanceRequestAttachmentCommand request, CancellationToken cancellationToken)
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
}
