using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Groups.Queries;

// ── List / summary / by customer ────────────────────────────────────────

public class GetTravelGroupsQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelGroupListItemDto>>>
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int? PackageId { get; set; }
    /// <summary>Departure on or after (inclusive).</summary>
    public DateTime? DepartureFrom { get; set; }
    /// <summary>Departure on or before (inclusive).</summary>
    public DateTime? DepartureTo { get; set; }
}

public class GetTravelGroupsQueryValidator : AbstractValidator<GetTravelGroupsQuery>
{
    public GetTravelGroupsQueryValidator()
    {
        RuleFor(x => x.DepartureTo).GreaterThanOrEqualTo(x => x.DepartureFrom)
            .When(x => x.DepartureFrom.HasValue && x.DepartureTo.HasValue).WithMessage("'To' must be on or after 'From'.");
        RuleFor(x => x.Status).Must(s => TravelValues.IsOneOf(s, TravelValues.GroupStatuses)).When(x => !string.IsNullOrEmpty(x.Status));
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public class GetTravelGroupsQueryHandler(ITravelGroupRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelGroupsQuery, ApiResponse<PagedResult<TravelGroupListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelGroupListItemDto>>> Handle(GetTravelGroupsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var filter = new TravelGroupListFilter(string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            string.IsNullOrEmpty(request.Status) ? null : request.Status, request.PackageId, request.DepartureFrom?.Date, request.DepartureTo?.Date);
        return ApiResponse<PagedResult<TravelGroupListItemDto>>.SuccessResponse(
            await repository.GetPagedAsync(tenantId, scope, filter, request.Page, request.PageSize, cancellationToken));
    }
}

/// <summary>Current group count per status (zero-filled; Total = sum).</summary>
public record GetTravelGroupSummaryQuery : IRequest<ApiResponse<TravelGroupSummaryDto>>;

public class GetTravelGroupSummaryQueryHandler(ITravelGroupRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelGroupSummaryQuery, ApiResponse<TravelGroupSummaryDto>>
{
    public async Task<ApiResponse<TravelGroupSummaryDto>> Handle(GetTravelGroupSummaryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var counts = await repository.GetStatusCountsAsync(tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken);
        return ApiResponse<TravelGroupSummaryDto>.SuccessResponse(Build(counts));
    }

    public static TravelGroupSummaryDto Build(IReadOnlyDictionary<string, int> counts)
    {
        var filled = TravelValues.GroupStatuses.ToDictionary(s => s, s => counts.TryGetValue(s, out var n) ? n : 0);
        return new TravelGroupSummaryDto { Counts = filled, Total = filled.Values.Sum() };
    }
}

public record GetTravelGroupsByCustomerQuery(int CustomerId) : IRequest<ApiResponse<IReadOnlyList<TravelGroupListItemDto>>>;

public class GetTravelGroupsByCustomerQueryHandler(ITravelGroupRepository repository, ITravelCustomerRepository customers,
    ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelGroupsByCustomerQuery, ApiResponse<IReadOnlyList<TravelGroupListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelGroupListItemDto>>> Handle(GetTravelGroupsByCustomerQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var customer = await customers.GetRefAsync(request.CustomerId, tenantId, cancellationToken);
        if (customer is null || !scope.Allows(customer.BranchId)) throw new NotFoundException("TravelCustomer", request.CustomerId);
        return ApiResponse<IReadOnlyList<TravelGroupListItemDto>>.SuccessResponse(
            await repository.GetByCustomerAsync(request.CustomerId, tenantId, cancellationToken));
    }
}

// ── Detail ──────────────────────────────────────────────────────────────

public record GetTravelGroupByIdQuery(int Id) : IRequest<ApiResponse<TravelGroupDto>>;

public class GetTravelGroupByIdQueryHandler(ITravelGroupRepository repository, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<GetTravelGroupByIdQuery, ApiResponse<TravelGroupDto>>
{
    public async Task<ApiResponse<TravelGroupDto>> Handle(GetTravelGroupByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var group = await repository.GetByIdAsync(request.Id, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken)
            ?? throw new NotFoundException("TravelGroup", request.Id);

        var members = await repository.GetMembersAsync(group.Id, tenantId, cancellationToken);
        if (!currentUser.HasPermission(TravelPermissions.TravelerViewPii))
            foreach (var m in members) m.PassportNumber = TravelPii.Mask(m.PassportNumber);

        group.Members = members;
        group.Stays = await repository.GetStaysAsync(group.Id, tenantId, cancellationToken);
        group.Rooms = await repository.GetRoomsAsync(group.Id, tenantId, cancellationToken);
        group.Transport = await repository.GetTransportAsync(group.Id, tenantId, cancellationToken);
        group.History = await repository.GetHistoryAsync(group.Id, tenantId, cancellationToken);
        group.Readiness = TravelGroupReadiness.Evaluate(members, group.VisaCaseStatus, group.Stays, group.Rooms,
            await repository.GetTicketFactsAsync(group.Id, tenantId, cancellationToken) ?? []);
        group.AllowedActions = TravelGroupStateMachine.AllowedActions(group.Status);
        return ApiResponse<TravelGroupDto>.SuccessResponse(group);
    }
}

// ── Rooming suggestion ──────────────────────────────────────────────────

/// <summary>Suggested rooms for one stay (families together, others by gender). Nothing is saved.</summary>
public record SuggestTravelGroupRoomingQuery(int GroupId, int StayId) : IRequest<ApiResponse<IReadOnlyList<TravelRoomingRoomDto>>>;

public class SuggestTravelGroupRoomingQueryHandler(ITravelGroupRepository repository, TravelGroupGuard guard,
    ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<SuggestTravelGroupRoomingQuery, ApiResponse<IReadOnlyList<TravelRoomingRoomDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelRoomingRoomDto>>> Handle(SuggestTravelGroupRoomingQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetVisibleAsync(request.GroupId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken);
        if (!(await repository.GetStaysAsync(state.Id, tenantId, cancellationToken)).Any(s => s.Id == request.StayId))
            throw new NotFoundException("TravelGroupStay", request.StayId);
        return ApiResponse<IReadOnlyList<TravelRoomingRoomDto>>.SuccessResponse(
            TravelRoomAllocator.Suggest(await repository.GetMemberFactsAsync(state.Id, tenantId, cancellationToken)));
    }
}

// ── Manifest ────────────────────────────────────────────────────────────

/// <summary>
/// Pilgrim manifest (contains passport data). Requires Travel.Group.Manifest; passport numbers are masked without
/// Travel.Traveler.ViewPII. Audited <b>fail-closed</b> before any data is returned, and recorded in the group history.
/// </summary>
public record GetTravelGroupManifestQuery(int GroupId) : IRequest<ApiResponse<TravelGroupManifestDto>>;

public class GetTravelGroupManifestQueryHandler(
    ITravelGroupRepository repository,
    ITravelTravelerRepository travelers,
    IAuditEngine auditEngine,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<GetTravelGroupManifestQuery, ApiResponse<TravelGroupManifestDto>>
{
    public async Task<ApiResponse<TravelGroupManifestDto>> Handle(GetTravelGroupManifestQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var group = await repository.GetByIdAsync(request.GroupId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken)
            ?? throw new NotFoundException("TravelGroup", request.GroupId);

        var canViewPii = currentUser.HasPermission(TravelPermissions.TravelerViewPii);
        await auditEngine.RecordAsync(new AuditEventWrite(
            EventKey: AuditEventRegistrySeed.ResolveEventKey("TravelGroup", "Manifest"),
            EntityType: "TravelGroup",
            EntityId: group.Id,
            Action: "Manifest",
            Message: $"Generated pilgrim manifest for group {group.GroupNumber} ({(canViewPii ? "unmasked" : "masked")})",
            FailPolicy: AuditFailPolicy.FailClosed), cancellationToken);
        await repository.AddHistoryAsync(tenantId, group.Id, null, "ManifestGenerated", null, canViewPii ? "Unmasked" : "Masked", null,
            currentUser.UserId, cancellationToken);

        var members = (await repository.GetMembersAsync(group.Id, tenantId, cancellationToken)).Where(m => m.Status != "Cancelled").ToList();
        var stays = await repository.GetStaysAsync(group.Id, tenantId, cancellationToken);
        var rooms = await repository.GetRoomsAsync(group.Id, tenantId, cancellationToken);
        var details = (await travelers.GetByIdsAsync(members.Select(m => m.TravelerId).ToList(), tenantId, cancellationToken)).ToDictionary(t => t.Id);

        return ApiResponse<TravelGroupManifestDto>.SuccessResponse(new TravelGroupManifestDto
        {
            GroupNumber = group.GroupNumber,
            GroupName = group.Name,
            PackageName = group.PackageName,
            DepartureDate = group.DepartureDate,
            ReturnDate = group.ReturnDate,
            LeaderName = group.LeaderDisplayName,
            LeaderPhone = group.LeaderPhone,
            VisaCaseNumber = group.VisaCaseNumber,
            PiiMasked = !canViewPii,
            GeneratedAtUtc = DateTime.UtcNow,
            Stays = stays,
            Transport = await repository.GetTransportAsync(group.Id, tenantId, cancellationToken),
            Pilgrims = TravelGroupManifest.BuildPilgrims(members, details, stays, rooms, canViewPii)
        });
    }
}

/// <summary>Pure manifest row builder (ordered by booking customer, then surname).</summary>
public static class TravelGroupManifest
{
    public static IReadOnlyList<TravelManifestPilgrimDto> BuildPilgrims(IReadOnlyList<TravelGroupMemberDto> members,
        IReadOnlyDictionary<int, Travelers.DTOs.TravelTravelerDto> travelers, IReadOnlyList<TravelGroupStayDto> stays,
        IReadOnlyList<TravelGroupRoomDto> rooms, bool canViewPii)
    {
        var roomByStayMember = rooms.SelectMany(r => r.MemberIds.Select(m => (r.GroupHotelId, MemberId: m, Room: r)))
            .ToDictionary(x => (x.GroupHotelId, x.MemberId), x => x.Room);
        var byTraveler = members.ToDictionary(m => m.TravelerId);

        return members
            .OrderBy(m => m.CustomerName).ThenBy(m => travelers.TryGetValue(m.TravelerId, out var t) ? t.Surname : m.TravelerName)
            .Select((m, i) =>
            {
                travelers.TryGetValue(m.TravelerId, out var t);
                var mahram = t?.MahramTravelerId is int mid && byTraveler.TryGetValue(mid, out var g) ? g.TravelerName : null;
                return new TravelManifestPilgrimDto
                {
                    No = i + 1,
                    MemberId = m.Id,
                    Surname = t?.Surname ?? m.TravelerName,
                    GivenNames = t?.GivenNames ?? string.Empty,
                    Gender = m.Gender,
                    DateOfBirth = m.DateOfBirth,
                    NationalityCode = t?.NationalityCode,
                    PassportNumber = canViewPii ? m.PassportNumber : TravelPii.Mask(m.PassportNumber),
                    PassportExpiryDate = m.PassportExpiryDate,
                    VisaNumber = m.VisaNumber,
                    VisaExpiryDate = m.VisaExpiryDate,
                    MahramName = mahram,
                    Phone = t?.Phone,
                    CustomerName = m.CustomerName,
                    Rooms = stays.OrderBy(s => s.Sequence).Select(s => roomByStayMember.TryGetValue((s.Id, m.Id), out var r)
                        ? $"{s.City} · {s.HotelName} · {(string.IsNullOrWhiteSpace(r.RoomNumber) ? "room TBA" : "Room " + r.RoomNumber)} ({r.RoomType})"
                        : string.Empty).ToList()
                };
            })
            .ToList();
    }
}
