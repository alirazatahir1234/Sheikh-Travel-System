using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.DTOs;
using A = SheikhTravelSystem.Application.Features.TravelNexus.Groups.TravelGroupStateMachine.Actions;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Groups.Commands;

// ── Register ────────────────────────────────────────────────────────────

/// <summary>Registers travelers of one customer as pilgrims (group Open; capacity; no overlapping group). Quoted price from the package.</summary>
public record RegisterTravelGroupMembersCommand(int GroupId, RegisterTravelGroupMembersDto Registration) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "RegisterPilgrims";
    public string AuditEntityName => "TravelGroup";
    public int? AuditEntityId => GroupId;
}

public class RegisterTravelGroupMembersCommandValidator : AbstractValidator<RegisterTravelGroupMembersCommand>
{
    public RegisterTravelGroupMembersCommandValidator()
    {
        RuleFor(x => x.GroupId).GreaterThan(0);
        RuleFor(x => x.Registration).NotNull();
        RuleFor(x => x.Registration.CustomerId).GreaterThan(0);
        RuleFor(x => x.Registration.Members).NotEmpty().Must(m => m.Count <= 100)
            .Must(m => m.Select(i => i.TravelerId).Distinct().Count() == m.Count).WithMessage("Each traveler can be registered once.");
        RuleForEach(x => x.Registration.Members).ChildRules(m =>
        {
            m.RuleFor(i => i.TravelerId).GreaterThan(0);
            m.RuleFor(i => i.RoomPreference).Must(r => TravelValues.IsOneOf(r, TravelValues.RoomTypes)).WithMessage("Unknown room type.");
            m.RuleFor(i => i.PriceCategory).Must(c => TravelValues.IsOneOf(c, TravelValues.PriceCategories)).WithMessage("Unknown price category.");
        });
    }
}

public class RegisterTravelGroupMembersCommandHandler(
    ITravelGroupRepository repository, ITravelPackageRepository packages, ITravelCustomerRepository customers, ITravelTravelerRepository travelers,
    TravelGroupGuard guard, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<RegisterTravelGroupMembersCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(RegisterTravelGroupMembersCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var state = await guard.GetVisibleAsync(request.GroupId, tenantId, scope, cancellationToken);
        if (!TravelGroupStateMachine.CanRegister(state.Status)) return ApiResponse<int>.FailResponse("Pilgrims can be registered only while the group is open.");

        var dto = request.Registration;
        var customer = await customers.GetRefAsync(dto.CustomerId, tenantId, cancellationToken);
        if (customer is null || !scope.Allows(customer.BranchId)) throw new NotFoundException("TravelCustomer", dto.CustomerId);

        var own = (await travelers.GetByCustomerAsync(dto.CustomerId, tenantId, cancellationToken)).Select(t => t.Id).ToHashSet();
        if (dto.Members.Any(m => !own.Contains(m.TravelerId)))
            return ApiResponse<int>.FailResponse("Only travelers of the selected customer can be registered.");

        var ids = dto.Members.Select(m => m.TravelerId).ToList();
        var already = (await repository.GetActiveMemberTravelerIdsAsync(state.Id, tenantId, cancellationToken)).ToHashSet();
        if (ids.Any(already.Contains)) return ApiResponse<int>.FailResponse("A traveler is already registered in this group.");
        var clashes = await repository.FindOverlappingTravelersAsync(tenantId, ids, state.DepartureDate, state.ReturnDate, state.Id, cancellationToken);
        if (clashes.Count > 0) return ApiResponse<int>.FailResponse($"{clashes.Count} traveler(s) are already in another group on overlapping dates.");

        var package = await packages.GetByIdAsync(state.PackageId, tenantId, cancellationToken) ?? throw new NotFoundException("TravelPackage", state.PackageId);
        var members = new List<TravelGroupNewMember>();
        foreach (var m in dto.Members)
        {
            var price = package.Prices.FirstOrDefault(p => p.RoomType == m.RoomPreference);
            if (price is null) return ApiResponse<int>.FailResponse($"The package has no {m.RoomPreference} price.");
            var quoted = m.PriceCategory switch { "Child" => price.ChildPrice, "Infant" => price.InfantPrice, _ => price.AdultPrice };
            members.Add(new TravelGroupNewMember(m.TravelerId, dto.CustomerId, m.RoomPreference, m.PriceCategory, quoted, package.CurrencyCode));
        }

        var added = await repository.RegisterMembersAsync(tenantId, state.Id, members, currentUser.UserId, cancellationToken)
            ?? throw new ConflictException("The group is full or no longer open. Reload and try again.");
        return ApiResponse<int>.SuccessResponse(added, $"{added} pilgrim(s) registered.");
    }
}

// ── Confirm / update / cancel ───────────────────────────────────────────

public record ConfirmTravelGroupMemberCommand(int GroupId, int MemberId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "ConfirmPilgrim";
    public string AuditEntityName => "TravelGroup";
    public int? AuditEntityId => GroupId;
}

public class ConfirmTravelGroupMemberCommandHandler(ITravelGroupRepository repository, TravelGroupGuard guard, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<ConfirmTravelGroupMemberCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ConfirmTravelGroupMemberCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetVisibleAsync(request.GroupId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken);
        if (state.Status is not ("Open" or "RegistrationClosed"))
            return ApiResponse<bool>.FailResponse("Pilgrims can be confirmed while registration is open or just closed.");
        if (!await repository.ConfirmMemberAsync(tenantId, state.Id, request.MemberId, currentUser.UserId, cancellationToken))
            throw new NotFoundException("TravelGroupMember", request.MemberId);
        return ApiResponse<bool>.SuccessResponse(true, "Pilgrim confirmed.");
    }
}

