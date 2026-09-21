using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

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
    IAssignmentRepository assignmentRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<ApproveAssignmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ApproveAssignmentCommand request, CancellationToken cancellationToken)
    {
        var by = currentUser.UserId?.ToString() ?? "system";
        await assignmentRepository.ApproveAsync(
            tenantContext.GetRequiredTenantId(), request.AssignmentId, request.Body.Notes, by, cancellationToken);
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
    IAssignmentRepository assignmentRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<RejectAssignmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(RejectAssignmentCommand request, CancellationToken cancellationToken)
    {
        var by = currentUser.UserId?.ToString() ?? "system";
        await assignmentRepository.RejectAsync(
            tenantContext.GetRequiredTenantId(), request.AssignmentId, request.Body.Reason, by, cancellationToken);
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
        var ids = request.Body.AssignmentIds.Distinct().ToList();
        var succeeded = 0;
        var errors = new List<string>();
        foreach (var id in ids)
        {
            try
            {
                var result = await mediator.Send(new CompleteAssignmentCommand(id, new CompleteAssignmentRequest(request.Body.Reason)), cancellationToken);
                if (result.Success) succeeded++;
                else errors.Add($"#{id}: {result.Message}");
            }
            catch (Exception ex) { errors.Add($"#{id}: {ex.Message}"); }
        }
        return ApiResponse<BulkAssignmentResultDto>.SuccessResponse(
            new BulkAssignmentResultDto(succeeded, ids.Count - succeeded, errors));
    }
}

public class BulkCancelAssignmentsCommandHandler(IMediator mediator)
    : IRequestHandler<BulkCancelAssignmentsCommand, ApiResponse<BulkAssignmentResultDto>>
{
    public async Task<ApiResponse<BulkAssignmentResultDto>> Handle(BulkCancelAssignmentsCommand request, CancellationToken cancellationToken)
    {
        var ids = request.Body.AssignmentIds.Distinct().ToList();
        var succeeded = 0;
        var errors = new List<string>();
        foreach (var id in ids)
        {
            try
            {
                var result = await mediator.Send(new CancelAssignmentCommand(id, new CancelAssignmentRequest(request.Body.Reason)), cancellationToken);
                if (result.Success) succeeded++;
                else errors.Add($"#{id}: {result.Message}");
            }
            catch (Exception ex) { errors.Add($"#{id}: {ex.Message}"); }
        }
        return ApiResponse<BulkAssignmentResultDto>.SuccessResponse(
            new BulkAssignmentResultDto(succeeded, ids.Count - succeeded, errors));
    }
}
