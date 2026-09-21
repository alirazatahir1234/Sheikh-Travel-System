using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record IngestPositionCommand(IngestPositionDto Position) : IRequest<ApiResponse<bool>>;

public class IngestPositionCommandValidator : AbstractValidator<IngestPositionCommand>
{
    public IngestPositionCommandValidator()
    {
        RuleFor(x => x.Position.VehicleId).GreaterThan(0);
        RuleFor(x => x.Position.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Position.Longitude).InclusiveBetween(-180, 180);
    }
}

public class IngestPositionCommandHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<IngestPositionCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(IngestPositionCommand request, CancellationToken cancellationToken)
        => gpsTrackingRepository.IngestPositionAsync(request, cancellationToken);
}
