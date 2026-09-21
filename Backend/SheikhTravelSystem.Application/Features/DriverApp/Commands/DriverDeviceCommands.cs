using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;

namespace SheikhTravelSystem.Application.Features.DriverApp.Commands;

public record RegisterDriverDeviceCommand(RegisterDriverDeviceRequest Request)
    : IRequest<ApiResponse<DriverDeviceDto>>;

public class RegisterDriverDeviceCommandValidator : AbstractValidator<RegisterDriverDeviceCommand>
{
    public RegisterDriverDeviceCommandValidator()
    {
        RuleFor(x => x.Request.DeviceId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Platform).NotEmpty().MaximumLength(40);
    }
}

public class RegisterDriverDeviceCommandHandler(
    IDriverAppRepository driverAppRepository,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<RegisterDriverDeviceCommand, ApiResponse<DriverDeviceDto>>
{
    public async Task<ApiResponse<DriverDeviceDto>> Handle(
        RegisterDriverDeviceCommand request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverDeviceDto>.FailResponse("Driver identity required.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var r = request.Request;
        var now = DateTime.UtcNow;

        var id = await driverAppRepository.UpsertDriverDeviceAsync(new DriverDeviceUpsert
        {
            TenantId = tenantId,
            DriverId = driverId.Value,
            UserId = currentUser.UserId,
            DeviceId = r.DeviceId,
            Platform = r.Platform,
            Model = r.Model,
            OsVersion = r.OsVersion,
            AppVersion = r.AppVersion,
            PackageName = r.PackageName,
            InstallerStore = r.InstallerStore,
            FingerprintHash = r.FingerprintHash,
            IsEmulator = r.IsEmulator,
            IsRooted = r.IsRooted,
            IsJailbroken = r.IsJailbroken,
            IsTampered = r.IsTampered,
            PinningConfigured = r.PinningConfigured,
            Now = now
        }, cancellationToken);

        if (id is null or <= 0)
            return ApiResponse<DriverDeviceDto>.FailResponse("Device registration failed.");

        return ApiResponse<DriverDeviceDto>.SuccessResponse(new DriverDeviceDto(
            id.Value,
            r.DeviceId,
            r.Platform,
            r.Model,
            r.IsEmulator,
            r.IsRooted || r.IsJailbroken,
            r.IsTampered,
            now));
    }
}
