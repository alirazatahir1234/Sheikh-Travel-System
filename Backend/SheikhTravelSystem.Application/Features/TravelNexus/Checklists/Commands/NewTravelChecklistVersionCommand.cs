using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Checklists.Commands;

/// <summary>Copies an Active or Retired checklist into a new Draft with the next version number.</summary>
public record NewTravelChecklistVersionCommand(int Id) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "NewVersion";
    public string AuditEntityName => "TravelChecklist";
    public int? AuditEntityId => Id;
}

public class NewTravelChecklistVersionCommandValidator : AbstractValidator<NewTravelChecklistVersionCommand>
{
    public NewTravelChecklistVersionCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class NewTravelChecklistVersionCommandHandler(ITravelChecklistRepository repository, TravelChecklistGuard guard,
    ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<NewTravelChecklistVersionCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(NewTravelChecklistVersionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var existing = await guard.GetAsync(request.Id, tenantId, cancellationToken);
        if (existing.Status == "Draft") return ApiResponse<int>.FailResponse("This checklist is already a draft; edit it directly.");

        var id = await repository.NewVersionAsync(request.Id, tenantId, currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("TravelChecklist", request.Id);
        return ApiResponse<int>.SuccessResponse(id, "New draft version created.");
    }
}
