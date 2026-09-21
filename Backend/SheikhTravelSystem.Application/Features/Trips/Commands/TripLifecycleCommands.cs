using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Trips.Commands;

public record UpdateTripStatusCommand(int Id, TripStatus Status, string? Note = null, string? CancellationReason = null)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UpdateStatus";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => Id;
}

public class UpdateTripStatusCommandValidator : AbstractValidator<UpdateTripStatusCommand>
{
    public UpdateTripStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class UpdateTripStatusCommandHandler(
    ITripRepository tripRepository,
    ITenantContext tenantContext,
    IServiceScopeFactory scopeFactory,
    ILogger<UpdateTripStatusCommandHandler> logger)
    : IRequestHandler<UpdateTripStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTripStatusCommand request, CancellationToken cancellationToken)
    {
        var result = await tripRepository.UpdateStatusAsync(
            request.Id, request.Status, request.Note, request.CancellationReason, cancellationToken);

        if (!result.Success)
            return ApiResponse<bool>.FailResponse(result.ErrorMessage!);

        logger.LogInformation("Trip {TripId} status {From} → {To}", request.Id, result.PreviousStatus, request.Status);

        // Notify after commit on a background scope so Accept/Arrived/Complete
        // stay under the driver-app 20s timeout while Email/Browser still fire.
        QueueTripStatusNotifications(
            tenantContext.GetRequiredTenantId(),
            request.Id,
            result.TripNumber,
            request.Status,
            request.Note,
            request.CancellationReason);

        return ApiResponse<bool>.SuccessResponse(true, $"Trip status updated to {request.Status}.");
    }

    private void QueueTripStatusNotifications(
        int tenantId,
        int tripId,
        string? tripNumber,
        TripStatus status,
        string? note,
        string? cancellationReason)
    {
        NotificationDecisionRequest? payload = status switch
        {
            TripStatus.Started => new(
                "trip_started",
                $"Trip Started: {tripNumber}",
                $"Trip {tripNumber} has started.",
                NotificationType.TripStarted,
                ReferenceId: tripId,
                TenantId: tenantId,
                SuggestedPriority: 2,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
                ]),
            TripStatus.Completed => new(
                "trip_completed",
                $"Trip Completed: {tripNumber}",
                $"Trip {tripNumber} has been completed.",
                NotificationType.TripCompleted,
                ReferenceId: tripId,
                TenantId: tenantId,
                SuggestedPriority: 2,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
                ]),
            TripStatus.Delayed => new(
                "trip_delayed",
                $"Trip Delayed: {tripNumber}",
                $"Trip {tripNumber} is marked delayed." + (string.IsNullOrWhiteSpace(note) ? "" : $" Note: {note}"),
                NotificationType.TripDelayed,
                ReferenceId: tripId,
                TenantId: tenantId,
                SuggestedPriority: 3,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
                ]),
            TripStatus.Cancelled => new(
                "trip_cancelled",
                $"Trip Cancelled: {tripNumber}",
                $"Trip {tripNumber} was cancelled." + (string.IsNullOrWhiteSpace(cancellationReason) ? "" : $" Reason: {cancellationReason}"),
                NotificationType.TripCancelled,
                ReferenceId: tripId,
                TenantId: tenantId,
                SuggestedPriority: 3,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
                ]),
            TripStatus.AtPickup => new(
                "trip_driver_arriving",
                $"Driver Arriving: {tripNumber}",
                $"Driver has arrived at pickup for trip {tripNumber}.",
                NotificationType.TripDriverArriving,
                ReferenceId: tripId,
                TenantId: tenantId,
                SuggestedPriority: 2,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
                ]),
            _ => null
        };

        if (payload is null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                var engine = scope.ServiceProvider.GetRequiredService<INotificationDecisionEngine>();
                await engine.DispatchIfAllowedAsync(payload, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background trip notification failed for trip {TripId} status {Status}", tripId, status);
            }
        });
    }
}

public record AssignTripDriverCommand(int TripId, int DriverId, int? AssistantDriverId = null, string? DriverNotes = null)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "AssignDriver";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => TripId;
}

public class AssignTripDriverCommandHandler(
    ITripRepository tripRepository,
    INotificationDecisionEngine decisionEngine)
    : IRequestHandler<AssignTripDriverCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AssignTripDriverCommand request, CancellationToken cancellationToken)
    {
        var result = await tripRepository.AssignDriverAsync(
            request.TripId, request.DriverId, request.AssistantDriverId, request.DriverNotes, cancellationToken);

        if (!result.Success)
            return ApiResponse<bool>.FailResponse(result.ErrorMessage!);

        var tripNumber = result.TripNumber;
        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            "trip_driver_assigned",
            $"Driver Assigned: {tripNumber}",
            $"A driver has been assigned to trip {tripNumber}.",
            NotificationType.TripDriverAssigned,
            ReferenceId: request.TripId,
            SuggestedPriority: 2,
            RequestedChannels:
            [
                NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
            ]), cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Driver assigned.");
    }
}

public record AssignTripVehicleCommand(int TripId, int VehicleId)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "AssignVehicle";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => TripId;
}

public class AssignTripVehicleCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<AssignTripVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AssignTripVehicleCommand request, CancellationToken cancellationToken)
    {
        var result = await tripRepository.AssignVehicleAsync(request.TripId, request.VehicleId, cancellationToken);
        if (!result.Success)
            return ApiResponse<bool>.FailResponse(result.ErrorMessage!);

        return ApiResponse<bool>.SuccessResponse(true, "Vehicle assigned.");
    }
}

public record CreateTripFromBookingCommand(int BookingId) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "CreateFromBooking";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => null;
}

public class CreateTripFromBookingCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<CreateTripFromBookingCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateTripFromBookingCommand request, CancellationToken cancellationToken)
    {
        var seed = await tripRepository.GetCreateFromBookingSeedAsync(request.BookingId, cancellationToken);

        if (seed.ExistingTripId is int existingId)
            return ApiResponse<int>.SuccessResponse(existingId, "Trip already exists for this booking.");

        var id = await tripRepository.CreateAsync(seed.Seed!, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Trip created successfully.");
    }
}