public record UpdateTravelGroupMemberCommand(int GroupId, int MemberId, UpdateTravelGroupMemberDto Member) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UpdatePilgrim";
    public string AuditEntityName => "TravelGroup";
    public int? AuditEntityId => GroupId;
}

public class UpdateTravelGroupMemberCommandValidator : AbstractValidator<UpdateTravelGroupMemberCommand>
{
    public UpdateTravelGroupMemberCommandValidator()
    {
        RuleFor(x => x.Member).NotNull();
        RuleFor(x => x.Member.RoomPreference).Must(r => TravelValues.IsOneOf(r, TravelValues.RoomTypes)).WithMessage("Unknown room type.");
        RuleFor(x => x.Member.PriceCategory).Must(c => TravelValues.IsOneOf(c, TravelValues.PriceCategories)).WithMessage("Unknown price category.");
        RuleFor(x => x.Member.QuotedPrice).GreaterThanOrEqualTo(0).When(x => x.Member.QuotedPrice.HasValue);
    }
}

public class UpdateTravelGroupMemberCommandHandler(ITravelGroupRepository repository, ITravelPackageRepository packages, TravelGroupGuard guard,
    ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<UpdateTravelGroupMemberCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTravelGroupMemberCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetVisibleAsync(request.GroupId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken);
        if (!TravelGroupStateMachine.CanManageMembers(state.Status)) return ApiResponse<bool>.FailResponse($"Pilgrims cannot be changed once the group is {state.Status}.");
        var package = await packages.GetByIdAsync(state.PackageId, tenantId, cancellationToken);
        if (!await repository.UpdateMemberAsync(tenantId, state.Id, request.MemberId, request.Member, package?.CurrencyCode, cancellationToken))
            throw new NotFoundException("TravelGroupMember", request.MemberId);
        return ApiResponse<bool>.SuccessResponse(true, "Pilgrim updated.");
    }
}

/// <summary>
/// Cancels a pilgrim (reason required) and frees their beds. When the group visa case exists the pilgrim is also
/// taken off it: removed before submission, recorded as Withdrawn after. The visa update is a second step; if it
/// cannot be applied the response says so and the visa case must be updated manually.
/// </summary>
public record CancelTravelGroupMemberCommand(int GroupId, int MemberId, CancelTravelGroupMemberDto Cancellation) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "CancelPilgrim";
    public string AuditEntityName => "TravelGroup";
    public int? AuditEntityId => GroupId;
}

public class CancelTravelGroupMemberCommandValidator : AbstractValidator<CancelTravelGroupMemberCommand>
{
    public CancelTravelGroupMemberCommandValidator()
    {
        RuleFor(x => x.Cancellation).NotNull();
        RuleFor(x => x.Cancellation.Reason).NotEmpty().WithMessage("Give a reason.").MaximumLength(500);
    }
}

public class CancelTravelGroupMemberCommandHandler(ITravelGroupRepository repository, ITravelVisaCaseRepository visaCases, TravelGroupGuard guard,
    ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<CancelTravelGroupMemberCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(CancelTravelGroupMemberCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetVisibleAsync(request.GroupId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken);
        if (!TravelGroupStateMachine.CanManageMembers(state.Status)) return ApiResponse<bool>.FailResponse($"Pilgrims cannot be cancelled once the group is {state.Status}.");

        var member = (await repository.GetMemberFactsAsync(state.Id, tenantId, cancellationToken)).FirstOrDefault(m => m.MemberId == request.MemberId)
            ?? throw new NotFoundException("TravelGroupMember", request.MemberId);
        if (member.Status == "Cancelled") return ApiResponse<bool>.FailResponse("The pilgrim is already cancelled.");

        var reason = request.Cancellation.Reason.Trim();
        if (!await repository.CancelMemberAsync(tenantId, state.Id, request.MemberId, reason, currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelGroupGuard.ConflictMessage);

        var visaNote = await WithdrawFromVisaCaseAsync(state, member.TravelerId, reason, tenantId, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Pilgrim cancelled." + visaNote);
    }

    private async Task<string> WithdrawFromVisaCaseAsync(TravelGroupState state, int travelerId, string reason, int tenantId, CancellationToken ct)
    {
        if (state.VisaCaseId is not int caseId) return string.Empty;
        var visa = await visaCases.GetStateAsync(caseId, tenantId, TravelBranchScope.CompanyWide, ct);
        if (visa is null || VisaCaseStateMachine.IsTerminal(visa.Status)) return string.Empty;
        var applicant = (await visaCases.GetApplicantsAsync(caseId, tenantId, ct)).FirstOrDefault(a => a.TravelerId == travelerId);
        if (applicant is null || applicant.Decision == "Withdrawn") return string.Empty;

        var ok = VisaCaseStateMachine.IsPreSubmission(visa.Status)
            ? await visaCases.RemoveApplicantAsync(tenantId, caseId, applicant.Id, visa.Status, currentUser.UserId, ct)
            : VisaCaseStateMachine.AcceptsDecisions(visa.Status)
              && (await visaCases.RecordDecisionAsync(tenantId, caseId, applicant.Id, visa.Status,
                  new VisaCases.DTOs.RecordTravelVisaDecisionDto("Withdrawn", null, null, null, null, null, $"Pilgrim cancelled: {reason}"),
                  currentUser.UserId, DateTime.UtcNow, ct)).Applied;
        return ok ? " Removed from the group visa case." : " The group visa case could not be updated — update it manually.";
    }
}
