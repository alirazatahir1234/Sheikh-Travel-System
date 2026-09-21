using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Commands;

public record SavePortalAddressCommand(string Phone, PortalSaveAddressRequest Request)
    : IRequest<ApiResponse<PortalSavedAddressDto>>;

public class SavePortalAddressCommandValidator : AbstractValidator<SavePortalAddressCommand>
{
    public SavePortalAddressCommandValidator()
    {
        RuleFor(x => x.Request.Label).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.AddressLine).NotEmpty().MaximumLength(500);
    }
}

public class SavePortalAddressCommandHandler(ICustomerPortalRepository portalRepository)
    : IRequestHandler<SavePortalAddressCommand, ApiResponse<PortalSavedAddressDto>>
{
    public async Task<ApiResponse<PortalSavedAddressDto>> Handle(
        SavePortalAddressCommand request,
        CancellationToken cancellationToken)
    {
        var customerId = await portalRepository.ResolveCustomerIdByPhoneAsync(request.Phone, cancellationToken);
        if (customerId is null or <= 0)
            return ApiResponse<PortalSavedAddressDto>.FailResponse("Complete a booking or sign in first.");

        var id = await portalRepository.SaveAddressAsync(
            customerId.Value,
            request.Request.Label,
            request.Request.AddressLine,
            request.Request.Latitude,
            request.Request.Longitude,
            cancellationToken);

        return ApiResponse<PortalSavedAddressDto>.SuccessResponse(
            new PortalSavedAddressDto(id, request.Request.Label, request.Request.AddressLine, request.Request.Latitude, request.Request.Longitude),
            "Address saved.");
    }
}
