using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Bookings.Queries;

// ── List / summary ──────────────────────────────────────────────────────

public class GetTravelBookingsQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelBookingListItemDto>>>
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    /// <summary>Customer | Group.</summary>
    public string? OwnerType { get; set; }
    public int? CustomerId { get; set; }
    public int? GroupId { get; set; }
    /// <summary>First travel date on or after (inclusive).</summary>
    public DateTime? TravelFrom { get; set; }
    /// <summary>First travel date on or before (inclusive).</summary>
    public DateTime? TravelTo { get; set; }
    /// <summary>Only files with a flight on hold whose ticketing limit is within 72 hours.</summary>
    public bool? TicketingDue { get; set; }
}

public class GetTravelBookingsQueryValidator : AbstractValidator<GetTravelBookingsQuery>
{
    public GetTravelBookingsQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => TravelValues.IsOneOf(s, TravelValues.BookingStatuses)).When(x => !string.IsNullOrEmpty(x.Status));
        RuleFor(x => x.OwnerType).Must(o => o is "Customer" or "Group").When(x => !string.IsNullOrEmpty(x.OwnerType));
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public class GetTravelBookingsQueryHandler(ITravelBookingRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelBookingsQuery, ApiResponse<PagedResult<TravelBookingListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelBookingListItemDto>>> Handle(GetTravelBookingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        // An inverted range is empty by definition — never fall back to unfiltered data.
        if (request.TravelFrom.HasValue && request.TravelTo.HasValue && request.TravelTo.Value.Date < request.TravelFrom.Value.Date)
            return ApiResponse<PagedResult<TravelBookingListItemDto>>.SuccessResponse(new PagedResult<TravelBookingListItemDto>
                { Items = [], TotalCount = 0, Page = request.Page, PageSize = request.PageSize });

        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var filter = new TravelBookingListFilter(string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            string.IsNullOrEmpty(request.Status) ? null : request.Status, string.IsNullOrEmpty(request.OwnerType) ? null : request.OwnerType,
            request.CustomerId, request.GroupId, request.TravelFrom?.Date, request.TravelTo?.Date)
        {
            TicketingDueBeforeUtc = request.TicketingDue == true ? DateTime.UtcNow.AddHours(72) : null
        };
        return ApiResponse<PagedResult<TravelBookingListItemDto>>.SuccessResponse(
            await repository.GetPagedAsync(tenantId, scope, filter, request.Page, request.PageSize, cancellationToken));
    }
}

/// <summary>Current booking files per status (zero-filled; Total = sum) and flights whose ticketing deadline is within 72 h.</summary>
public record GetTravelBookingSummaryQuery : IRequest<ApiResponse<TravelBookingSummaryDto>>;

public class GetTravelBookingSummaryQueryHandler(ITravelBookingRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelBookingSummaryQuery, ApiResponse<TravelBookingSummaryDto>>
{
    public async Task<ApiResponse<TravelBookingSummaryDto>> Handle(GetTravelBookingSummaryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var counts = await repository.GetStatusCountsAsync(tenantId, scope, cancellationToken);
        var due = await repository.CountTicketingDueAsync(tenantId, scope, DateTime.UtcNow.AddHours(72), cancellationToken);
        return ApiResponse<TravelBookingSummaryDto>.SuccessResponse(Build(counts, due));
    }

    public static TravelBookingSummaryDto Build(IReadOnlyDictionary<string, int> counts, int ticketingDue)
    {
        var filled = TravelValues.BookingStatuses.ToDictionary(s => s, s => counts.TryGetValue(s, out var n) ? n : 0);
        return new TravelBookingSummaryDto { Counts = filled, Total = filled.Values.Sum(), TicketingDueNext72Hours = ticketingDue };
    }
}

public record GetTravelBookingsByCustomerQuery(int CustomerId) : IRequest<ApiResponse<IReadOnlyList<TravelBookingListItemDto>>>;

public class GetTravelBookingsByCustomerQueryHandler(ITravelBookingRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelBookingsByCustomerQuery, ApiResponse<IReadOnlyList<TravelBookingListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelBookingListItemDto>>> Handle(GetTravelBookingsByCustomerQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        return ApiResponse<IReadOnlyList<TravelBookingListItemDto>>.SuccessResponse(await repository.GetByCustomerAsync(request.CustomerId, tenantId,
            await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken));
    }
}

public record GetTravelBookingsByGroupQuery(int GroupId) : IRequest<ApiResponse<IReadOnlyList<TravelBookingListItemDto>>>;

public class GetTravelBookingsByGroupQueryHandler(ITravelBookingRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelBookingsByGroupQuery, ApiResponse<IReadOnlyList<TravelBookingListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelBookingListItemDto>>> Handle(GetTravelBookingsByGroupQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        return ApiResponse<IReadOnlyList<TravelBookingListItemDto>>.SuccessResponse(await repository.GetByGroupAsync(request.GroupId, tenantId,
            await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken));
    }
}

// ── Detail ──────────────────────────────────────────────────────────────

public record GetTravelBookingByIdQuery(int Id) : IRequest<ApiResponse<TravelBookingDto>>;

public class GetTravelBookingByIdQueryHandler(ITravelBookingRepository repository, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext, ICurrentUserService currentUser) : IRequestHandler<GetTravelBookingByIdQuery, ApiResponse<TravelBookingDto>>
{
    public async Task<ApiResponse<TravelBookingDto>> Handle(GetTravelBookingByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await repository.GetStateAsync(request.Id, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken)
                    ?? throw new NotFoundException("TravelBooking", request.Id);
        var booking = await repository.GetDetailAsync(state.Id, tenantId, cancellationToken) ?? throw new NotFoundException("TravelBooking", request.Id);
        return ApiResponse<TravelBookingDto>.SuccessResponse(Decorate(booking, currentUser.HasPermission(TravelPermissions.TravelerViewPii), DateTime.UtcNow));
    }

    /// <summary>Masks passports, computes allowed actions, totals and next refund steps.</summary>
    public static TravelBookingDto Decorate(TravelBookingDto booking, bool canViewPii, DateTime utcNow)
    {
        var fileOpen = TravelBookingStateMachine.IsOpen(booking.Status);
        foreach (var f in booking.Flights)
        {
            f.AllowedActions = TravelFlightStateMachine.AllowedActions(f.Status, fileOpen);
            if (!canViewPii) foreach (var p in f.Passengers) p.PassportNumber = TravelPii.Mask(p.PassportNumber);
        }
        foreach (var h in booking.Hotels)
            h.AllowedActions = TravelHotelStateMachine.AllowedActions(h.Status, fileOpen, h.CheckIn, utcNow.Date);
        foreach (var c in booking.Cancellations) c.NextRefundStatuses = TravelRefundStateMachine.Next(c.RefundStatus);
        booking.AllowedActions = TravelBookingStateMachine.AllowedActions(booking.Status);
        booking.Totals = TravelBookingRules.Totals(booking.Flights, booking.Hotels);
        return booking;
    }
}
