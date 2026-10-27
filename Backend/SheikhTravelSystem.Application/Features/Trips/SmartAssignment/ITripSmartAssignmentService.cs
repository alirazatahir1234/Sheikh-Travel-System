using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.Trips.SmartAssignment;

public interface ITripSmartAssignmentService
{
    Task<ApiResponse<TripSmartAssignmentSuggestionsDto>> GetSuggestionsAsync(
        int tripId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<TripSmartAssignmentApplyResultDto>> AutoAssignAsync(
        int tripId,
        bool fromTripCreation = false,
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
