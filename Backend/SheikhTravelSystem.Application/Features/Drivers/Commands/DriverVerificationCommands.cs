using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Commands;

public record UpdateDocumentStatusCommand(
    int DriverId, int DocumentId, string Status, string? RejectionReason = null)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "DriverDocument";
    public int? AuditEntityId => DocumentId;
}

public class UpdateDocumentStatusCommandValidator : AbstractValidator<UpdateDocumentStatusCommand>
{
    private static readonly HashSet<string> AllowedStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Approved", "Rejected" };

    public UpdateDocumentStatusCommandValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0);
        RuleFor(x => x.DocumentId).GreaterThan(0);
        RuleFor(x => x.Status).Must(s => AllowedStatuses.Contains(s))
            .WithMessage("Status must be 'Approved' or 'Rejected'.");
        RuleFor(x => x.RejectionReason)
            .NotEmpty()
            .MaximumLength(500)
            .When(x => string.Equals(x.Status, "Rejected", StringComparison.OrdinalIgnoreCase));
    }
}

public class UpdateDocumentStatusCommandHandler(
    IDriverRepository driverRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateDocumentStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateDocumentStatusCommand request, CancellationToken cancellationToken)
    {
        var reviewer = currentUser.UserId?.ToString() ?? "api";
        await driverRepository.UpdateDocumentStatusAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, request.DocumentId,
            request.Status, request.RejectionReason, reviewer, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true,
            request.Status == "Approved" ? "Document approved." : "Document rejected.");
    }
}

public record AddDriverReviewNoteCommand(int DriverId, string Note, string? DocumentType = null)
    : IRequest<ApiResponse<DriverReviewNoteDto>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "DriverReviewNote";
    public int? AuditEntityId => null;
}

public class AddDriverReviewNoteCommandValidator : AbstractValidator<AddDriverReviewNoteCommand>
{
    public AddDriverReviewNoteCommandValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0);
        RuleFor(x => x.Note).NotEmpty().MaximumLength(1000);
    }
}

public class AddDriverReviewNoteCommandHandler(
    IDriverRepository driverRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<AddDriverReviewNoteCommand, ApiResponse<DriverReviewNoteDto>>
{
    public async Task<ApiResponse<DriverReviewNoteDto>> Handle(
        AddDriverReviewNoteCommand request, CancellationToken cancellationToken)
    {
        var createdBy = currentUser.UserId?.ToString() ?? "api";
        var id = await driverRepository.InsertReviewNoteAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, request.Note,
            request.DocumentType, createdBy, cancellationToken);
        var dto = new DriverReviewNoteDto(id, request.Note, request.DocumentType, createdBy, DateTime.UtcNow);
        return ApiResponse<DriverReviewNoteDto>.SuccessResponse(dto, "Note added.");
    }
}
