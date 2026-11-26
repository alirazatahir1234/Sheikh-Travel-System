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

/// <summary>Visibility check shared by group commands and queries (other tenants / branches → NotFound).</summary>
public sealed class TravelGroupGuard(ITravelGroupRepository groups)
{
    public const string ConflictMessage = "The group was changed by someone else. Reload and try again.";

    public async Task<TravelGroupState> GetVisibleAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken ct)
        => await groups.GetStateAsync(id, tenantId, scope, ct) ?? throw new NotFoundException("TravelGroup", id);
}

// ── Create ──────────────────────────────────────────────────────────────

/// <summary>Creates a Draft group from a published package; hotel stays are copied back-to-back from the departure date.</summary>
public record CreateTravelGroupCommand(CreateTravelGroupDto Group) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "TravelGroup";
    public int? AuditEntityId => null;
}

public class CreateTravelGroupCommandValidator : AbstractValidator<CreateTravelGroupCommand>
{
    public CreateTravelGroupCommandValidator()
    {
        RuleFor(x => x.Group).NotNull();
        RuleFor(x => x.Group.PackageId).GreaterThan(0);
        RuleFor(x => x.Group.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Group.Capacity).InclusiveBetween(1, 1000);
        RuleFor(x => x.Group.DepartureDate).Must(d => d.Date >= DateTime.UtcNow.Date).WithMessage("Departure cannot be in the past.");
        RuleFor(x => x.Group.Notes).MaximumLength(2000);
    }
}

public class CreateTravelGroupCommandHandler(ITravelGroupRepository repository, ITravelPackageRepository packages,
    ITravelCustomerRepository customers, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<CreateTravelGroupCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateTravelGroupCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Group with { Name = request.Group.Name.Trim(), DepartureDate = request.Group.DepartureDate.Date };
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        if (!scope.Allows(dto.BranchId)) throw new ForbiddenException("Branch is outside your data scope.");
        var refError = await customers.ValidateReferencesAsync(tenantId, null, dto.BranchId, null, cancellationToken);
        if (refError is not null) return ApiResponse<int>.FailResponse(refError);

        var package = await packages.GetByIdAsync(dto.PackageId, tenantId, cancellationToken) ?? throw new NotFoundException("TravelPackage", dto.PackageId);
        if (package.Status != "Published") return ApiResponse<int>.FailResponse("Groups can only be created from a published package.");

        var stays = TravelGroupPlanning.StaysFromPackage(package.Stays, dto.DepartureDate);
        var returnDate = dto.DepartureDate.AddDays(package.DurationNights);
        var id = await repository.CreateAsync(tenantId, dto, returnDate, stays, currentUser.UserId, DateTime.UtcNow, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Group created as draft.");
    }
}

/// <summary>Pure planning helpers.</summary>
public static class TravelGroupPlanning
{
    /// <summary>Package stays become consecutive date ranges starting on the departure date.</summary>
    public static IReadOnlyList<TravelGroupStayPlan> StaysFromPackage(IEnumerable<Packages.DTOs.TravelPackageStayDto> stays, DateTime departure)
    {
        var plans = new List<TravelGroupStayPlan>();
        var checkIn = departure.Date;
        var sequence = 1;
        foreach (var s in stays.OrderBy(s => s.Sequence))
        {
            plans.Add(new TravelGroupStayPlan(s.HotelId, checkIn, checkIn.AddDays(s.Nights), sequence++));
            checkIn = checkIn.AddDays(s.Nights);
        }
        return plans;
    }

    public static bool DatesOverlap(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) => aStart <= bEnd && bStart <= aEnd;
}

// ── Update ──────────────────────────────────────────────────────────────

/// <summary>Edits the group. Name, dates and capacity change only before visa processing; leader and notes any time before closure.</summary>
public record UpdateTravelGroupCommand(int Id, UpdateTravelGroupDto Group) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "TravelGroup";
    public int? AuditEntityId => Id;
}

