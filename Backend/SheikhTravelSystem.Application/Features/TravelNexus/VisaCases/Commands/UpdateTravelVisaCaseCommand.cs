using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Commands;

public record UpdateTravelVisaCaseCommand(int Id, UpdateTravelVisaCaseDto VisaCase) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "TravelVisaCase";
    public int? AuditEntityId => Id;
}

public class UpdateTravelVisaCaseCommandValidator : AbstractValidator<UpdateTravelVisaCaseCommand>
{
    public UpdateTravelVisaCaseCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.VisaCase).NotNull();
        TravelVisaCaseRules.ApplyCommon(this, x => x.VisaCase.Priority, x => x.VisaCase.QuotedServiceFee, x => x.VisaCase.QuotedEmbassyFee,
            x => x.VisaCase.QuotedServiceFee.HasValue || x.VisaCase.QuotedEmbassyFee.HasValue || !string.IsNullOrEmpty(x.VisaCase.CurrencyCode),
            x => x.VisaCase.CurrencyCode, x => x.VisaCase.Notes);
    }
}

public class UpdateTravelVisaCaseCommandHandler(
    ITravelVisaCaseRepository repository,
    ITravelCustomerRepository customers,
    TravelVisaCaseGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<UpdateTravelVisaCaseCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTravelVisaCaseCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var state = await guard.GetVisibleAsync(request.Id, tenantId, scope, cancellationToken);
        if (VisaCaseStateMachine.IsTerminal(state.Status))
            return ApiResponse<bool>.FailResponse($"A {state.Status} case can no longer be edited.");

        var refError = await customers.ValidateReferencesAsync(tenantId, request.VisaCase.AssignedAgentUserId, null, null, cancellationToken);
        if (refError is not null) return ApiResponse<bool>.FailResponse(refError);

        var dto = request.VisaCase with
        {
            CurrencyCode = string.IsNullOrWhiteSpace(request.VisaCase.CurrencyCode) ? null : request.VisaCase.CurrencyCode,
            Notes = string.IsNullOrWhiteSpace(request.VisaCase.Notes) ? null : request.VisaCase.Notes.Trim(),
            IntendedTravelDate = request.VisaCase.IntendedTravelDate?.Date,
            ExpectedDecisionDate = request.VisaCase.ExpectedDecisionDate?.Date
        };
        if (!await repository.UpdateAsync(request.Id, tenantId, dto, cancellationToken))
            throw new ConflictException(TravelVisaCaseRules.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Visa case updated.");
    }
}
