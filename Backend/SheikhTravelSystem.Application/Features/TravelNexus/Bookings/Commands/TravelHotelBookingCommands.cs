using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using H = SheikhTravelSystem.Application.Features.TravelNexus.Bookings.TravelHotelStateMachine;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Bookings.Commands;

// ── Save (create / update) ──────────────────────────────────────────────

/// <summary>Adds (HotelBookingId null) or edits a hotel booking. A group file may link it to one of the group's stays.</summary>
public record SaveTravelHotelBookingCommand(int BookingId, int? HotelBookingId, SaveTravelHotelBookingDto Hotel) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => HotelBookingId.HasValue ? "UpdateHotel" : "AddHotel";
    public string AuditEntityName => "TravelBooking";
    public int? AuditEntityId => BookingId;
}

public class SaveTravelHotelBookingCommandValidator : AbstractValidator<SaveTravelHotelBookingCommand>
{
    public SaveTravelHotelBookingCommandValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.Hotel).NotNull();
        RuleFor(x => x.Hotel.HotelId).GreaterThan(0);
        RuleFor(x => x.Hotel.CheckOut).GreaterThan(x => x.Hotel.CheckIn).WithMessage("Check-out must be after check-in.");
        RuleFor(x => x.Hotel).Must(h => (h.CheckOut.Date - h.CheckIn.Date).TotalDays <= 90).WithMessage("A stay is at most 90 nights.");
        RuleFor(x => x.Hotel.MealPlan).Must(m => TravelValues.IsOneOf(m, TravelValues.MealPlans)).WithMessage("Unknown meal plan.");
        RuleFor(x => x.Hotel.ConfirmationNumber).MaximumLength(50);
        RuleFor(x => x.Hotel.SpecialRequests).MaximumLength(1000);
        RuleFor(x => x.Hotel.Rooms).NotEmpty().WithMessage("Book at least one room.")
            .Must(r => r.Select(x => x.RoomType).Distinct().Count() == r.Count).WithMessage("List each room type once.");
        RuleForEach(x => x.Hotel.Rooms).ChildRules(r =>
        {
            r.RuleFor(x => x.RoomType).Must(t => TravelValues.IsOneOf(t, TravelValues.RoomTypes)).WithMessage("Unknown room type.");
            r.RuleFor(x => x.Quantity).InclusiveBetween(1, 500);
        });
        RuleFor(x => x.Hotel.GuestTravelerIds).NotNull().Must(g => g.Distinct().Count() == g.Count).WithMessage("List each guest once.");
    }
}

public class SaveTravelHotelBookingCommandHandler(ITravelBookingRepository repository, TravelBookingGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<SaveTravelHotelBookingCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelHotelBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var file = await guard.GetVisibleAsync(request.BookingId, tenantId, cancellationToken);
        if (!TravelBookingStateMachine.IsOpen(file.Status)) return ApiResponse<int>.FailResponse(TravelBookingGuard.NotOpenMessage);

        var dto = request.Hotel with
        {
            CheckIn = request.Hotel.CheckIn.Date, CheckOut = request.Hotel.CheckOut.Date,
            ConfirmationNumber = string.IsNullOrWhiteSpace(request.Hotel.ConfirmationNumber) ? null : request.Hotel.ConfirmationNumber.Trim(),
            SpecialRequests = string.IsNullOrWhiteSpace(request.Hotel.SpecialRequests) ? null : request.Hotel.SpecialRequests.Trim(),
            CurrencyCode = string.IsNullOrWhiteSpace(request.Hotel.CurrencyCode) ? null : request.Hotel.CurrencyCode.Trim().ToUpperInvariant()
        };

        if (request.HotelBookingId is int hotelBookingId)
        {
            var hotel = await repository.GetHotelStateAsync(file.Id, hotelBookingId, tenantId, cancellationToken)
                        ?? throw new NotFoundException("TravelHotelBooking", hotelBookingId);
            if (!H.CanEdit(hotel.Status)) return ApiResponse<int>.FailResponse($"A {hotel.Status} hotel booking cannot be edited.");
            if (hotel.Status == "Confirmed" && dto.ConfirmationNumber is null) return ApiResponse<int>.FailResponse("A confirmed booking keeps its confirmation number.");
        }

        var amountError = TravelBookingRules.ValidateAmounts(dto.SellAmount, dto.CostAmount, dto.CurrencyCode);
        if (amountError is not null) return ApiResponse<int>.FailResponse(amountError);
        if (!await repository.HotelExistsAsync(tenantId, dto.HotelId, cancellationToken)) return ApiResponse<int>.FailResponse("Hotel not found.");
        if (dto.SupplierId is int supplierId && !await repository.SupplierExistsAsync(tenantId, supplierId, cancellationToken))
            return ApiResponse<int>.FailResponse("Supplier not found.");
        if (dto.GroupHotelId is int stayId && (file.GroupId is not int groupId || !await repository.GroupStayExistsAsync(tenantId, groupId, stayId, cancellationToken)))
            return ApiResponse<int>.FailResponse("The stay does not belong to this booking's group.");

        var eligible = (await repository.GetEligibleTravelersAsync(tenantId, file.CustomerId, file.GroupId, cancellationToken)).Select(e => e.TravelerId).ToHashSet();
        if (dto.GuestTravelerIds.Any(id => !eligible.Contains(id)))
            return ApiResponse<int>.FailResponse(file.GroupId is null ? "Guests must be travelers of the booking's customer." : "Guests must be active pilgrims of the group.");

        var id = await repository.SaveHotelAsync(tenantId, file.Id, request.HotelBookingId, dto, currentUser.UserId, cancellationToken);
        if (id == 0) throw new ConflictException(TravelBookingGuard.ConflictMessage);
        return ApiResponse<int>.SuccessResponse(id, request.HotelBookingId.HasValue ? "Hotel booking updated." : "Hotel booking added.");
    }
}