public class UpdateTravelGroupCommandValidator : AbstractValidator<UpdateTravelGroupCommand>
{
    public UpdateTravelGroupCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Group).NotNull();
        RuleFor(x => x.Group.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Group.Capacity).InclusiveBetween(1, 1000);
        RuleFor(x => x.Group.ReturnDate).GreaterThanOrEqualTo(x => x.Group.DepartureDate).WithMessage("Return must be on or after departure.");
        RuleFor(x => x.Group.LeaderName).MaximumLength(100);
        RuleFor(x => x.Group.LeaderPhone).MaximumLength(30);
        RuleFor(x => x.Group.Notes).MaximumLength(2000);
    }
}

public class UpdateTravelGroupCommandHandler(ITravelGroupRepository repository, TravelGroupGuard guard, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<UpdateTravelGroupCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTravelGroupCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var state = await guard.GetVisibleAsync(request.Id, tenantId, scope, cancellationToken);
        if (TravelGroupStateMachine.IsTerminal(state.Status)) return ApiResponse<bool>.FailResponse($"A {state.Status} group cannot be edited.");

        var dto = request.Group with { Name = request.Group.Name.Trim(), DepartureDate = request.Group.DepartureDate.Date, ReturnDate = request.Group.ReturnDate.Date };
        var headerChanged = dto.Name != state.Name || dto.DepartureDate != state.DepartureDate.Date || dto.ReturnDate != state.ReturnDate.Date
                            || dto.Capacity != state.Capacity;
        if (headerChanged && !TravelGroupStateMachine.HeaderEditable.Contains(state.Status))
            return ApiResponse<bool>.FailResponse("Name, dates and capacity are locked once visa processing has started.");

        var members = await repository.GetMemberFactsAsync(request.Id, tenantId, cancellationToken);
        if (dto.Capacity < members.Count(m => m.Status != "Cancelled"))
            return ApiResponse<bool>.FailResponse("Capacity cannot be below the number of registered pilgrims.");
        if (dto.GroupLeaderTravelerId is int leader && !members.Any(m => m.TravelerId == leader && m.Status != "Cancelled"))
            return ApiResponse<bool>.FailResponse("The group leader must be an active pilgrim of this group (or enter a leader name instead).");

        if (!await repository.UpdateAsync(request.Id, tenantId, dto, [state.Status], currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelGroupGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Group updated.");
    }
}

// ── Transition ──────────────────────────────────────────────────────────

public record TransitionTravelGroupCommand(int Id, TransitionTravelGroupDto Transition) : IRequest<ApiResponse<string>>, IAuditableCommand
{
    public string AuditAction => Transition.Action;
    public string AuditEntityName => "TravelGroup";
    public int? AuditEntityId => Id;
}

public class TransitionTravelGroupCommandValidator : AbstractValidator<TransitionTravelGroupCommand>
{
    public TransitionTravelGroupCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Transition).NotNull();
        RuleFor(x => x.Transition.Action).Must(a => TravelValues.IsOneOf(a, TravelGroupStateMachine.TransitionActions)).WithMessage("Unknown action.");
        RuleFor(x => x.Transition.Note).NotEmpty().When(x => x.Transition.Action == A.Cancel).WithMessage("Give a reason for cancelling.");
        RuleFor(x => x.Transition.Note).MaximumLength(500);
    }
}

