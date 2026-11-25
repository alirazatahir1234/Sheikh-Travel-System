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

// ── Hotel stays ─────────────────────────────────────────────────────────

public record SaveTravelGroupStayCommand(int GroupId, int? StayId, SaveTravelGroupStayDto Stay) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => StayId.HasValue ? "UpdateStay" : "AddStay";
    public string AuditEntityName => "TravelGroup";
    public int? AuditEntityId => GroupId;
}

public class SaveTravelGroupStayCommandValidator : AbstractValidator<SaveTravelGroupStayCommand>
{
    public SaveTravelGroupStayCommandValidator()
    {
        RuleFor(x => x.Stay).NotNull();
        RuleFor(x => x.Stay.HotelId).GreaterThan(0);
        RuleFor(x => x.Stay.CheckOut).GreaterThan(x => x.Stay.CheckIn).WithMessage("Check-out must be after check-in.");
        RuleFor(x => x.Stay.ConfirmationNumber).MaximumLength(60);
    }
}

public class SaveTravelGroupStayCommandHandler(ITravelGroupRepository repository, ITravelHotelRepository hotels, TravelGroupGuard guard,
    ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<SaveTravelGroupStayCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelGroupStayCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetVisibleAsync(request.GroupId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken);
        if (!TravelGroupStateMachine.CanManageStays(state.Status)) return ApiResponse<int>.FailResponse($"Hotel stays are locked once the group is {state.Status}.");

        var dto = request.Stay with { CheckIn = request.Stay.CheckIn.Date, CheckOut = request.Stay.CheckOut.Date };
        if (dto.CheckIn < state.DepartureDate.Date || dto.CheckOut > state.ReturnDate.Date)
            return ApiResponse<int>.FailResponse("The stay must fall within the group's travel dates.");
        var hotel = await hotels.GetByIdAsync(dto.HotelId, tenantId, cancellationToken);
        if (hotel is null || !hotel.IsActive) return ApiResponse<int>.FailResponse("Choose an active hotel from the catalog.");

        var id = await repository.SaveStayAsync(tenantId, state.Id, request.StayId, dto, cancellationToken)
            ?? throw new NotFoundException("TravelGroupStay", request.StayId ?? 0);
        return ApiResponse<int>.SuccessResponse(id, "Hotel stay saved.");
    }
}

public record DeleteTravelGroupStayCommand(int GroupId, int StayId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "DeleteStay";
    public string AuditEntityName => "TravelGroup";
    public int? AuditEntityId => GroupId;
}

public class DeleteTravelGroupStayCommandHandler(ITravelGroupRepository repository, TravelGroupGuard guard, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelGroupStayCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelGroupStayCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetVisibleAsync(request.GroupId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken);
        if (!TravelGroupStateMachine.CanManageStays(state.Status)) return ApiResponse<bool>.FailResponse($"Hotel stays are locked once the group is {state.Status}.");
        if (!await repository.DeleteStayAsync(tenantId, state.Id, request.StayId, cancellationToken)) throw new NotFoundException("TravelGroupStay", request.StayId);
        return ApiResponse<bool>.SuccessResponse(true, "Hotel stay removed (with its rooms).");
    }
}

// ── Rooming ─────────────────────────────────────────────────────────────

/// <summary>Replaces the rooms and assignments of one stay after validating capacity, gender/family rules and one bed per pilgrim.</summary>
public record SaveTravelGroupRoomingCommand(int GroupId, int StayId, SaveTravelStayRoomingDto Rooming) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "SaveRooming";
    public string AuditEntityName => "TravelGroup";
    public int? AuditEntityId => GroupId;
}

public class SaveTravelGroupRoomingCommandValidator : AbstractValidator<SaveTravelGroupRoomingCommand>
{
    public SaveTravelGroupRoomingCommandValidator()
    {
        RuleFor(x => x.Rooming).NotNull();
        RuleFor(x => x.Rooming.Rooms).NotNull().Must(r => r.Count <= 500).WithMessage("Too many rooms.");
        RuleForEach(x => x.Rooming.Rooms).ChildRules(r => r.RuleFor(i => i.RoomNumber).MaximumLength(20));
    }
}

