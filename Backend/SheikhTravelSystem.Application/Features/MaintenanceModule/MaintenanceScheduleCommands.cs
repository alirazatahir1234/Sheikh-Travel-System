using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

// ── Queries ───────────────────────────────────────────────────────────────────

public record ListMaintenanceSchedulesQuery(
    int? VehicleId = null,
    string? Status = null,
    string? Search = null)
    : IRequest<ApiResponse<IReadOnlyList<MaintenanceScheduleListItemDto>>>;

public record GetMaintenanceScheduleCalendarQuery(DateTime From, DateTime To)
    : IRequest<ApiResponse<IReadOnlyList<MaintenanceScheduleCalendarItemDto>>>;

public record GetMaintenanceScheduleTemplatesQuery()
    : IRequest<ApiResponse<IReadOnlyList<MaintenanceScheduleTemplateDto>>>;

// ── Commands ──────────────────────────────────────────────────────────────────

public record CreateMaintenanceScheduleCommand(CreateMaintenanceScheduleDto Body)
    : IRequest<ApiResponse<int>>;

public record RescheduleMaintenanceScheduleCommand(int Id, RescheduleMaintenanceScheduleDto Body)
    : IRequest<ApiResponse<bool>>;

public record UpdateMaintenanceScheduleCommand(int Id, UpdateMaintenanceScheduleDto Body)
    : IRequest<ApiResponse<bool>>;

public record CreateWorkOrderFromScheduleCommand(int ScheduleId)
    : IRequest<ApiResponse<int>>;

// ── Internal row mapping ──────────────────────────────────────────────────────

public sealed record ScheduleListRow(
    int Id,
    int VehicleId,
    string? VehicleName,
    string? VehicleRegistration,
    decimal CurrentMileage,
    decimal? NextServiceMileage,
    DateTime? DueDate,
    string ServiceTypeName,
    string IntervalType,
    int IntervalValue,
    string Priority,
    bool IsActive,
    decimal? CurrentEngineHours,
    decimal? NextDueEngineHours,
    decimal? LastServiceMileage,
    DateTime? LastServiceDate,
    decimal? LastServiceEngineHours);

// ── Handlers ──────────────────────────────────────────────────────────────────

public class ListMaintenanceSchedulesQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ListMaintenanceSchedulesQuery, ApiResponse<IReadOnlyList<MaintenanceScheduleListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<MaintenanceScheduleListItemDto>>> Handle(
        ListMaintenanceSchedulesQuery request, CancellationToken cancellationToken)
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
}

public class GetMaintenanceScheduleCalendarQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetMaintenanceScheduleCalendarQuery, ApiResponse<IReadOnlyList<MaintenanceScheduleCalendarItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<MaintenanceScheduleCalendarItemDto>>> Handle(
        GetMaintenanceScheduleCalendarQuery request, CancellationToken cancellationToken)
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
}

public class GetMaintenanceScheduleTemplatesQueryHandler
    : IRequestHandler<GetMaintenanceScheduleTemplatesQuery, ApiResponse<IReadOnlyList<MaintenanceScheduleTemplateDto>>>
{
    public Task<ApiResponse<IReadOnlyList<MaintenanceScheduleTemplateDto>>> Handle(
        GetMaintenanceScheduleTemplatesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(ApiResponse<IReadOnlyList<MaintenanceScheduleTemplateDto>>.SuccessResponse(
            MaintenanceScheduleHelper.DefaultTemplates));
}

public class CreateMaintenanceScheduleCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext)
    : IRequestHandler<CreateMaintenanceScheduleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateMaintenanceScheduleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;
        using var connection = dbFactory.CreateConnection();

        var vehicle = await connection.QuerySingleOrDefaultAsync<(decimal CurrentMileage, decimal? CurrentEngineHours)?>(
            new CommandDefinition(
                "SELECT CurrentMileage, CurrentEngineHours FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = body.VehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (vehicle is null)
            throw new NotFoundException("Vehicle", body.VehicleId);

        var lastMileage = body.LastServiceMileage ?? vehicle.Value.CurrentMileage;
        var lastEngineHours = body.LastServiceEngineHours ?? vehicle.Value.CurrentEngineHours;
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
}

public class RescheduleMaintenanceScheduleCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext)
    : IRequestHandler<RescheduleMaintenanceScheduleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        RescheduleMaintenanceScheduleCommand request, CancellationToken cancellationToken)
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
}

public class UpdateMaintenanceScheduleCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext)
    : IRequestHandler<UpdateMaintenanceScheduleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateMaintenanceScheduleCommand request, CancellationToken cancellationToken)
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
}

public class CreateWorkOrderFromScheduleCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateWorkOrderFromScheduleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        CreateWorkOrderFromScheduleCommand request, CancellationToken cancellationToken)
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
}
