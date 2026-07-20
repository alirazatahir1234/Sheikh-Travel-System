using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
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
    IDbConnectionFactory dbFactory,
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

        using var connection = dbFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            MERGE DriverDevices AS t
            USING (SELECT @DriverId AS DriverId, @DeviceId AS DeviceId) AS s
            ON t.DriverId = s.DriverId AND t.DeviceId = s.DeviceId AND t.IsDeleted = 0
            WHEN MATCHED THEN UPDATE SET
                Platform = @Platform,
                Model = @Model,
                OsVersion = @OsVersion,
                AppVersion = @AppVersion,
                PackageName = @PackageName,
                InstallerStore = @InstallerStore,
                FingerprintHash = @FingerprintHash,
                IsEmulator = @IsEmulator,
                IsRooted = @IsRooted,
                IsJailbroken = @IsJailbroken,
                IsTampered = @IsTampered,
                PinningConfigured = @PinningConfigured,
                UserId = @UserId,
                LastSeenAt = @Now,
                UpdatedAt = @Now
            WHEN NOT MATCHED THEN INSERT
                (TenantId, DriverId, UserId, DeviceId, Platform, Model, OsVersion, AppVersion,
                 PackageName, InstallerStore, FingerprintHash, IsEmulator, IsRooted, IsJailbroken,
                 IsTampered, PinningConfigured, LastSeenAt, CreatedAt, IsDeleted)
            VALUES
                (@TenantId, @DriverId, @UserId, @DeviceId, @Platform, @Model, @OsVersion, @AppVersion,
                 @PackageName, @InstallerStore, @FingerprintHash, @IsEmulator, @IsRooted, @IsJailbroken,
                 @IsTampered, @PinningConfigured, @Now, @Now, 0);

            SELECT Id FROM DriverDevices
            WHERE DriverId = @DriverId AND DeviceId = @DeviceId AND IsDeleted = 0;
            """,
            new
            {
                TenantId = tenantId,
                DriverId = driverId.Value,
                UserId = currentUser.UserId,
                r.DeviceId,
                r.Platform,
                r.Model,
                r.OsVersion,
                r.AppVersion,
                r.PackageName,
                r.InstallerStore,
                r.FingerprintHash,
                r.IsEmulator,
                r.IsRooted,
                r.IsJailbroken,
                r.IsTampered,
                r.PinningConfigured,
                Now = now
            },
            cancellationToken: cancellationToken));

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