public class SaveTravelGroupRoomingCommandHandler(ITravelGroupRepository repository, TravelGroupGuard guard, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<SaveTravelGroupRoomingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(SaveTravelGroupRoomingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetVisibleAsync(request.GroupId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken);
        if (!TravelGroupStateMachine.CanManageRooms(state.Status)) return ApiResponse<bool>.FailResponse($"Rooms cannot be changed once the group is {state.Status}.");
        if (!(await repository.GetStaysAsync(state.Id, tenantId, cancellationToken)).Any(s => s.Id == request.StayId))
            throw new NotFoundException("TravelGroupStay", request.StayId);

        var rooms = request.Rooming.Rooms.Where(r => r.MemberIds.Count > 0 || !string.IsNullOrWhiteSpace(r.RoomNumber)).ToList();
        var error = TravelRoomAllocator.Validate(rooms, await repository.GetMemberFactsAsync(state.Id, tenantId, cancellationToken));
        if (error is not null) return ApiResponse<bool>.FailResponse(error);

        if (!await repository.SaveRoomingAsync(tenantId, state.Id, request.StayId, rooms, currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelGroupGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Rooming saved.");
    }
}

// ── Transport ───────────────────────────────────────────────────────────

public record SaveTravelGroupTransportCommand(int GroupId, int? LegId, SaveTravelTransportLegDto Leg) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => LegId.HasValue ? "UpdateTransport" : "AddTransport";
    public string AuditEntityName => "TravelGroup";
    public int? AuditEntityId => GroupId;
}

public class SaveTravelGroupTransportCommandValidator : AbstractValidator<SaveTravelGroupTransportCommand>
{
    public SaveTravelGroupTransportCommandValidator()
    {
        RuleFor(x => x.Leg).NotNull();
        RuleFor(x => x.Leg.LegType).Must(t => TravelValues.IsOneOf(t, TravelValues.TransportLegTypes)).WithMessage("Unknown leg type.");
        RuleFor(x => x.Leg.FromPlace).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Leg.ToPlace).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Leg.ProviderName).MaximumLength(150);
        RuleFor(x => x.Leg.VehicleType).MaximumLength(60);
        RuleFor(x => x.Leg.Seats).InclusiveBetween(1, 1000).When(x => x.Leg.Seats.HasValue);
        RuleFor(x => x.Leg.FleetBookingId).GreaterThan(0).When(x => x.Leg.FleetBookingId.HasValue);
        RuleFor(x => x.Leg.Notes).MaximumLength(500);
    }
}

public class SaveTravelGroupTransportCommandHandler(ITravelGroupRepository repository, TravelGroupGuard guard, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<SaveTravelGroupTransportCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelGroupTransportCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetVisibleAsync(request.GroupId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken);
        if (!TravelGroupStateMachine.CanManageTransport(state.Status)) return ApiResponse<int>.FailResponse($"Transport is locked once the group is {state.Status}.");
        if (request.Leg.FleetBookingId is int bookingId && !await repository.FleetBookingExistsAsync(tenantId, bookingId, cancellationToken))
            return ApiResponse<int>.FailResponse("Fleet booking not found.");

        var id = await repository.SaveTransportAsync(tenantId, state.Id, request.LegId, request.Leg, currentUser.UserId, cancellationToken)
            ?? throw new NotFoundException("TravelGroupTransport", request.LegId ?? 0);
        return ApiResponse<int>.SuccessResponse(id, "Transport saved.");
    }
}

public record DeleteTravelGroupTransportCommand(int GroupId, int LegId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "DeleteTransport";
    public string AuditEntityName => "TravelGroup";
    public int? AuditEntityId => GroupId;
}

public class DeleteTravelGroupTransportCommandHandler(ITravelGroupRepository repository, TravelGroupGuard guard, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelGroupTransportCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelGroupTransportCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetVisibleAsync(request.GroupId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken);
        if (!TravelGroupStateMachine.CanManageTransport(state.Status)) return ApiResponse<bool>.FailResponse($"Transport is locked once the group is {state.Status}.");
        if (!await repository.DeleteTransportAsync(tenantId, state.Id, request.LegId, cancellationToken)) throw new NotFoundException("TravelGroupTransport", request.LegId);
        return ApiResponse<bool>.SuccessResponse(true, "Transport leg removed.");
    }
}
