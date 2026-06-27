using Dapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record CreateGpsDeviceCommand(CreateGpsDeviceDto Device) : IRequest<ApiResponse<int>>;

public class CreateGpsDeviceCommandValidator : AbstractValidator<CreateGpsDeviceCommand>
{
    public CreateGpsDeviceCommandValidator()
    {
        RuleFor(x => x.Device.UniqueId)
            .NotEmpty()
            .MaximumLength(100)
            .Matches(@"^\d{14,20}$")
            .WithMessage("Unique ID must be 14–20 digits (IMEI).");
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

public class CreateGpsDeviceCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITraccarClient traccar,
    IOptions<TraccarOptions> traccarOptions)
    : IRequestHandler<CreateGpsDeviceCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateGpsDeviceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Device;

        var duplicate = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM GpsDevices WHERE UniqueId = @UniqueId AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { dto.UniqueId },
            cancellationToken: cancellationToken));

        if (duplicate)
            return ApiResponse<int>.FailResponse("A device with this IMEI already exists.");

        if (dto.VehicleId.HasValue)
        {
            var vehicleError = await GpsDeviceVehicleGuard.ValidateAsync(connection, dto.VehicleId.Value, cancellationToken);
            if (vehicleError is not null)
                return ApiResponse<int>.FailResponse(vehicleError);
        }

        int? traccarDeviceId = null;
        if (traccarOptions.Value.Enabled)
        {
            var created = await traccar.CreateDeviceAsync(dto.Name, dto.UniqueId, ct: cancellationToken);
            if (created is null)
                return ApiResponse<int>.FailResponse(
                    "Failed to create device in Traccar. Check server connectivity and credentials.");

            traccarDeviceId = created.Id;
        }

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO GpsDevices (VehicleId, UniqueId, Name, Protocol, Model, SimNumber, Vendor,
              SupportsEngineCutoff, TraccarDeviceId, IsActive, CreatedAt, CreatedBy, IsDeleted)
              OUTPUT INSERTED.Id
              VALUES (@VehicleId, @UniqueId, @Name, @Protocol, @Model, @SimNumber, @Vendor,
              @SupportsEngineCutoff, @TraccarDeviceId, 1, GETUTCDATE(), @CreatedBy, 0)",
            new
            {
                dto.VehicleId,
                dto.UniqueId,
                dto.Name,
                dto.Protocol,
                dto.Model,
                dto.SimNumber,
                dto.Vendor,
                dto.SupportsEngineCutoff,
                TraccarDeviceId = traccarDeviceId,
                CreatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "GPS device created.");
    }
}

public record UpdateGpsDeviceCommand(int Id, UpdateGpsDeviceDto Device) : IRequest<ApiResponse<bool>>;

public class UpdateGpsDeviceCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITraccarClient traccar,
    IOptions<TraccarOptions> traccarOptions)
    : IRequestHandler<UpdateGpsDeviceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateGpsDeviceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Device;

        var existing = await connection.QueryFirstOrDefaultAsync<(int Id, int? TraccarDeviceId, string UniqueId)>(
            new CommandDefinition(
                "SELECT Id, TraccarDeviceId, UniqueId FROM GpsDevices WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (existing.Id == 0)
            return ApiResponse<bool>.FailResponse("Device not found.");

        if (dto.VehicleId.HasValue)
        {
            var vehicleError = await GpsDeviceVehicleGuard.ValidateAsync(connection, dto.VehicleId.Value, cancellationToken);
            if (vehicleError is not null)
                return ApiResponse<bool>.FailResponse(vehicleError);
        }

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE GpsDevices SET VehicleId = @VehicleId, Name = @Name, Protocol = @Protocol,
              SupportsEngineCutoff = @SupportsEngineCutoff, IsActive = @IsActive, UpdatedAt = GETUTCDATE(), UpdatedBy = @UpdatedBy
              WHERE Id = @Id AND IsDeleted = 0",
            new
            {
                request.Id,
                dto.VehicleId,
                dto.Name,
                dto.Protocol,
                dto.SupportsEngineCutoff,
                dto.IsActive,
                UpdatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        if (rows == 0)
            return ApiResponse<bool>.FailResponse("Device not found.");

        if (traccarOptions.Value.Enabled && existing.TraccarDeviceId.HasValue)
        {
            var synced = await traccar.UpdateDeviceAsync(
                existing.TraccarDeviceId.Value,
                dto.Name,
                existing.UniqueId,
                disabled: !dto.IsActive,
                ct: cancellationToken);

            if (!synced)
                return ApiResponse<bool>.FailResponse("Device updated locally but Traccar sync failed.");
        }

        return ApiResponse<bool>.SuccessResponse(true, "GPS device updated.");
    }
}

public record DeleteGpsDeviceCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteGpsDeviceCommandHandler(
    IDbConnectionFactory dbFactory,
    ITraccarClient traccar,
    IOptions<TraccarOptions> traccarOptions)
    : IRequestHandler<DeleteGpsDeviceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteGpsDeviceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var existing = await connection.QueryFirstOrDefaultAsync<(int Id, int? TraccarDeviceId)>(
            new CommandDefinition(
                "SELECT Id, TraccarDeviceId FROM GpsDevices WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (existing.Id == 0)
            return ApiResponse<bool>.FailResponse("Device not found.");

        var traccarDeviceId = existing.TraccarDeviceId;

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE GpsDevices SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
            new { request.Id },
            cancellationToken: cancellationToken));

        if (rows == 0)
            return ApiResponse<bool>.FailResponse("Device not found.");

        if (traccarOptions.Value.Enabled && traccarDeviceId.HasValue)
            await traccar.DeleteDeviceAsync(traccarDeviceId.Value, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "GPS device deleted.");
    }
}

internal static class GpsDeviceVehicleGuard
{
    public static async Task<string?> ValidateAsync(
        System.Data.IDbConnection connection,
        int vehicleId,
        CancellationToken cancellationToken)
    {
        var status = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Status FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
            new { Id = vehicleId },
            cancellationToken: cancellationToken));

        if (status is null)
            return "Vehicle not found.";

        if (status == (int)VehicleStatus.Draft)
            return "Cannot link a GPS device to a draft vehicle. Complete the vehicle first.";

        return null;
    }
}
