using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record CreateMaintenanceRequestCommand(CreateMaintenanceRequestDto Body)
    : IRequest<ApiResponse<int>>;

public record UpdateMaintenanceRequestCommand(int Id, UpdateMaintenanceRequestDto Body)
    : IRequest<ApiResponse<bool>>;

public record ConvertMaintenanceRequestCommand(int Id, ConvertRequestToWorkOrderDto Body)
    : IRequest<ApiResponse<int>>;

public class CreateMaintenanceRequestCommandValidator : AbstractValidator<CreateMaintenanceRequestCommand>
{
    public CreateMaintenanceRequestCommandValidator()
    {
        RuleFor(x => x.Body.VehicleId).GreaterThan(0).WithMessage("Vehicle is required.");

        RuleFor(x => x.Body.Description)
            .Must(d => !string.IsNullOrWhiteSpace(d))
            .WithMessage("Description is required.")
            .Must(d => d!.Trim().Length >= MaintenanceRequestValidation.DescriptionMinLength)
            .WithMessage($"Description must be at least {MaintenanceRequestValidation.DescriptionMinLength} characters.")
            .Must(d => d!.Trim().Length <= MaintenanceRequestValidation.DescriptionMaxLength)
            .WithMessage($"Description cannot exceed {MaintenanceRequestValidation.DescriptionMaxLength} characters.");

        RuleFor(x => x.Body.Priority)
            .NotEmpty().WithMessage("Priority is required.")
            .Must(MaintenanceRequestValidation.IsValidPriority)
            .WithMessage("Priority is invalid.");

        RuleFor(x => x.Body.RequestType)
            .NotEmpty().WithMessage("Type is required.")
            .Must(MaintenanceRequestValidation.IsValidRequestType)
            .WithMessage("Request type is invalid.");

        RuleFor(x => x.Body.IssueCategory)
            .NotEmpty().WithMessage("Category is required.")
            .Must(MaintenanceRequestValidation.IsValidIssueCategory)
            .WithMessage("Issue category is invalid.");
    }
}

public class CreateMaintenanceRequestCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateMaintenanceRequestCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var body = request.Body;
        using var connection = dbFactory.CreateConnection();

        var vehicleExists = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT 1 FROM Vehicles WHERE Id = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0",
            new { body.VehicleId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (vehicleExists is null)
            throw new NotFoundException("Vehicle", body.VehicleId);

        var seq = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) + 1 FROM MaintenanceRequests WHERE TenantId = @TenantId",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));

        var requestNumber = $"MR-{seq:D5}";

        var vehicleMeta = await connection.QuerySingleOrDefaultAsync<(int? BranchId, int? DepartmentId)>(
            new CommandDefinition(
                "SELECT BranchId, DepartmentId FROM Vehicles WHERE Id = @VehicleId AND TenantId = @TenantId",
                new { body.VehicleId, TenantId = tenantId }, cancellationToken: cancellationToken));

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO MaintenanceRequests
                (TenantId, RequestNumber, VehicleId, DriverId, BranchId, DepartmentId, RequestType, Priority, IssueCategory,
                 Description, BreakdownLocation, DriverRemarks, PhotosJson, DocumentsJson, Status, CreatedBy, CreatedAt)
            VALUES
                (@TenantId, @RequestNumber, @VehicleId, @DriverId, @BranchId, @DepartmentId, @RequestType, @Priority, @IssueCategory,
                 @Description, @BreakdownLocation, @DriverRemarks, @PhotosJson, @DocumentsJson, N'Open', @CreatedBy, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            RequestNumber = requestNumber,
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
}

public class UpdateMaintenanceRequestCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateMaintenanceRequestCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateMaintenanceRequestCommand request, CancellationToken cancellationToken)
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
}

public class ConvertMaintenanceRequestCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<ConvertMaintenanceRequestCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(ConvertMaintenanceRequestCommand request, CancellationToken cancellationToken)
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
        var seq = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) + 1 FROM WorkOrders WHERE TenantId = @TenantId",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));

        var woNumber = $"WO-{seq:D5}";
        var serviceType = body.ServiceTypeName ?? req.IssueCategory;

        var woId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO WorkOrders
                (TenantId, WorkOrderNumber, RequestId, VehicleId, WorkshopId, TechnicianId,
                 ServiceTypeId, ServiceTypeName, StartDate, EstimatedCompletionDate,
                 Priority, Notes, Status, CreatedBy, CreatedAt)
            VALUES
                (@TenantId, @WorkOrderNumber, @RequestId, @VehicleId, @WorkshopId, @TechnicianId,
                 @ServiceTypeId, @ServiceTypeName, @StartDate, @EstimatedCompletionDate,
                 @Priority, @Description, N'Open', @CreatedBy, GETUTCDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
        {
            TenantId = tenantId,
            WorkOrderNumber = woNumber,
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
        }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE MaintenanceRequests SET WorkOrderId = @WorkOrderId, Status = N'Converted', UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { WorkOrderId = woId, request.Id, TenantId = tenantId }, cancellationToken: cancellationToken));

        await MaintenanceAlertHelper.InsertAlertAsync(
            connection, tenantId, req.VehicleId, "WorkOrderCreated", "Info",
            $"Work order {woNumber} created", $"Converted from maintenance request.", "WorkOrder", woId, cancellationToken);

        return ApiResponse<int>.SuccessResponse(woId);
    }
}
