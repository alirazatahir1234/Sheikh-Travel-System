using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Commands;

/// <summary>Creates (Id null) or updates a lost reason (price, chose competitor, visa refused…).</summary>
public record SaveTravelLostReasonCommand(int? Id, SaveTravelLostReasonDto LostReason)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Id.HasValue ? "Update" : "Create";
    public string AuditEntityName => "TravelLostReason";
    public int? AuditEntityId => Id;
}

public class SaveTravelLostReasonCommandValidator : AbstractValidator<SaveTravelLostReasonCommand>
{
    public SaveTravelLostReasonCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).When(x => x.Id.HasValue);
        RuleFor(x => x.LostReason).NotNull();
        RuleFor(x => x.LostReason.Name).NotEmpty().MaximumLength(100);
    }
}

public class SaveTravelLostReasonCommandHandler(ITravelConfigRepository repository, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<SaveTravelLostReasonCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelLostReasonCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.LostReason with { Name = request.LostReason.Name.Trim() };

        if (await repository.LostReasonNameExistsAsync(tenantId, dto.Name, request.Id, cancellationToken))
            return ApiResponse<int>.FailResponse("A lost reason with this name already exists.");

        if (request.Id is int id)
        {
            if (!await repository.UpdateLostReasonAsync(id, tenantId, dto, cancellationToken))
                throw new NotFoundException("TravelLostReason", id);
            return ApiResponse<int>.SuccessResponse(id, "Lost reason updated.");
        }

        var newId = await repository.CreateLostReasonAsync(tenantId, dto, currentUser.UserId, cancellationToken);
        return ApiResponse<int>.SuccessResponse(newId, "Lost reason created.");
    }
}
