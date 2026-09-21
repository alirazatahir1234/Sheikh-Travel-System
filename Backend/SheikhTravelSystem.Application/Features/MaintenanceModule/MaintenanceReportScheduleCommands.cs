using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
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

public class ListMaintenanceReportSchedulesQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<ListMaintenanceReportSchedulesQuery, ApiResponse<IReadOnlyList<MaintenanceReportScheduleDto>>>
{
    public Task<ApiResponse<IReadOnlyList<MaintenanceReportScheduleDto>>> Handle(ListMaintenanceReportSchedulesQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.ListMaintenanceReportSchedulesAsync(request, cancellationToken);
}

public class CreateMaintenanceReportScheduleCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<CreateMaintenanceReportScheduleCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(CreateMaintenanceReportScheduleCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.CreateMaintenanceReportScheduleAsync(request, cancellationToken);
}

public class UpdateMaintenanceReportScheduleCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<UpdateMaintenanceReportScheduleCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(UpdateMaintenanceReportScheduleCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.UpdateMaintenanceReportScheduleAsync(request, cancellationToken);
}

public class DeleteMaintenanceReportScheduleCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<DeleteMaintenanceReportScheduleCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(DeleteMaintenanceReportScheduleCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.DeleteMaintenanceReportScheduleAsync(request, cancellationToken);
}

internal sealed record ScheduleRow(
    int Id, string ReportType, string? FiltersJson, string Frequency, string Recipients,
    DateTime? NextRunAt, DateTime? LastRunAt, string? LastRunStatus, bool IsActive)
{
    public MaintenanceReportScheduleDto ToDto() => new(
        Id, ReportType, MaintenanceReportHelper.ParseFilters(FiltersJson),
        Frequency, Recipients, NextRunAt, LastRunAt, LastRunStatus, IsActive);
}
