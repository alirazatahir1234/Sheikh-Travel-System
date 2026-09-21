using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

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

public record ListSchedulableMaintenanceVehiclesQuery()
    : IRequest<ApiResponse<IReadOnlyList<MaintenanceSchedulableVehicleDto>>>;

// ── Commands ──────────────────────────────────────────────────────────────────

public record CreateMaintenanceScheduleCommand(CreateMaintenanceScheduleDto Body)
    : IRequest<ApiResponse<int>>;

public class CreateMaintenanceScheduleCommandValidator : AbstractValidator<CreateMaintenanceScheduleCommand>
{
    private static readonly HashSet<string> ValidIntervalTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mileage", "Months", "Month", "Days", "Day", "EngineHours", "Hours"
    };

    public CreateMaintenanceScheduleCommandValidator()
    {
        RuleFor(x => x.Body.VehicleId).GreaterThan(0).WithMessage("Vehicle is required.");
        RuleFor(x => x.Body.ServiceTypeName)
            .Must(n => !string.IsNullOrWhiteSpace(n))
            .WithMessage("Service type is required.")
            .MaximumLength(150);
        RuleFor(x => x.Body.IntervalType)
            .Must(t => ValidIntervalTypes.Contains(t))
            .WithMessage("Interval type is invalid.");
        RuleFor(x => x.Body.IntervalValue).GreaterThan(0).WithMessage("Interval value must be greater than zero.");
        RuleFor(x => x.Body.Priority).NotEmpty().WithMessage("Priority is required.");
    }
}

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

public class ListMaintenanceSchedulesQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<ListMaintenanceSchedulesQuery, ApiResponse<IReadOnlyList<MaintenanceScheduleListItemDto>>>
{
    public Task<ApiResponse<IReadOnlyList<MaintenanceScheduleListItemDto>>> Handle(ListMaintenanceSchedulesQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.ListMaintenanceSchedulesAsync(request, cancellationToken);
}

public class GetMaintenanceScheduleCalendarQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<GetMaintenanceScheduleCalendarQuery, ApiResponse<IReadOnlyList<MaintenanceScheduleCalendarItemDto>>>
{
    public Task<ApiResponse<IReadOnlyList<MaintenanceScheduleCalendarItemDto>>> Handle(GetMaintenanceScheduleCalendarQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.GetMaintenanceScheduleCalendarAsync(request, cancellationToken);
}

public class GetMaintenanceScheduleTemplatesQueryHandler
    : IRequestHandler<GetMaintenanceScheduleTemplatesQuery, ApiResponse<IReadOnlyList<MaintenanceScheduleTemplateDto>>>
{
    public Task<ApiResponse<IReadOnlyList<MaintenanceScheduleTemplateDto>>> Handle(
        GetMaintenanceScheduleTemplatesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(ApiResponse<IReadOnlyList<MaintenanceScheduleTemplateDto>>.SuccessResponse(
            MaintenanceScheduleHelper.DefaultTemplates));
}

public class ListSchedulableMaintenanceVehiclesQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<ListSchedulableMaintenanceVehiclesQuery, ApiResponse<IReadOnlyList<MaintenanceSchedulableVehicleDto>>>
{
    public Task<ApiResponse<IReadOnlyList<MaintenanceSchedulableVehicleDto>>> Handle(ListSchedulableMaintenanceVehiclesQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.ListSchedulableMaintenanceVehiclesAsync(request, cancellationToken);
}

public class CreateMaintenanceScheduleCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<CreateMaintenanceScheduleCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(CreateMaintenanceScheduleCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.CreateMaintenanceScheduleAsync(request, cancellationToken);
}

public class RescheduleMaintenanceScheduleCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<RescheduleMaintenanceScheduleCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(RescheduleMaintenanceScheduleCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.RescheduleMaintenanceScheduleAsync(request, cancellationToken);
}

public class UpdateMaintenanceScheduleCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<UpdateMaintenanceScheduleCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(UpdateMaintenanceScheduleCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.UpdateMaintenanceScheduleAsync(request, cancellationToken);
}

public class CreateWorkOrderFromScheduleCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<CreateWorkOrderFromScheduleCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(CreateWorkOrderFromScheduleCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.CreateWorkOrderFromScheduleAsync(request, cancellationToken);
}
