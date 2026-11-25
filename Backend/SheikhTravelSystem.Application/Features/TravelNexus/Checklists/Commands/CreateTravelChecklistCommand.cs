using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Checklists.Commands;

/// <summary>Creates a Draft checklist (version 1).</summary>
public record CreateTravelChecklistCommand(SaveTravelChecklistTemplateDto Checklist) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "TravelChecklist";
    public int? AuditEntityId => null;
}

public class CreateTravelChecklistCommandValidator : AbstractValidator<CreateTravelChecklistCommand>
{
    public CreateTravelChecklistCommandValidator() => RuleFor(x => x.Checklist).NotNull().SetValidator(new SaveTravelChecklistTemplateDtoValidator());
}

public class CreateTravelChecklistCommandHandler(ITravelChecklistRepository repository, TravelChecklistGuard guard,
    ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<CreateTravelChecklistCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateTravelChecklistCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Checklist with { Name = request.Checklist.Name.Trim(), Notes = string.IsNullOrWhiteSpace(request.Checklist.Notes) ? null : request.Checklist.Notes.Trim() };
        var error = await guard.ValidateScopeAsync(tenantId, dto, cancellationToken);
        if (error is not null) return ApiResponse<int>.FailResponse(error);
        return ApiResponse<int>.SuccessResponse(await repository.CreateAsync(tenantId, dto, currentUser.UserId, cancellationToken), "Checklist created as draft.");
    }
}
