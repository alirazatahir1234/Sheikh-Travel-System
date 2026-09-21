using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record CreateGpsDeviceCommand(CreateGpsDeviceDto Device) : IRequest<ApiResponse<int>>;

public class CreateGpsDeviceCommandValidator : AbstractValidator<CreateGpsDeviceCommand>
{
    public CreateGpsDeviceCommandValidator()
    {
        RuleFor(x => x.Device.UniqueId)
            .NotEmpty()
            .MaximumLength(100)
            .Matches(@"^\d{15}$")
            .WithMessage("IMEI must be exactly 15 digits.");
        RuleFor(x => x.Device.Name)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(200)
            .Matches(@"[A-Za-z]")
            .WithMessage("Name must contain at least one letter.");
    }
}

public class UpdateGpsDeviceCommandValidator : AbstractValidator<UpdateGpsDeviceCommand>
{
    public UpdateGpsDeviceCommandValidator()
    {
        RuleFor(x => x.Device.Name)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(200)
            .Matches(@"[A-Za-z]")
            .WithMessage("Name must contain at least one letter.");
    }
}

public class CreateGpsDeviceCommandHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<CreateGpsDeviceCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(CreateGpsDeviceCommand request, CancellationToken cancellationToken)
        => gpsDeviceRepository.CreateGpsDeviceAsync(request, cancellationToken);
}

public record UpdateGpsDeviceCommand(int Id, UpdateGpsDeviceDto Device) : IRequest<ApiResponse<bool>>;

public class UpdateGpsDeviceCommandHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<UpdateGpsDeviceCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(UpdateGpsDeviceCommand request, CancellationToken cancellationToken)
        => gpsDeviceRepository.UpdateGpsDeviceAsync(request, cancellationToken);
}

public record DeleteGpsDeviceCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteGpsDeviceCommandHandler(IGpsDeviceRepository gpsDeviceRepository)
    : IRequestHandler<DeleteGpsDeviceCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(DeleteGpsDeviceCommand request, CancellationToken cancellationToken)
        => gpsDeviceRepository.DeleteGpsDeviceAsync(request, cancellationToken);
}
