using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using A = SheikhTravelSystem.Application.Features.TravelNexus.Bookings.TravelBookingStateMachine.Actions;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Bookings.Commands;

/// <summary>Visibility check shared by booking commands and queries (other tenants / branches → NotFound).</summary>
public sealed class TravelBookingGuard(ITravelBookingRepository bookings, ITravelBranchScopeResolver scopeResolver)
{
    public const string ConflictMessage = "The booking was changed by someone else. Reload and try again.";
    public const string NotOpenMessage = "The booking file is not open — reopen it to change services.";

    public async Task<TravelBookingState> GetVisibleAsync(int id, int tenantId, CancellationToken ct)
        => await bookings.GetStateAsync(id, tenantId, await scopeResolver.ResolveAsync(tenantId, ct), ct)
           ?? throw new NotFoundException("TravelBooking", id);
}

// ── Create ──────────────────────────────────────────────────────────────

/// <summary>Opens a booking file for a customer or an Umrah group.</summary>
public record CreateTravelBookingCommand(CreateTravelBookingDto Booking) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "TravelBooking";
    public int? AuditEntityId => null;
}

public class CreateTravelBookingCommandValidator : AbstractValidator<CreateTravelBookingCommand>
{
    public CreateTravelBookingCommandValidator()
    {
        RuleFor(x => x.Booking).NotNull();
        RuleFor(x => x.Booking).Must(b => b.CustomerId.HasValue ^ b.GroupId.HasValue).WithMessage("A booking belongs to either a customer or a group.");
        RuleFor(x => x.Booking.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Booking.Notes).MaximumLength(2000);
    }
}

public class CreateTravelBookingCommandHandler(ITravelBookingRepository repository, ITravelCustomerRepository customers,
    ITravelGroupRepository groups, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<CreateTravelBookingCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateTravelBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var dto = request.Booking with { Title = request.Booking.Title.Trim(), Notes = string.IsNullOrWhiteSpace(request.Booking.Notes) ? null : request.Booking.Notes.Trim() };

        if (dto.CustomerId is int customerId)
        {
            _ = await customers.GetByIdAsync(customerId, tenantId, scope, cancellationToken) ?? throw new NotFoundException("TravelCustomer", customerId);
        }
        else
        {
            var group = await groups.GetStateAsync(dto.GroupId!.Value, tenantId, scope, cancellationToken) ?? throw new NotFoundException("TravelGroup", dto.GroupId);
            if (group.Status is "Closed" or "Cancelled") return ApiResponse<int>.FailResponse($"The group is {group.Status}.");
            dto = dto with { BranchId = dto.BranchId ?? group.BranchId };
        }
        if (!scope.Allows(dto.BranchId)) throw new ForbiddenException("Branch is outside your data scope.");
        var refError = await customers.ValidateReferencesAsync(tenantId, dto.AssignedAgentUserId, dto.BranchId, null, cancellationToken)
                       ?? await LinkError(repository, tenantId, dto.EnquiryId, dto.VisaCaseId, dto.CustomerId, dto.GroupId, cancellationToken);
        if (refError is not null) return ApiResponse<int>.FailResponse(refError);

        var id = await repository.CreateAsync(tenantId, dto, currentUser.UserId, DateTime.UtcNow, cancellationToken);
        if (id == 0) return ApiResponse<int>.FailResponse("The customer or group is no longer available.");
        return ApiResponse<int>.SuccessResponse(id, "Booking file opened.");
    }

    internal static async Task<string?> LinkError(ITravelBookingRepository repository, int tenantId, int? enquiryId, int? visaCaseId,
        int? customerId, int? groupId, CancellationToken ct)
    {
        if (enquiryId is int e && (groupId is not null || !await repository.EnquiryBelongsAsync(tenantId, e, customerId, ct)))
            return "The enquiry does not belong to this customer.";
        if (visaCaseId is int v && !await repository.VisaCaseBelongsAsync(tenantId, v, customerId, groupId, ct))
            return "The visa case does not belong to this customer or group.";
        return null;
    }
}

