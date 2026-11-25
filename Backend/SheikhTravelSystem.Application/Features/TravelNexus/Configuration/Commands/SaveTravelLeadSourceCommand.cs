using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Commands;

/// <summary>Creates (Id null) or updates a lead source (walk-in, Facebook, referral…).</summary>
public record SaveTravelLeadSourceCommand(int? Id, SaveTravelLeadSourceDto LeadSource)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Id.HasValue ? "Update" : "Create";
    public string AuditEntityName => "TravelLeadSource";
    public int? AuditEntityId => Id;
}

public class SaveTravelLeadSourceCommandValidator : AbstractValidator<SaveTravelLeadSourceCommand>
{
    public SaveTravelLeadSourceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).When(x => x.Id.HasValue);
        RuleFor(x => x.LeadSource).NotNull();
        RuleFor(x => x.LeadSource.Name).NotEmpty().MaximumLength(100);
    }
}

public class SaveTravelLeadSourceCommandHandler(ITravelConfigRepository repository, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<SaveTravelLeadSourceCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelLeadSourceCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.LeadSource with { Name = request.LeadSource.Name.Trim() };

        if (await repository.LeadSourceNameExistsAsync(tenantId, dto.Name, request.Id, cancellationToken))
            return ApiResponse<int>.FailResponse("A lead source with this name already exists.");

        if (request.Id is int id)
        {
            if (!await repository.UpdateLeadSourceAsync(id, tenantId, dto, cancellationToken))
                throw new NotFoundException("TravelLeadSource", id);
            return ApiResponse<int>.SuccessResponse(id, "Lead source updated.");
        }

        var newId = await repository.CreateLeadSourceAsync(tenantId, dto, currentUser.UserId, cancellationToken);
        return ApiResponse<int>.SuccessResponse(newId, "Lead source created.");
    }
}