// ── Transition ──────────────────────────────────────────────────────────

public record TransitionTravelHotelBookingCommand(int BookingId, int HotelBookingId, TransitionTravelHotelDto Transition)
    : IRequest<ApiResponse<string>>, IAuditableCommand
{
    public string AuditAction => $"Hotel{Transition.Action}";
    public string AuditEntityName => "TravelBooking";
    public int? AuditEntityId => BookingId;
}

public class TransitionTravelHotelBookingCommandValidator : AbstractValidator<TransitionTravelHotelBookingCommand>
{
    public TransitionTravelHotelBookingCommandValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.HotelBookingId).GreaterThan(0);
        RuleFor(x => x.Transition.Action).Must(a => TravelValues.IsOneOf(a, H.TransitionActions)).WithMessage("Unknown action.");
        RuleFor(x => x.Transition.ConfirmationNumber).MaximumLength(50);
        RuleFor(x => x.Transition.Note).MaximumLength(500);
    }
}

public class TransitionTravelHotelBookingCommandHandler(ITravelBookingRepository repository, TravelBookingGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<TransitionTravelHotelBookingCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(TransitionTravelHotelBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var file = await guard.GetVisibleAsync(request.BookingId, tenantId, cancellationToken);
        if (!TravelBookingStateMachine.IsOpen(file.Status)) return ApiResponse<string>.FailResponse(TravelBookingGuard.NotOpenMessage);
        var hotel = await repository.GetHotelStateAsync(file.Id, request.HotelBookingId, tenantId, cancellationToken)
                    ?? throw new NotFoundException("TravelHotelBooking", request.HotelBookingId);

        var t = request.Transition;
        var target = H.Target(hotel.Status, t.Action);
        if (target is null) return ApiResponse<string>.FailResponse($"A {hotel.Status} hotel booking cannot be moved with '{t.Action}'.");
        var today = DateTime.UtcNow.Date;
        var confirmation = string.IsNullOrWhiteSpace(t.ConfirmationNumber) ? null : t.ConfirmationNumber.Trim();
        if (t.Action == H.Actions.Confirm && confirmation is null) return ApiResponse<string>.FailResponse("Enter the hotel's confirmation number.");
        if (t.Action is H.Actions.CheckIn or H.Actions.NoShow && today < hotel.CheckIn.Date)
            return ApiResponse<string>.FailResponse($"The stay starts on {hotel.CheckIn:dd MMM yyyy}.");

        if (!await repository.TransitionHotelAsync(tenantId, file.Id, hotel.Id, hotel.Status, target, confirmation, t.Note?.Trim(),
                currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelBookingGuard.ConflictMessage);
        return ApiResponse<string>.SuccessResponse(target, $"Hotel booking {target.ToLowerInvariant()}.");
    }
}

// ── Cancel / delete ─────────────────────────────────────────────────────

public record CancelTravelHotelBookingCommand(int BookingId, int HotelBookingId, CancelTravelServiceDto Cancellation) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "CancelHotel";
    public string AuditEntityName => "TravelBooking";
    public int? AuditEntityId => BookingId;
}

public class CancelTravelHotelBookingCommandValidator : AbstractValidator<CancelTravelHotelBookingCommand>
{
    public CancelTravelHotelBookingCommandValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.HotelBookingId).GreaterThan(0);
        RuleFor(x => x.Cancellation).NotNull().SetValidator(new CancelTravelServiceDtoValidator());
        RuleFor(x => x.Cancellation.TicketOutcome).Null().WithMessage("Ticket outcome applies to flights only.");
    }
}

