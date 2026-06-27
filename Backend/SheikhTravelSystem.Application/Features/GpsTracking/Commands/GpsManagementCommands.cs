using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record CreateGeofenceCommand(CreateGeofenceDto Geofence) : IRequest<ApiResponse<int>>;

public class CreateGeofenceCommandValidator : AbstractValidator<CreateGeofenceCommand>
{
    public CreateGeofenceCommandValidator()
    {
        RuleFor(x => x.Geofence.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Geofence.RadiusMeters).GreaterThan(0);
    }
}

public class CreateGeofenceCommandHandler(IDbConnectionFactory dbFactory, ICurrentUserService currentUser)
    : IRequestHandler<CreateGeofenceCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateGeofenceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Geofence;
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO Geofences (Name, AreaType, CenterLat, CenterLng, RadiusMeters, GeoJson, IsActive, CreatedAt, CreatedBy, IsDeleted)
              OUTPUT INSERTED.Id
              VALUES (@Name, 'circle', @CenterLat, @CenterLng, @RadiusMeters, @GeoJson, 1, GETUTCDATE(), @CreatedBy, 0)",
            new
            {
                dto.Name,
                dto.CenterLat,
                dto.CenterLng,
                dto.RadiusMeters,
                dto.GeoJson,
                CreatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Geofence created.");
    }
}

public record UpdateGeofenceCommand(int Id, UpdateGeofenceDto Geofence) : IRequest<ApiResponse<bool>>;

public class UpdateGeofenceCommandHandler(IDbConnectionFactory dbFactory, ICurrentUserService currentUser)
    : IRequestHandler<UpdateGeofenceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateGeofenceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Geofence;
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Geofences SET Name = @Name, CenterLat = @CenterLat, CenterLng = @CenterLng,
              RadiusMeters = @RadiusMeters, GeoJson = @GeoJson, IsActive = @IsActive, UpdatedAt = GETUTCDATE(), UpdatedBy = @UpdatedBy
              WHERE Id = @Id AND IsDeleted = 0",
            new
            {
                request.Id,
                dto.Name,
                dto.CenterLat,
                dto.CenterLng,
                dto.RadiusMeters,
                dto.GeoJson,
                dto.IsActive,
                UpdatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Geofence updated.")
            : ApiResponse<bool>.FailResponse("Geofence not found.");
    }
}

public record DeleteGeofenceCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteGeofenceCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<DeleteGeofenceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteGeofenceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Geofences SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
            new { request.Id },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Geofence deleted.")
            : ApiResponse<bool>.FailResponse("Geofence not found.");
    }
}

public record CreateGpsAlertRuleCommand(CreateGpsAlertRuleDto Rule) : IRequest<ApiResponse<int>>;

public class CreateGpsAlertRuleCommandHandler(IDbConnectionFactory dbFactory, ICurrentUserService currentUser)
    : IRequestHandler<CreateGpsAlertRuleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateGpsAlertRuleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Rule;
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO GpsAlertRules (VehicleId, SpeedLimitKmh, GeofenceId, AlertOnEnter, AlertOnExit, IsActive, CreatedAt, CreatedBy, IsDeleted)
              OUTPUT INSERTED.Id
              VALUES (@VehicleId, @SpeedLimitKmh, @GeofenceId, @AlertOnEnter, @AlertOnExit, 1, GETUTCDATE(), @CreatedBy, 0)",
            new
            {
                dto.VehicleId,
                dto.SpeedLimitKmh,
                dto.GeofenceId,
                dto.AlertOnEnter,
                dto.AlertOnExit,
                CreatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Alert rule created.");
    }
}

public record AcknowledgeGpsAlertCommand(int Id) : IRequest<ApiResponse<bool>>;

public class AcknowledgeGpsAlertCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<AcknowledgeGpsAlertCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AcknowledgeGpsAlertCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE GpsAlertEvents SET IsAcknowledged = 1 WHERE Id = @Id AND IsDeleted = 0",
            new { request.Id },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Alert acknowledged.")
            : ApiResponse<bool>.FailResponse("Alert not found.");
    }
}

public record SendDeviceCommandCommand(SendDeviceCommandDto Command) : IRequest<ApiResponse<int>>;

public class SendDeviceCommandCommandValidator : AbstractValidator<SendDeviceCommandCommand>
{
    private static readonly string[] Allowed = ["engineStop", "engineResume", "positionSingle", "custom"];

    public SendDeviceCommandCommandValidator()
    {
        RuleFor(x => x.Command.GpsDeviceId).GreaterThan(0);
        RuleFor(x => x.Command.CommandType).Must(t => Allowed.Contains(t, StringComparer.OrdinalIgnoreCase));
    }
}

public class SendDeviceCommandCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    IAuditService auditService,
    ITraccarClient traccar,
    ITenantContext tenantContext,
    INotificationService notifications)
    : IRequestHandler<SendDeviceCommandCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SendDeviceCommandCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var device = await connection.QueryFirstOrDefaultAsync<(int Id, bool SupportsEngineCutoff, string Name, int? TraccarDeviceId)>(
            new CommandDefinition(
                "SELECT Id, SupportsEngineCutoff, Name, TraccarDeviceId FROM GpsDevices WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.Command.GpsDeviceId },
                cancellationToken: cancellationToken));

        if (device.Id == 0)
            return ApiResponse<int>.FailResponse("Device not found.");

        if (request.Command.CommandType.Equals("engineStop", StringComparison.OrdinalIgnoreCase)
            && !device.SupportsEngineCutoff)
            return ApiResponse<int>.FailResponse("Device does not support engine cut-off.");

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO GpsDeviceCommands
                (GpsDeviceId, CommandType, Status, Reason, TenantId, RequestedBy, RequestedAt, CreatedAt, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES
                (@GpsDeviceId, @CommandType, 'pending', @Reason, @TenantId, @RequestedBy, GETUTCDATE(), GETUTCDATE(), 0)
            """,
            new
            {
                request.Command.GpsDeviceId,
                request.Command.CommandType,
                request.Command.Reason,
                TenantId = tenantContext.TenantId,
                RequestedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        // Forward to Traccar immediately if the device is linked; device will relay to hardware.
        if (device.TraccarDeviceId.HasValue)
        {
            var sent = await traccar.SendCommandAsync(device.TraccarDeviceId.Value, request.Command.CommandType, cancellationToken);
            if (sent)
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE GpsDeviceCommands SET Status = 'sent' WHERE Id = @Id",
                    new { Id = id },
                    cancellationToken: cancellationToken));
        }

        await auditService.LogAsync("Send", "GpsDeviceCommand", id, cancellationToken);

        if (request.Command.CommandType is "engineStop" or "engineResume")
        {
            var verb = request.Command.CommandType == "engineStop" ? "cut off" : "restored";
            await notifications.CreateForAllAsync(
                $"Engine {verb} — {device.Name}",
                $"Reason: {request.Command.Reason ?? "Not specified"}",
                NotificationType.EngineCommandSent,
                id,
                cancellationToken);
        }

        return ApiResponse<int>.SuccessResponse(id, "Command queued.");
    }
}

public record CompleteDeviceCommandCommand(int Id, string Status) : IRequest<ApiResponse<bool>>;

public class CompleteDeviceCommandCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CompleteDeviceCommandCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(CompleteDeviceCommandCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE GpsDeviceCommands SET Status = @Status, CompletedAt = GETUTCDATE()
              WHERE Id = @Id AND IsDeleted = 0",
            new { request.Id, request.Status },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Command updated.")
            : ApiResponse<bool>.FailResponse("Command not found.");
    }
}
