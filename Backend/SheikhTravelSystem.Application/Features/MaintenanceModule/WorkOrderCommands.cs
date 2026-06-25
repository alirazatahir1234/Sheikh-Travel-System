using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record CreateWorkOrderCommand(CreateWorkOrderDto Body) : IRequest<ApiResponse<int>>;

public record UpdateWorkOrderStatusCommand(int Id, UpdateWorkOrderStatusDto Body) : IRequest<ApiResponse<bool>>;

public record UpdateWorkOrderCommand(int Id, UpdateWorkOrderDto Body) : IRequest<ApiResponse<bool>>;

public class CreateWorkOrderCommandValidator : AbstractValidator<CreateWorkOrderCommand>
{
    public CreateWorkOrderCommandValidator()
    {
        RuleFor(x => x.Body.VehicleId)
            .GreaterThan(0)
            .WithMessage("Please select a vehicle.");

        RuleFor(x => x.Body.ServiceTypeName)
            .Must(s => !string.IsNullOrWhiteSpace(s))
            .WithMessage("Select at least one service item.");

        RuleFor(x => x.Body.Priority)
            .Must(WorkOrderValidation.IsValidPriority)
            .When(x => !string.IsNullOrWhiteSpace(x.Body.Priority))
            .WithMessage("Priority is invalid.");

        RuleFor(x => x.Body.MaintenanceType)
            .Must(WorkOrderValidation.IsValidMaintenanceType)
            .When(x => !string.IsNullOrWhiteSpace(x.Body.MaintenanceType))
            .WithMessage("Maintenance type is invalid.");

        RuleFor(x => x.Body.LaborCost)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Labor cost cannot be negative.")
            .LessThanOrEqualTo(WorkOrderValidation.MaxCost)
            .WithMessage($"Labor cost cannot exceed {WorkOrderValidation.MaxCost:N2}.");

        RuleFor(x => x.Body.PartsCost)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Parts cost cannot be negative.")
            .LessThanOrEqualTo(WorkOrderValidation.MaxCost)
            .WithMessage($"Parts cost cannot exceed {WorkOrderValidation.MaxCost:N2}.");

        RuleFor(x => x.Body.Notes)
            .MaximumLength(WorkOrderValidation.NotesMaxLength)
            .WithMessage($"Notes cannot exceed {WorkOrderValidation.NotesMaxLength} characters.");

        RuleFor(x => x.Body)
            .Must(b => WorkOrderValidation.IsValidDateRange(b.StartDate, b.EstimatedCompletionDate))
            .WithMessage("Estimated completion must be on or after the start date.");

        RuleFor(x => x.Body)
            .Must(b => !string.Equals(b.MaintenanceType, "Emergency", StringComparison.OrdinalIgnoreCase) || b.StartDate.HasValue)
            .WithMessage("Start date is required for emergency work orders.");
    }
}

public class CreateWorkOrderCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateWorkOrderCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateWorkOrderCommand request, CancellationToken cancellationToken)
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
}

public class UpdateWorkOrderStatusCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateWorkOrderStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateWorkOrderStatusCommand request, CancellationToken cancellationToken)
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
}

public class UpdateWorkOrderCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateWorkOrderCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateWorkOrderCommand request, CancellationToken cancellationToken)
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
}
