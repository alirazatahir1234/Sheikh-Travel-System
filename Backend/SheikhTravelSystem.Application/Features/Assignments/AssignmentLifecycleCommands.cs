using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Assignments;

public record ApproveAssignmentCommand(int AssignmentId, ApproveAssignmentRequest Body)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Approve";
    public string AuditEntityName => "Assignment";
    public int? AuditEntityId => AssignmentId;
}

public class ApproveAssignmentCommandValidator : AbstractValidator<ApproveAssignmentCommand>
{
    public ApproveAssignmentCommandValidator() => RuleFor(x => x.AssignmentId).GreaterThan(0);
}

public class ApproveAssignmentCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<ApproveAssignmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ApproveAssignmentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var by = currentUser.UserId?.ToString() ?? "system";

        var row = await connection.QuerySingleOrDefaultAsync<(int Id, int VehicleId, int? DriverId, string Status)>(
            new CommandDefinition(
                @"SELECT a.Id, a.VehicleId, a.DriverId, a.Status
                  FROM AssignmentHistory a INNER JOIN Vehicles v ON a.VehicleId = v.Id
                  WHERE a.Id = @Id AND v.TenantId = @TenantId AND a.IsDeleted = 0",
                new { Id = request.AssignmentId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row.Id == 0) throw new NotFoundException("Assignment", request.AssignmentId);
        if (row.Status != "PendingApproval")
            throw new ConflictException("Only pending assignments can be approved.");

        var newStatus = "Scheduled";
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE AssignmentHistory SET Status = @Status, ApprovedBy = @ApprovedBy,
              ModifiedAt = GETUTCDATE(), ModifiedBy = @ModifiedBy
              WHERE Id = @Id",
            new { Id = request.AssignmentId, Status = newStatus, ApprovedBy = by, ModifiedBy = by },
            cancellationToken: cancellationToken));

        await AssignmentChangelogWriter.WriteAsync(connection, tenantId, request.AssignmentId,
            row.VehicleId, row.VehicleId, row.DriverId, row.DriverId,
            "Approved", request.Body.Notes, by, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Assignment approved.");
    }
}

public record RejectAssignmentCommand(int AssignmentId, RejectAssignmentRequest Body)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Reject";
    public string AuditEntityName => "Assignment";
    public int? AuditEntityId => AssignmentId;
}

public class RejectAssignmentCommandValidator : AbstractValidator<RejectAssignmentCommand>
{
    public RejectAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId).GreaterThan(0);
        RuleFor(x => x.Body.Reason).NotEmpty().MaximumLength(300);
    }
}

public class RejectAssignmentCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<RejectAssignmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(RejectAssignmentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var by = currentUser.UserId?.ToString() ?? "system";

        var row = await connection.QuerySingleOrDefaultAsync<(int Id, int VehicleId, string Status)>(
            new CommandDefinition(
                @"SELECT a.Id, a.VehicleId, a.Status
                  FROM AssignmentHistory a INNER JOIN Vehicles v ON a.VehicleId = v.Id
                  WHERE a.Id = @Id AND v.TenantId = @TenantId AND a.IsDeleted = 0",
                new { Id = request.AssignmentId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row.Id == 0) throw new NotFoundException("Assignment", request.AssignmentId);
        if (row.Status != "PendingApproval")
            throw new ConflictException("Only pending assignments can be rejected.");

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE AssignmentHistory SET Status = N'Cancelled', EndAt = GETUTCDATE(),
              Reason = @Reason, ModifiedAt = GETUTCDATE(), ModifiedBy = @ModifiedBy
              WHERE Id = @Id",
            new { Id = request.AssignmentId, request.Body.Reason, ModifiedBy = by },
            cancellationToken: cancellationToken));

        await AssignmentChangelogWriter.WriteAsync(connection, tenantId, request.AssignmentId,
            row.VehicleId, null, null, null, "Rejected", request.Body.Reason, by, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Assignment rejected.");
    }
}

public record BulkCompleteAssignmentsCommand(BulkAssignmentIdsRequest Body) : IRequest<ApiResponse<BulkAssignmentResultDto>>;

public record BulkCancelAssignmentsCommand(BulkAssignmentIdsRequest Body) : IRequest<ApiResponse<BulkAssignmentResultDto>>;

public class BulkCompleteAssignmentsCommandHandler(IMediator mediator)
    : IRequestHandler<BulkCompleteAssignmentsCommand, ApiResponse<BulkAssignmentResultDto>>
{
    public async Task<ApiResponse<BulkAssignmentResultDto>> Handle(BulkCompleteAssignmentsCommand request, CancellationToken cancellationToken)
    {
        var succeeded = 0;
        var errors = new List<string>();
        foreach (var id in request.Body.AssignmentIds.Distinct())
        {
            try
            {
                var result = await mediator.Send(new CompleteAssignmentCommand(id, new CompleteAssignmentRequest(request.Body.Reason)), cancellationToken);
                if (result.Success) succeeded++;
                else errors.Add($"#{id}: {result.Message}");
            }
            catch (Exception ex)
            {
                errors.Add($"#{id}: {ex.Message}");
            }
        }
        return ApiResponse<BulkAssignmentResultDto>.SuccessResponse(
            new BulkAssignmentResultDto(succeeded, request.Body.AssignmentIds.Count - succeeded, errors));
    }
}

public class BulkCancelAssignmentsCommandHandler(IMediator mediator)
    : IRequestHandler<BulkCancelAssignmentsCommand, ApiResponse<BulkAssignmentResultDto>>
{
    public async Task<ApiResponse<BulkAssignmentResultDto>> Handle(BulkCancelAssignmentsCommand request, CancellationToken cancellationToken)
    {
        var succeeded = 0;
        var errors = new List<string>();
        foreach (var id in request.Body.AssignmentIds.Distinct())
        {
            try
            {
                var result = await mediator.Send(new CancelAssignmentCommand(id, new CancelAssignmentRequest(request.Body.Reason)), cancellationToken);
                if (result.Success) succeeded++;
                else errors.Add($"#{id}: {result.Message}");
            }
            catch (Exception ex)
            {
                errors.Add($"#{id}: {ex.Message}");
            }
        }
        return ApiResponse<BulkAssignmentResultDto>.SuccessResponse(
            new BulkAssignmentResultDto(succeeded, request.Body.AssignmentIds.Count - succeeded, errors));
    }
}
