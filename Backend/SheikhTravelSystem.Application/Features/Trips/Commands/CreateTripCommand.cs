using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.Trips.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Trips.Commands;

public record CreateTripCommand(CreateTripDto Trip) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => null;
}

public class CreateTripCommandValidator : AbstractValidator<CreateTripCommand>
{
    public CreateTripCommandValidator()
    {
        RuleFor(x => x.Trip.TripName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Trip.CustomerId).GreaterThan(0);
        RuleFor(x => x.Trip.PassengerCount).GreaterThan(0);
        RuleFor(x => x.Trip.TripType).IsInEnum();
        RuleFor(x => x.Trip.Priority).IsInEnum();
        RuleFor(x => x.Trip.PlannedStart).NotEmpty();
    }
}

public class CreateTripCommandHandler(
    ITripRepository tripRepository,
    ILogger<CreateTripCommandHandler> logger)
    : IRequestHandler<CreateTripCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateTripCommand request, CancellationToken cancellationToken)
    {
        var id = await tripRepository.CreateAsync(request.Trip, cancellationToken);
        logger.LogInformation("Trip {TripId} created", id);
        return ApiResponse<int>.SuccessResponse(id, "Trip created successfully.");
    }
}

public record UpdateTripCommand(int Id, UpdateTripDto Trip) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => Id;
}

public class UpdateTripCommandValidator : AbstractValidator<UpdateTripCommand>
{
    public UpdateTripCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Trip.TripName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Trip.CustomerId).GreaterThan(0);
        RuleFor(x => x.Trip.PassengerCount).GreaterThan(0);
    }
}

public class UpdateTripCommandHandler(
    ITripRepository tripRepository,
    INotificationDecisionEngine decisionEngine)
    : IRequestHandler<UpdateTripCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTripCommand request, CancellationToken cancellationToken)
    {
        var result = await tripRepository.UpdateAsync(request.Id, request.Trip, cancellationToken);
        if (!result.Success)
            return ApiResponse<bool>.FailResponse(result.ErrorMessage!);

        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            "trip_updated",
            $"Trip Updated: {result.TripNumber}",
            $"Trip {result.TripNumber} details were updated.",
            NotificationType.TripUpdated,
            ReferenceId: request.Id,
            SuggestedPriority: 2,
            RequestedChannels:
            [
                NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
            ]), cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Trip updated.");
    }
}

public record DeleteTripCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => Id;
}

public class DeleteTripCommandHandler(ITripRepository tripRepository)
    : IRequestHandler<DeleteTripCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTripCommand request, CancellationToken cancellationToken)
    {
        var result = await tripRepository.SoftDeleteAsync(request.Id, cancellationToken);
        return result.Success
            ? ApiResponse<bool>.SuccessResponse(true, "Trip deleted.")
            : ApiResponse<bool>.FailResponse(result.ErrorMessage!);
    }
}
