using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Queries;

public record GetTravelVisaCaseByIdQuery(int Id) : IRequest<ApiResponse<TravelVisaCaseDto>>;

public class GetTravelVisaCaseByIdQueryHandler(
    ITravelVisaCaseRepository repository,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetTravelVisaCaseByIdQuery, ApiResponse<TravelVisaCaseDto>>
{
    public async Task<ApiResponse<TravelVisaCaseDto>> Handle(GetTravelVisaCaseByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var visaCase = await repository.GetByIdAsync(request.Id, tenantId, scope, cancellationToken)
            ?? throw new NotFoundException("TravelVisaCase", request.Id);

        var canViewPii = currentUser.HasPermission(TravelPermissions.TravelerViewPii);
        var applicants = await repository.GetApplicantsAsync(request.Id, tenantId, cancellationToken);
        foreach (var a in applicants)
        {
            a.PassportWarning = NeedsPassportWarning(a.PassportNumber, a.PassportExpiryDate, visaCase.IntendedTravelDate, DateTime.UtcNow);
            if (!canViewPii) a.PassportNumber = TravelPii.Mask(a.PassportNumber);
        }

        visaCase.Applicants = applicants;
        visaCase.History = await repository.GetHistoryAsync(request.Id, tenantId, cancellationToken);
        visaCase.AllowedActions = VisaCaseStateMachine.AllowedActions(visaCase.Status, visaCase.OnHoldFromStatus);
        return ApiResponse<TravelVisaCaseDto>.SuccessResponse(visaCase);
    }

    /// <summary>Most destinations require six months of passport validity beyond the travel date.</summary>
    public static bool NeedsPassportWarning(string? passportNumber, DateTime? passportExpiry, DateTime? travelDate, DateTime todayUtc)
    {
        if (string.IsNullOrWhiteSpace(passportNumber) || passportExpiry is null) return true;
        var reference = (travelDate ?? todayUtc).Date;
        return passportExpiry.Value.Date < reference.AddMonths(6);
    }
}
