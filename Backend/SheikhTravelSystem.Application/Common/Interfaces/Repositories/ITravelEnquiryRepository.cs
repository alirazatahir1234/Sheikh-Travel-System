using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Enquiries and their activities. Every statement is scoped by <c>tenantId</c>; list/get/summary also apply the
/// caller's branch scope. Status-changing methods compare the expected status and return false on a concurrent change.
/// </summary>
public interface ITravelEnquiryRepository
{
    Task<PagedResult<TravelEnquiryListItemDto>> GetPagedAsync(
        int tenantId, TravelBranchScope scope, TravelEnquiryListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<TravelEnquiryDto?> GetByIdAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelEnquiryActivityDto>> GetActivitiesAsync(int enquiryId, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Counts per status (only statuses with rows) and the number of open enquiries with a due follow-up.</summary>
    Task<(IReadOnlyDictionary<string, int> Counts, int FollowUpsDue)> GetSummaryAsync(
        int tenantId, TravelBranchScope scope, DateTime nowUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelEnquiryListItemDto>> GetByCustomerAsync(int customerId, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Allocates the enquiry number and inserts in one transaction.</summary>
    Task<int> CreateAsync(int tenantId, SaveTravelEnquiryDto dto, int? userId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Updates an open enquiry; false when missing or no longer open.</summary>
    Task<bool> UpdateAsync(int id, int tenantId, SaveTravelEnquiryDto dto, CancellationToken cancellationToken = default);

    Task<bool> ChangeStatusAsync(int tenantId, TravelEnquiryStatusChange change, int? userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts the activity, updates the enquiry's next follow-up, and (when <paramref name="promoteNewToContacted"/>)
    /// moves a New enquiry to Contacted with a StatusChange activity. Returns false when the enquiry is missing.
    /// </summary>
    Task<bool> LogActivityAsync(
        int tenantId, int enquiryId, LogTravelEnquiryActivityDto activity, DateTime occurredAt, bool promoteNewToContacted,
        int? userId, CancellationToken cancellationToken = default);

    /// <summary>One transaction: resolve/create customer, optional Draft visa case, mark Won. Null on concurrent change.</summary>
    Task<TravelEnquiryConversionResult?> ConvertAsync(
        int tenantId, TravelEnquiryConversionPlan plan, int? userId, DateTime nowUtc, CancellationToken cancellationToken = default);
}