// ── Update ──────────────────────────────────────────────────────────────

public record UpdateTravelBookingCommand(int Id, UpdateTravelBookingDto Booking) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "TravelBooking";
    public int? AuditEntityId => Id;
}

public class UpdateTravelBookingCommandValidator : AbstractValidator<UpdateTravelBookingCommand>
{
    public UpdateTravelBookingCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Booking).NotNull();
        RuleFor(x => x.Booking.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Booking.Notes).MaximumLength(2000);
    }
}

public class UpdateTravelBookingCommandHandler(ITravelBookingRepository repository, ITravelCustomerRepository customers, TravelBookingGuard guard,
    ITenantContext tenantContext) : IRequestHandler<UpdateTravelBookingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTravelBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetVisibleAsync(request.Id, tenantId, cancellationToken);
        if (!TravelBookingStateMachine.IsOpen(state.Status)) return ApiResponse<bool>.FailResponse(TravelBookingGuard.NotOpenMessage);
        var dto = request.Booking with { Title = request.Booking.Title.Trim(), Notes = string.IsNullOrWhiteSpace(request.Booking.Notes) ? null : request.Booking.Notes.Trim() };
        var refError = await customers.ValidateReferencesAsync(tenantId, dto.AssignedAgentUserId, null, null, cancellationToken)
                       ?? await CreateTravelBookingCommandHandler.LinkError(repository, tenantId, dto.EnquiryId, dto.VisaCaseId, state.CustomerId, state.GroupId, cancellationToken);
        if (refError is not null) return ApiResponse<bool>.FailResponse(refError);
        if (!await repository.UpdateAsync(request.Id, tenantId, dto, cancellationToken)) throw new NotFoundException("TravelBooking", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Booking updated.");
    }
}

// ── Transition ──────────────────────────────────────────────────────────

public record TransitionTravelBookingCommand(int Id, TransitionTravelBookingDto Transition) : IRequest<ApiResponse<string>>, IAuditableCommand
{
    public string AuditAction => $"Booking{Transition.Action}";
    public string AuditEntityName => "TravelBooking";
    public int? AuditEntityId => Id;
}

public class TransitionTravelBookingCommandValidator : AbstractValidator<TransitionTravelBookingCommand>
{
    public TransitionTravelBookingCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Transition.Action).Must(a => TravelValues.IsOneOf(a, TravelBookingStateMachine.TransitionActions)).WithMessage("Unknown action.");
        RuleFor(x => x.Transition.Note).NotEmpty().When(x => x.Transition.Action == A.Cancel).WithMessage("A reason is required to cancel.");
        RuleFor(x => x.Transition.Note).MaximumLength(500);
    }
}

public class TransitionTravelBookingCommandHandler(ITravelBookingRepository repository, TravelBookingGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<TransitionTravelBookingCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(TransitionTravelBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetVisibleAsync(request.Id, tenantId, cancellationToken);
        var action = request.Transition.Action;
        var target = TravelBookingStateMachine.Target(state.Status, action);
        if (target is null) return ApiResponse<string>.FailResponse($"A {state.Status} booking cannot be moved with '{action}'.");

        var now = DateTime.UtcNow;
        if (action is A.Complete or A.Cancel)
        {
            var facts = await repository.GetServiceFactsAsync(state.Id, tenantId, cancellationToken);
            var blocker = action == A.Complete ? TravelBookingRules.CompleteBlocker(facts, now) : TravelBookingRules.CancelBlocker(facts);
            if (blocker is not null) return ApiResponse<string>.FailResponse(blocker);
        }

        var note = request.Transition.Note?.Trim();
        if (!await repository.ChangeStatusAsync(tenantId, state.Id, state.Status, target, note, action == A.Cancel ? note : null,
                currentUser.UserId, now, cancellationToken))
            throw new ConflictException(TravelBookingGuard.ConflictMessage);
        return ApiResponse<string>.SuccessResponse(target, $"Booking {target.ToLowerInvariant()}.");
    }
}
