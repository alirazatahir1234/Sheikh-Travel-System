using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record CreateGpsDeviceCommand(CreateGpsDeviceDto Device) : IRequest<ApiResponse<int>>;

public class CreateGpsDeviceCommandValidator : AbstractValidator<CreateGpsDeviceCommand>
{
    public CreateGpsDeviceCommandValidator()
    {
        RuleFor(x => x.Device.UniqueId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Device.Name).NotEmpty().MaximumLength(200);
    }
}

public class CreateGpsDeviceCommandHandler(IDbConnectionFactory dbFactory, ICurrentUserService currentUser)
    : IRequestHandler<CreateGpsDeviceCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateGpsDeviceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Device;
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO GpsDevices (VehicleId, UniqueId, Name, Protocol, Model, SimNumber, Vendor, SupportsEngineCutoff, IsActive, CreatedAt, CreatedBy, IsDeleted)
              OUTPUT INSERTED.Id
              VALUES (@VehicleId, @UniqueId, @Name, @Protocol, @Model, @SimNumber, @Vendor, @SupportsEngineCutoff, 1, GETUTCDATE(), @CreatedBy, 0)",
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
                CreatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "GPS device created.");
    }
}

public record UpdateGpsDeviceCommand(int Id, UpdateGpsDeviceDto Device) : IRequest<ApiResponse<bool>>;

public class UpdateGpsDeviceCommandHandler(IDbConnectionFactory dbFactory, ICurrentUserService currentUser)
    : IRequestHandler<UpdateGpsDeviceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateGpsDeviceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Device;
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

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "GPS device updated.")
            : ApiResponse<bool>.FailResponse("Device not found.");
    }
}

public record DeleteGpsDeviceCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteGpsDeviceCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<DeleteGpsDeviceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteGpsDeviceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE GpsDevices SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
            new { request.Id },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "GPS device deleted.")
            : ApiResponse<bool>.FailResponse("Device not found.");
    }
}
