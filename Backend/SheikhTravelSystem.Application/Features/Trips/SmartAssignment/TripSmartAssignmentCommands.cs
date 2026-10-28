using MediatR;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.Trips.SmartAssignment;

public record GetTripSmartAssignmentSuggestionsQuery(int TripId)
    : IRequest<ApiResponse<TripSmartAssignmentSuggestionsDto>>;

public class GetTripSmartAssignmentSuggestionsQueryHandler(ITripSmartAssignmentService service)
    : IRequestHandler<GetTripSmartAssignmentSuggestionsQuery, ApiResponse<TripSmartAssignmentSuggestionsDto>>
{
    public Task<ApiResponse<TripSmartAssignmentSuggestionsDto>> Handle(
        GetTripSmartAssignmentSuggestionsQuery request, CancellationToken cancellationToken)
        => service.GetSuggestionsAsync(request.TripId, cancellationToken);
}

public record AutoAssignTripSmartCommand(int TripId) : IRequest<ApiResponse<TripSmartAssignmentApplyResultDto>>;

public class AutoAssignTripSmartCommandHandler(ITripSmartAssignmentService service)
    : IRequestHandler<AutoAssignTripSmartCommand, ApiResponse<TripSmartAssignmentApplyResultDto>>
{
    public Task<ApiResponse<TripSmartAssignmentApplyResultDto>> Handle(
        AutoAssignTripSmartCommand request, CancellationToken cancellationToken)
        => service.AutoAssignAsync(request.TripId, fromTripCreation: false, allowWhenSuggestOnly: false, cancellationToken);
}

public record ConfirmTripSmartAssignmentCommand(int TripId, ConfirmTripSmartAssignmentRequest Body)
    : IRequest<ApiResponse<TripSmartAssignmentApplyResultDto>>;

public class ConfirmTripSmartAssignmentCommandHandler(ITripSmartAssignmentService service)
    : IRequestHandler<ConfirmTripSmartAssignmentCommand, ApiResponse<TripSmartAssignmentApplyResultDto>>
{
    public Task<ApiResponse<TripSmartAssignmentApplyResultDto>> Handle(
        ConfirmTripSmartAssignmentCommand request, CancellationToken cancellationToken)
        => service.ConfirmAsync(request.TripId, request.Body, cancellationToken);
}

public record OverrideTripSmartAssignmentCommand(int TripId, OverrideTripSmartAssignmentRequest Body)
    : IRequest<ApiResponse<TripSmartAssignmentApplyResultDto>>;

public class OverrideTripSmartAssignmentCommandHandler(ITripSmartAssignmentService service)
    : IRequestHandler<OverrideTripSmartAssignmentCommand, ApiResponse<TripSmartAssignmentApplyResultDto>>
{
    public Task<ApiResponse<TripSmartAssignmentApplyResultDto>> Handle(
        OverrideTripSmartAssignmentCommand request, CancellationToken cancellationToken)
        => service.ApplyOverrideAsync(request.TripId, request.Body, cancellationToken);
}

public record GetTripSmartAssignmentDecisionsQuery(int TripId)
    : IRequest<ApiResponse<IReadOnlyList<TripSmartAssignmentDecisionDto>>>;

public class GetTripSmartAssignmentDecisionsQueryHandler(ITripSmartAssignmentService service)
    : IRequestHandler<GetTripSmartAssignmentDecisionsQuery, ApiResponse<IReadOnlyList<TripSmartAssignmentDecisionDto>>>
{
    public Task<ApiResponse<IReadOnlyList<TripSmartAssignmentDecisionDto>>> Handle(
        GetTripSmartAssignmentDecisionsQuery request, CancellationToken cancellationToken)
        => service.GetDecisionsAsync(request.TripId, cancellationToken);
}

public record GetRecentTripSmartAssignmentsQuery(int Limit = 50)
    : IRequest<ApiResponse<IReadOnlyList<TripSmartAssignmentDecisionDto>>>;

public class GetRecentTripSmartAssignmentsQueryHandler(ITripSmartAssignmentService service)
    : IRequestHandler<GetRecentTripSmartAssignmentsQuery, ApiResponse<IReadOnlyList<TripSmartAssignmentDecisionDto>>>
{
    public Task<ApiResponse<IReadOnlyList<TripSmartAssignmentDecisionDto>>> Handle(
        GetRecentTripSmartAssignmentsQuery request, CancellationToken cancellationToken)
        => service.GetRecentDecisionsAsync(request.Limit, cancellationToken);
}
