using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.Trips.SmartAssignment;

public interface ITripSmartAssignmentService
{
    Task<ApiResponse<TripSmartAssignmentSuggestionsDto>> GetSuggestionsAsync(
        int tripId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-apply best candidate. Blocked while policy is SuggestOnly unless <paramref name="allowWhenSuggestOnly"/>
    /// (used for driver-reject re-offer continuity).
    /// </summary>
    Task<ApiResponse<TripSmartAssignmentApplyResultDto>> AutoAssignAsync(
        int tripId,
        bool fromTripCreation = false,
        bool allowWhenSuggestOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>Staff confirms a ranked (or chosen) candidate — required path in SuggestOnly mode.</summary>
    Task<ApiResponse<TripSmartAssignmentApplyResultDto>> ConfirmAsync(
        int tripId,
        ConfirmTripSmartAssignmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<TripSmartAssignmentApplyResultDto>> ApplyOverrideAsync(
        int tripId,
        OverrideTripSmartAssignmentRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<TripSmartAssignmentDecisionDto>>> GetDecisionsAsync(
        int tripId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<TripSmartAssignmentDecisionDto>>> GetRecentDecisionsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// When a driver rejects a smart-assigned trip, re-offer the next best driver instead of cancelling the trip.
    /// Returns true when handled by the smart-assignment flow.
    /// </summary>
    Task<bool> TryReassignAfterDriverRejectAsync(
        int tripId,
        int driverId,
        string? reason,
        CancellationToken cancellationToken = default);
}
