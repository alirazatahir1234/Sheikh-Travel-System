using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Maintenance.DTOs;

public record MaintenanceDto(
    int Id, int VehicleId, string Description, decimal Cost,
    DateTime MaintenanceDate, DateTime? NextDueDate,
    MaintenanceStatus Status, string? ServiceProvider, DateTime CreatedAt);

public record CreateMaintenanceDto(
    int VehicleId, string Description, decimal Cost,
    DateTime MaintenanceDate, DateTime? NextDueDate, string? ServiceProvider);
