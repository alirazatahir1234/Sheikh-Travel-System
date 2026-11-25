using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Commands;

public record UpdateTravelTravelerCommand(int Id, SaveTravelTravelerDto Traveler)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "TravelTraveler";
    public int? AuditEntityId => Id;
}

public class UpdateTravelTravelerCommandValidator : AbstractValidator<UpdateTravelTravelerCommand>
{
    public UpdateTravelTravelerCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Traveler).NotNull().SetValidator(new SaveTravelTravelerDtoValidator());
    }
}

public class UpdateTravelTravelerCommandHandler(
    ITravelTravelerRepository repository,
    ITravelConfigRepository config,
    TravelTravelerGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<UpdateTravelTravelerCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTravelTravelerCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var existing = await repository.GetIdentityAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("TravelTraveler", request.Id);
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        await guard.EnsureCustomerVisibleAsync(existing.CustomerId, tenantId, scope, cancellationToken);

        var dto = TravelTravelerNormalizer.Normalize(request.Traveler, existing);
        var error = await guard.ValidateAsync(tenantId, existing.CustomerId, request.Id, dto, cancellationToken);
        if (error is not null)
            return ApiResponse<bool>.FailResponse(error);

        if (!await repository.UpdateAsync(request.Id, tenantId, dto, cancellationToken))
            throw new NotFoundException("TravelTraveler", request.Id);

        var settings = await config.GetSettingsAsync(tenantId, cancellationToken);
        var status = TravelPii.PassportStatus(dto.PassportNumber, dto.PassportExpiryDate, DateTime.UtcNow, settings.PassportExpiryWarningDays);
        return ApiResponse<bool>.SuccessResponse(true, TravelTravelerMessages.Saved("Traveler updated.", status));
    }
}
