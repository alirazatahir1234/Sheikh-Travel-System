using Dapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record ListAnalyticsReportSchedulesQuery() : IRequest<ApiResponse<IReadOnlyList<AnalyticsReportScheduleDto>>>;

public record CreateAnalyticsReportScheduleCommand(CreateAnalyticsReportScheduleDto Body)
    : IRequest<ApiResponse<int>>;

public record UpdateAnalyticsReportScheduleCommand(int Id, UpdateAnalyticsReportScheduleDto Body)
    : IRequest<ApiResponse<bool>>;

public record DeleteAnalyticsReportScheduleCommand(int Id) : IRequest<ApiResponse<bool>>;

public class ListAnalyticsReportSchedulesQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ListAnalyticsReportSchedulesQuery, ApiResponse<IReadOnlyList<AnalyticsReportScheduleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AnalyticsReportScheduleDto>>> Handle(
        ListAnalyticsReportSchedulesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var rows = await connection.QueryAsync<ScheduleRow>(new CommandDefinition("""
            SELECT Id, ReportType, FiltersJson, Frequency, Recipients, NextRunAt, LastRunAt, LastRunStatus, IsActive
            FROM GpsAnalyticsReportSchedules
            WHERE TenantId = @TenantId AND IsDeleted = 0
            ORDER BY CreatedAt DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<AnalyticsReportScheduleDto>>.SuccessResponse(
            rows.Select(r => r.ToDto()).ToList());
    }
}

public class CreateAnalyticsReportScheduleCommandHandler(
    IDbConnectionFactory dbFactory, ITenantContext tenantContext, ICurrentUserService currentUser,
    ILogger<CreateAnalyticsReportScheduleCommandHandler> logger)
    : IRequestHandler<CreateAnalyticsReportScheduleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        CreateAnalyticsReportScheduleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;
        var nextRun = AnalyticsReportHelper.ComputeNextRunAt(body.Frequency);
        var filtersJson = AnalyticsReportHelper.SerializeFilters(body.Filters);
        var createdBy = currentUser.UserId?.ToString() ?? "system";

        using var connection = dbFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO GpsAnalyticsReportSchedules
                (TenantId, ReportType, FiltersJson, Frequency, Recipients, NextRunAt, LastRunStatus, CreatedBy)
            VALUES
                (@TenantId, @ReportType, @FiltersJson, @Frequency, @Recipients, @NextRunAt, N'Pending', @CreatedBy);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            ReportType = AnalyticsReportHelper.NormalizeReportType(body.ReportType),
            FiltersJson = filtersJson,
            body.Frequency,
            body.Recipients,
            NextRunAt = nextRun,
            CreatedBy = createdBy
        }, cancellationToken: cancellationToken));

        logger.LogInformation("Analytics report schedule {ScheduleId} queued (email delivery stubbed)", id);
        return ApiResponse<int>.SuccessResponse(id);
    }
}

public class UpdateAnalyticsReportScheduleCommandHandler(
    IDbConnectionFactory dbFactory, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateAnalyticsReportScheduleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateAnalyticsReportScheduleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM GpsAnalyticsReportSchedules WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (exists == 0) throw new NotFoundException("AnalyticsReportSchedule", request.Id);

        var body = request.Body;
        var nextRun = body.Frequency is not null
            ? AnalyticsReportHelper.ComputeNextRunAt(body.Frequency)
            : (DateTime?)null;

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE GpsAnalyticsReportSchedules SET
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
            FiltersJson = body.Filters is null ? null : AnalyticsReportHelper.SerializeFilters(body.Filters),
            body.IsActive,
            NextRunAt = nextRun,
            UpdatedBy = currentUser.UserId?.ToString() ?? "system"
        }, cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true);
    }
}

public class DeleteAnalyticsReportScheduleCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<DeleteAnalyticsReportScheduleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        DeleteAnalyticsReportScheduleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE GpsAnalyticsReportSchedules SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (affected == 0) throw new NotFoundException("AnalyticsReportSchedule", request.Id);
        return ApiResponse<bool>.SuccessResponse(true);
    }
}

internal sealed record ScheduleRow(
    int Id, string ReportType, string? FiltersJson, string Frequency, string Recipients,
    DateTime? NextRunAt, DateTime? LastRunAt, string? LastRunStatus, bool IsActive)
{
    public AnalyticsReportScheduleDto ToDto() => new(
        Id, ReportType, AnalyticsReportHelper.ParseFilters(FiltersJson),
        Frequency, Recipients, NextRunAt, LastRunAt, LastRunStatus, IsActive);
}