public class TransitionTravelGroupCommandHandler(
    ITravelGroupRepository repository,
    ITravelPackageRepository packages,
    ITravelConfigRepository config,
    ITravelVisaCaseRepository visaCases,
    TravelGroupGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<TransitionTravelGroupCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(TransitionTravelGroupCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var state = await guard.GetVisibleAsync(request.Id, tenantId, scope, cancellationToken);
        var action = request.Transition.Action;
        var target = TravelGroupStateMachine.Target(state.Status, action);
        if (target is null) return Fail($"Cannot {action} a group that is {state.Status}.");

        var members = await repository.GetMemberFactsAsync(request.Id, tenantId, cancellationToken);
        var active = members.Where(m => m.Status != "Cancelled").ToList();

        switch (action)
        {
            case A.Open:
            {
                var package = await packages.GetByIdAsync(state.PackageId, tenantId, cancellationToken);
                if (package?.Status != "Published") return Fail("The package must be published before opening registration.");
                if ((await repository.GetStaysAsync(request.Id, tenantId, cancellationToken)).Count == 0) return Fail("Add at least one hotel stay first.");
                break;
            }
            case A.CloseRegistration when active.Count == 0:
                return Fail("Register at least one pilgrim before closing registration.");
            case A.StartVisaProcessing:
                return await StartVisaProcessingAsync(state, active, tenantId, cancellationToken);
            case A.MarkReady:
            {
                var readiness = await ReadinessAsync(state, tenantId, cancellationToken);
                if (!readiness.IsReady)
                    return Fail(readiness switch
                    {
                        { UnconfirmedMembers: > 0 } => $"{readiness.UnconfirmedMembers} pilgrim(s) are not confirmed.",
                        { VisaCaseDecided: false } => "The group visa case has no approval yet.",
                        { MembersWithoutVisa: > 0 } => $"{readiness.MembersWithoutVisa} pilgrim(s) have no approved visa — cancel them or wait for approval.",
                        { MembersWithoutRoom: > 0 } => $"{readiness.MembersWithoutRoom} pilgrim(s) have no room in every hotel stay.",
                        { MembersWithoutTickets: > 0 } => $"{readiness.MembersWithoutTickets} pilgrim(s) have no issued ticket on an outbound and a return flight — ticket them in the group's booking.",
                        _ => "The group is not ready yet."
                    });
                break;
            }
            case A.Depart when DateTime.UtcNow.Date < state.DepartureDate.Date.AddDays(-1):
                return Fail($"The group departs on {state.DepartureDate:dd MMM yyyy}.");
        }

        var note = string.IsNullOrWhiteSpace(request.Transition.Note) ? null : request.Transition.Note.Trim();
        var change = new TravelGroupStatusChange(request.Id, state.Status, target, note, target == "Cancelled" ? note : null);
        if (!await repository.ChangeStatusAsync(tenantId, change, currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelGroupGuard.ConflictMessage);
        return ApiResponse<string>.SuccessResponse(target, $"Group is now {target}.");
    }

    private async Task<ApiResponse<string>> StartVisaProcessingAsync(TravelGroupState state, List<TravelGroupMemberFacts> active, int tenantId, CancellationToken ct)
    {
        if (active.Count == 0) return Fail("The group has no pilgrims.");
        if (active.Any(m => m.Status == "Registered")) return Fail("Confirm or cancel every registered pilgrim first.");

        var package = await packages.GetByIdAsync(state.PackageId, tenantId, ct);
        if (package?.VisaTypeId is not int visaTypeId) return Fail("The package has no visa type.");
        var visaType = await config.GetVisaTypeByIdAsync(visaTypeId, tenantId, ct);
        if (visaType is null || !visaType.IsActive) return Fail("The package's visa type is missing or inactive.");

        var caseId = await repository.StartVisaProcessingAsync(tenantId, state.Id, state.Status, visaType.Id, visaType.CountryCode,
            state.DepartureDate, currentUser.UserId, DateTime.UtcNow, ct)
            ?? throw new ConflictException(TravelGroupGuard.ConflictMessage);
        return ApiResponse<string>.SuccessResponse("VisaProcessing", $"Visa processing started (visa case #{caseId}).");
    }

    private async Task<TravelGroupReadinessDto> ReadinessAsync(TravelGroupState state, int tenantId, CancellationToken ct)
    {
        string? visaStatus = null;
        if (state.VisaCaseId is int caseId)
            visaStatus = (await visaCases.GetStateAsync(caseId, tenantId, TravelBranchScope.CompanyWide, ct))?.Status;
        return TravelGroupReadiness.Evaluate(
            await repository.GetMembersAsync(state.Id, tenantId, ct), visaStatus,
            await repository.GetStaysAsync(state.Id, tenantId, ct), await repository.GetRoomsAsync(state.Id, tenantId, ct),
            await repository.GetTicketFactsAsync(state.Id, tenantId, ct) ?? []);
    }

    private static ApiResponse<string> Fail(string message) => ApiResponse<string>.FailResponse(message);
}