public class CancelTravelHotelBookingCommandHandler(ITravelBookingRepository repository, TravelBookingGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<CancelTravelHotelBookingCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CancelTravelHotelBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var file = await guard.GetVisibleAsync(request.BookingId, tenantId, cancellationToken);
        if (!TravelBookingStateMachine.IsOpen(file.Status)) return ApiResponse<int>.FailResponse(TravelBookingGuard.NotOpenMessage);
        var hotel = await repository.GetHotelStateAsync(file.Id, request.HotelBookingId, tenantId, cancellationToken)
                    ?? throw new NotFoundException("TravelHotelBooking", request.HotelBookingId);
        if (!H.CanCancel(hotel.Status)) return ApiResponse<int>.FailResponse($"A {hotel.Status} hotel booking cannot be cancelled.");

        var error = TravelBookingRules.ValidateCancellation(request.Cancellation, hotel.SellAmount, hotel.CostAmount, hotel.CurrencyCode);
        if (error is not null) return ApiResponse<int>.FailResponse(error);

        var id = await repository.CancelHotelAsync(tenantId, file.Id, hotel.Id, hotel.Status,
            CancellationWrite.From(request.Cancellation, hotel.CurrencyCode), currentUser.UserId, cancellationToken)
            ?? throw new ConflictException(TravelBookingGuard.ConflictMessage);
        return ApiResponse<int>.SuccessResponse(id, "Hotel booking cancelled.");
    }
}

public record DeleteTravelHotelBookingCommand(int BookingId, int HotelBookingId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "DeleteHotel";
    public string AuditEntityName => "TravelBooking";
    public int? AuditEntityId => BookingId;
}

public class DeleteTravelHotelBookingCommandHandler(ITravelBookingRepository repository, TravelBookingGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<DeleteTravelHotelBookingCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelHotelBookingCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var file = await guard.GetVisibleAsync(request.BookingId, tenantId, cancellationToken);
        if (!TravelBookingStateMachine.IsOpen(file.Status)) return ApiResponse<bool>.FailResponse(TravelBookingGuard.NotOpenMessage);
        var hotel = await repository.GetHotelStateAsync(file.Id, request.HotelBookingId, tenantId, cancellationToken)
                    ?? throw new NotFoundException("TravelHotelBooking", request.HotelBookingId);
        if (hotel.Status != "Requested") return ApiResponse<bool>.FailResponse("Only a requested hotel booking can be removed — cancel it instead.");
        if (!await repository.DeleteHotelAsync(tenantId, file.Id, hotel.Id, currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelBookingGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Hotel booking removed.");
    }
}

// ── Refund tracking ─────────────────────────────────────────────────────

public record UpdateTravelRefundStatusCommand(int BookingId, int CancellationId, UpdateTravelRefundStatusDto Refund) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "RefundStatus";
    public string AuditEntityName => "TravelBooking";
    public int? AuditEntityId => BookingId;
}

public class UpdateTravelRefundStatusCommandValidator : AbstractValidator<UpdateTravelRefundStatusCommand>
{
    public UpdateTravelRefundStatusCommandValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.CancellationId).GreaterThan(0);
        RuleFor(x => x.Refund.RefundStatus).Must(s => TravelValues.IsOneOf(s, TravelValues.RefundStatuses)).WithMessage("Unknown refund status.");
        RuleFor(x => x.Refund.Note).MaximumLength(500);
    }
}

/// <summary>Moves refund tracking forward. Allowed on any file status: refunds often settle after the trip is closed.</summary>
public class UpdateTravelRefundStatusCommandHandler(ITravelBookingRepository repository, TravelBookingGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<UpdateTravelRefundStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTravelRefundStatusCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var file = await guard.GetVisibleAsync(request.BookingId, tenantId, cancellationToken);
        var cancellation = await repository.GetCancellationAsync(file.Id, request.CancellationId, tenantId, cancellationToken)
                           ?? throw new NotFoundException("TravelBookingCancellation", request.CancellationId);
        var next = request.Refund.RefundStatus;
        if (!TravelRefundStateMachine.Next(cancellation.RefundStatus).Contains(next))
            return ApiResponse<bool>.FailResponse($"Refund status cannot move from {cancellation.RefundStatus} to {next}.");
        if (!await repository.UpdateRefundStatusAsync(tenantId, file.Id, cancellation.Id, cancellation.RefundStatus, next,
                request.Refund.Note?.Trim(), currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelBookingGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Refund status updated.");
    }
}
