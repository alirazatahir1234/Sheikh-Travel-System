using Dapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record ListMaintenanceReportSchedulesQuery() : IRequest<ApiResponse<IReadOnlyList<MaintenanceReportScheduleDto>>>;

public record CreateMaintenanceReportScheduleCommand(CreateMaintenanceReportScheduleDto Body)
    : IRequest<ApiResponse<int>>;

public record UpdateMaintenanceReportScheduleCommand(int Id, UpdateMaintenanceReportScheduleDto Body)
    : IRequest<ApiResponse<bool>>;

public record DeleteMaintenanceReportScheduleCommand(int Id) : IRequest<ApiResponse<bool>>;

public class ListMaintenanceReportSchedulesQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ListMaintenanceReportSchedulesQuery, ApiResponse<IReadOnlyList<MaintenanceReportScheduleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<MaintenanceReportScheduleDto>>> Handle(
        ListMaintenanceReportSchedulesQuery request, CancellationToken cancellationToken)
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
}

public class CreateMaintenanceReportScheduleCommandHandler(
    IDbConnectionFactory dbFactory, ITenantContext tenantContext, ICurrentUserService currentUser,
    ILogger<CreateMaintenanceReportScheduleCommandHandler> logger)
    : IRequestHandler<CreateMaintenanceReportScheduleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        CreateMaintenanceReportScheduleCommand request, CancellationToken cancellationToken)
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
}

public class UpdateMaintenanceReportScheduleCommandHandler(
    IDbConnectionFactory dbFactory, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateMaintenanceReportScheduleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateMaintenanceReportScheduleCommand request, CancellationToken cancellationToken)
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
}

public class DeleteMaintenanceReportScheduleCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<DeleteMaintenanceReportScheduleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        DeleteMaintenanceReportScheduleCommand request, CancellationToken cancellationToken)
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
}

internal sealed record ScheduleRow(
    int Id, string ReportType, string? FiltersJson, string Frequency, string Recipients,
    DateTime? NextRunAt, DateTime? LastRunAt, string? LastRunStatus, bool IsActive)
{
    public MaintenanceReportScheduleDto ToDto() => new(
        Id, ReportType, MaintenanceReportHelper.ParseFilters(FiltersJson),
        Frequency, Recipients, NextRunAt, LastRunAt, LastRunStatus, IsActive);
}
