using SheikhTravelSystem.Application.Features.Fleet;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for fleet dashboard and related lists. SQL lives in Infrastructure.
/// </summary>
public interface IFleetRepository
{
    Task<FleetDashboardDto> GetDashboardAsync(int tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComplianceDocumentDto>> GetComplianceDocumentsAsync(
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InspectionDto>> GetInspectionsAsync(
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssignmentDto>> GetAssignmentsAsync(
        int tenantId,
        CancellationToken cancellationToken = default);
}
