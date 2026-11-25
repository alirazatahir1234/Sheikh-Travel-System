using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Documents;

/// <summary>Turns a checklist into per-applicant requirements (pure; see <see cref="TravelDocumentPlanner"/>).</summary>
public static class TravelChecklistPlanner
{
    /// <summary>
    /// One requirement per (traveler, document type). When several items for the same document type apply
    /// (different conditions), the requirement is mandatory if any is, takes the longest minimum validity and the
    /// first item's instructions.
    /// </summary>
    public static IReadOnlyList<TravelRequirementSeed> Plan(
        IReadOnlyList<TravelChecklistItemDto> items, IEnumerable<TravelApplicantProfile> applicants, DateTime referenceDate)
    {
        var seeds = new List<TravelRequirementSeed>();
        foreach (var applicant in applicants)
        {
            var applicable = items.Where(i => TravelChecklistRules.Applies(i.ApplicantCondition, applicant, referenceDate))
                .OrderBy(i => i.SortOrder)
                .GroupBy(i => i.DocumentTypeId);
            foreach (var g in applicable)
            {
                var first = g.First();
                var minValidity = g.Max(i => i.MinValidityDays);
                seeds.Add(new TravelRequirementSeed(applicant.TravelerId, g.Key, first.Id, g.Any(i => i.IsMandatory),
                    minValidity, first.Instructions));
            }
        }
        return seeds;
    }
}

/// <summary>Loads checklists and traveler profiles and plans the requirements for a visa case.</summary>
public sealed class TravelDocumentPlanner(ITravelChecklistRepository checklists, ITravelTravelerRepository travelers)
{
    /// <summary>For StartDocuments: the active checklist for the case's country/visa type (may be null).</summary>
    public async Task<(TravelChecklistSnapshotSource? Source, IReadOnlyList<TravelRequirementSeed> Seeds)> PlanForStartAsync(
        TravelVisaCaseState state, int tenantId, IReadOnlyCollection<int> travelerIds, CancellationToken ct)
    {
        var source = await checklists.GetActiveForAsync(tenantId, state.CountryCode, state.VisaTypeId, ct);
        if (source is null) return (null, []);
        return (source, await PlanAsync(source.Items, state, tenantId, travelerIds, ct));
    }

    /// <summary>For applicants added after documents started: the checklist version the case was built from.</summary>
    public async Task<IReadOnlyList<TravelRequirementSeed>> PlanForNewApplicantsAsync(
        TravelVisaCaseState state, int tenantId, IReadOnlyCollection<int> travelerIds, CancellationToken ct)
    {
        if (state.ChecklistTemplateId is not int templateId) return [];
        var items = await checklists.GetItemsAsync(templateId, tenantId, ct);
        return await PlanAsync(items, state, tenantId, travelerIds, ct);
    }

    private async Task<IReadOnlyList<TravelRequirementSeed>> PlanAsync(IReadOnlyList<TravelChecklistItemDto> items,
        TravelVisaCaseState state, int tenantId, IReadOnlyCollection<int> travelerIds, CancellationToken ct)
    {
        if (items.Count == 0 || travelerIds.Count == 0) return [];
        var profiles = (await travelers.GetByIdsAsync(travelerIds.ToList(), tenantId, ct))
            .Select(t => new TravelApplicantProfile(t.Id, t.Gender, t.DateOfBirth, t.MahramTravelerId));
        return TravelChecklistPlanner.Plan(items, profiles, (state.IntendedTravelDate ?? DateTime.UtcNow).Date);
    }
}
