using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Assignments;

public record CreateAssignmentCommand(CreateAssignmentRequest Body) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Assignment";
    public int? AuditEntityId => null;
}

public class CreateAssignmentCommandValidator : AbstractValidator<CreateAssignmentCommand>
{
    public CreateAssignmentCommandValidator()
    {
        RuleFor(x => x.Body.VehicleId).GreaterThan(0);
        RuleFor(x => x.Body.DriverId).GreaterThan(0);
        RuleFor(x => x.Body.AssignmentType).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Body.StartDate).NotEmpty();
        RuleFor(x => x.Body.Reason).MaximumLength(300).When(x => x.Body.Reason != null);
        RuleFor(x => x.Body.Notes).MaximumLength(500).When(x => x.Body.Notes != null);
    }
}

public class CreateAssignmentCommandHandler(
    IAssignmentRepository assignmentRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateAssignmentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateAssignmentCommand request, CancellationToken cancellationToken)
    {
        var id = await assignmentRepository.CreateAsync(
            tenantContext.GetRequiredTenantId(), request.Body,
            currentUser.UserId?.ToString() ?? "system", cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Assignment created successfully.");
    }
}

public record TransferAssignmentCommand(int AssignmentId, TransferAssignmentRequest Body)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Transfer";
    public string AuditEntityName => "Assignment";
    public int? AuditEntityId => AssignmentId;
}

public class TransferAssignmentCommandValidator : AbstractValidator<TransferAssignmentCommand>
{
    public TransferAssignmentCommandValidator()
    {
        RuleFor(x => x.AssignmentId).GreaterThan(0);
        RuleFor(x => x.Body.TransferType).NotEmpty();
    }
}

public class TransferAssignmentCommandHandler(
    IAssignmentRepository assignmentRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<TransferAssignmentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(TransferAssignmentCommand request, CancellationToken cancellationToken)
    {
        var newId = await assignmentRepository.TransferAsync(
            tenantContext.GetRequiredTenantId(), request.AssignmentId, request.Body,
            currentUser.UserId?.ToString() ?? "system", cancellationToken);
        return ApiResponse<int>.SuccessResponse(newId, "Assignment transferred successfully.");
    }
}

public record CompleteAssignmentCommand(int AssignmentId, CompleteAssignmentRequest Body)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Complete";
    public string AuditEntityName => "Assignment";
    public int? AuditEntityId => AssignmentId;
}

public class CompleteAssignmentCommandHandler(
    IAssignmentRepository assignmentRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CompleteAssignmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(CompleteAssignmentCommand request, CancellationToken cancellationToken)
    {
        await assignmentRepository.CompleteAsync(
            tenantContext.GetRequiredTenantId(), request.AssignmentId, request.Body,
            currentUser.UserId?.ToString(), cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Assignment completed.");
    }
}

public record CancelAssignmentCommand(int AssignmentId, CancelAssignmentRequest Body)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Cancel";
    public string AuditEntityName => "Assignment";
    public int? AuditEntityId => AssignmentId;
}

public class CancelAssignmentCommandHandler(
    IAssignmentRepository assignmentRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CancelAssignmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(CancelAssignmentCommand request, CancellationToken cancellationToken)
    {
        await assignmentRepository.CancelAsync(
            tenantContext.GetRequiredTenantId(), request.AssignmentId, request.Body,
            currentUser.UserId?.ToString() ?? "system", cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Assignment cancelled.");
    }
}
