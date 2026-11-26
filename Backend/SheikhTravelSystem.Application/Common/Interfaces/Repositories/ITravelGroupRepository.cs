using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Umrah groups: pilgrims, stays, rooms, transport, history. Every statement is scoped by <c>tenantId</c>; list/get
/// apply the caller's branch scope. Status-dependent writes lock the group in its expected status and write history in
/// the same transaction; they return false/null on a concurrent change.
/// </summary>
public interface ITravelGroupRepository
{
    Task<PagedResult<TravelGroupListItemDto>> GetPagedAsync(int tenantId, TravelBranchScope scope, TravelGroupListFilter filter, int page, int pageSize,
        CancellationToken cancellationToken = default);
    Task<TravelGroupDto?> GetByIdAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<TravelGroupState?> GetStateAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelGroupListItemDto>> GetByCustomerAsync(int customerId, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Members with raw passport numbers and their decision on the group visa case.</summary>
    Task<IReadOnlyList<TravelGroupMemberDto>> GetMembersAsync(int groupId, int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelGroupMemberFacts>> GetMemberFactsAsync(int groupId, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Issued tickets on ticketed flights of the group's non-cancelled booking files (input of the ticket gate).</summary>
    Task<IReadOnlyList<Features.TravelNexus.Bookings.DTOs.TravelGroupTicketFact>> GetTicketFactsAsync(int groupId, int tenantId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetActiveMemberTravelerIdsAsync(int groupId, int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelGroupStayDto>> GetStaysAsync(int groupId, int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelGroupRoomDto>> GetRoomsAsync(int groupId, int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelGroupTransportDto>> GetTransportAsync(int groupId, int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelGroupHistoryDto>> GetHistoryAsync(int groupId, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Travelers (of <paramref name="travelerIds"/>) already active in another group whose dates overlap.</summary>
    Task<IReadOnlyList<int>> FindOverlappingTravelersAsync(int tenantId, IReadOnlyList<int> travelerIds, DateTime departure, DateTime returnDate,
        int excludeGroupId, CancellationToken cancellationToken = default);
    Task<bool> FleetBookingExistsAsync(int tenantId, int bookingId, CancellationToken cancellationToken = default);

    /// <summary>Creates the group (Draft, number allocated) with stays copied from the package plan.</summary>
    Task<int> CreateAsync(int tenantId, CreateTravelGroupDto dto, DateTime returnDate, IReadOnlyList<TravelGroupStayPlan> stays, int? userId,
        DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, int tenantId, UpdateTravelGroupDto dto, IReadOnlyList<string> editableStatuses, int? userId,
        CancellationToken cancellationToken = default);
    Task<bool> ChangeStatusAsync(int tenantId, TravelGroupStatusChange change, int? userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One transaction: opens the group visa case (number allocated, Draft) with the active pilgrims as applicants,
    /// links it to the group and moves the group from <paramref name="expectedStatus"/> to VisaProcessing.
    /// Returns the visa case id, or null on a concurrent change.
    /// </summary>
    Task<int?> StartVisaProcessingAsync(int tenantId, int groupId, string expectedStatus, int visaTypeId, string countryCode,
        DateTime travelDate, int? userId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Adds members while the group is Open and capacity allows (checked under lock). Null on conflict or full.</summary>
    Task<int?> RegisterMembersAsync(int tenantId, int groupId, IReadOnlyList<TravelGroupNewMember> members, int? userId,
        CancellationToken cancellationToken = default);
    Task<bool> ConfirmMemberAsync(int tenantId, int groupId, int memberId, int? userId, CancellationToken cancellationToken = default);
    /// <summary>Cancels the member and removes their room assignments.</summary>
    Task<bool> CancelMemberAsync(int tenantId, int groupId, int memberId, string reason, int? userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateMemberAsync(int tenantId, int groupId, int memberId, UpdateTravelGroupMemberDto dto, string? currencyCode,
        CancellationToken cancellationToken = default);

    Task<int?> SaveStayAsync(int tenantId, int groupId, int? stayId, SaveTravelGroupStayDto dto, CancellationToken cancellationToken = default);
    /// <summary>Soft-deletes the stay with its rooms and assignments.</summary>
    Task<bool> DeleteStayAsync(int tenantId, int groupId, int stayId, CancellationToken cancellationToken = default);
    /// <summary>Replaces all rooms and assignments of one stay in a transaction.</summary>
    Task<bool> SaveRoomingAsync(int tenantId, int groupId, int stayId, IReadOnlyList<TravelRoomingRoomDto> rooms, int? userId,
        CancellationToken cancellationToken = default);

    Task<int?> SaveTransportAsync(int tenantId, int groupId, int? legId, SaveTravelTransportLegDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteTransportAsync(int tenantId, int groupId, int legId, CancellationToken cancellationToken = default);

    Task AddHistoryAsync(int tenantId, int groupId, int? memberId, string eventType, string? fromValue, string? toValue, string? note, int? userId,
        CancellationToken cancellationToken = default);
}
