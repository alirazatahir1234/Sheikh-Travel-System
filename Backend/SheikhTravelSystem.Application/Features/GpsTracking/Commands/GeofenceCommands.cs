using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record CreateGeofenceCommand(CreateGeofenceDto Geofence) : IRequest<ApiResponse<int>>;

public class CreateGeofenceCommandValidator : AbstractValidator<CreateGeofenceCommand>
{
    public CreateGeofenceCommandValidator()
    {
        RuleFor(x => x.Geofence.Name).NotEmpty().MaximumLength(100)
            .WithMessage("Geofence name is required (max 100 characters).");
        RuleFor(x => x.Geofence.Category).NotEmpty().WithMessage("Category is required.");
        RuleFor(x => x.Geofence.AreaType).NotEmpty();
        RuleFor(x => x.Geofence.Description).MaximumLength(250);
        RuleFor(x => x.Geofence).Custom((dto, ctx) =>
        {
            if (!GpsGeoHelper.TryValidateGeofenceGeometry(dto.AreaType, dto.RadiusMeters, dto.GeoJson, out var error) && error != null)
                ctx.AddFailure(error);
        });
    }
}

public class CreateGeofenceCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<CreateGeofenceCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(CreateGeofenceCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.CreateGeofenceAsync(request, cancellationToken);
}

public record UpdateGeofenceCommand(int Id, UpdateGeofenceDto Geofence) : IRequest<ApiResponse<bool>>;

public class UpdateGeofenceCommandValidator : AbstractValidator<UpdateGeofenceCommand>
{
    public UpdateGeofenceCommandValidator()
    {
        RuleFor(x => x.Geofence.Name).NotEmpty().MaximumLength(100)
            .WithMessage("Geofence name is required (max 100 characters).");
        RuleFor(x => x.Geofence.Category).NotEmpty().WithMessage("Category is required.");
        RuleFor(x => x.Geofence.Description).MaximumLength(250);
        RuleFor(x => x.Geofence).Custom((dto, ctx) =>
        {
            if (!GpsGeoHelper.TryValidateGeofenceGeometry(dto.AreaType, dto.RadiusMeters, dto.GeoJson, out var error) && error != null)
                ctx.AddFailure(error);
        });
    }
}

public class UpdateGeofenceCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<UpdateGeofenceCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(UpdateGeofenceCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.UpdateGeofenceAsync(request, cancellationToken);
}

public record DeleteGeofenceCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteGeofenceCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<DeleteGeofenceCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(DeleteGeofenceCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.DeleteGeofenceAsync(request, cancellationToken);
}

public record DuplicateGeofenceCommand(int Id) : IRequest<ApiResponse<int>>;

public class DuplicateGeofenceCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<DuplicateGeofenceCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(DuplicateGeofenceCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.DuplicateGeofenceAsync(request, cancellationToken);
}

public record UpsertGeofenceAssignmentsCommand(int GeofenceId, UpsertGeofenceAssignmentsDto Body)
    : IRequest<ApiResponse<bool>>;

public class UpsertGeofenceAssignmentsCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<UpsertGeofenceAssignmentsCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(UpsertGeofenceAssignmentsCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.UpsertGeofenceAssignmentsAsync(request, cancellationToken);
}

public record DeleteGeofenceAssignmentCommand(int GeofenceId, int AssignmentId) : IRequest<ApiResponse<bool>>;

public class DeleteGeofenceAssignmentCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<DeleteGeofenceAssignmentCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(DeleteGeofenceAssignmentCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.DeleteGeofenceAssignmentAsync(request, cancellationToken);
}
