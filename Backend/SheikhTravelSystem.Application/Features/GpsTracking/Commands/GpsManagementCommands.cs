using System.Text.Json;
using Dapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

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

public class AcknowledgeGpsAlertCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<AcknowledgeGpsAlertCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AcknowledgeGpsAlertCommand request, CancellationToken cancellationToken)
    {
        if (!GpsAlertAccess.CanAcknowledge(currentUser))
            return ApiResponse<bool>.FailResponse("Insufficient permission to acknowledge alerts.");

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE e
            SET e.IsAcknowledged = 1,
                e.Status = CASE WHEN e.Status = 'active' THEN 'acknowledged' ELSE e.Status END,
                e.AcknowledgedAt = COALESCE(e.AcknowledgedAt, GETUTCDATE()),
                e.AcknowledgedBy = COALESCE(e.AcknowledgedBy, @AcknowledgedBy),
                e.ReadAt = COALESCE(e.ReadAt, GETUTCDATE()),
                e.ReadBy = COALESCE(e.ReadBy, @AcknowledgedBy)
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE e.Id = @Id
              AND e.IsDeleted = 0
              AND v.TenantId = @TenantId
              AND v.IsDeleted = 0
              AND e.Status IN ('active', 'acknowledged')
            """,
            new
            {
                request.Id,
                TenantId = tenantContext.GetRequiredTenantId(),
                AcknowledgedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Alert acknowledged.")
            : ApiResponse<bool>.FailResponse("Alert not found or already processed.");
    }
}

public record MarkGpsAlertReadCommand(int Id) : IRequest<ApiResponse<bool>>;

public class MarkGpsAlertReadCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<MarkGpsAlertReadCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(MarkGpsAlertReadCommand request, CancellationToken cancellationToken)
    {
        if (!GpsAlertAccess.CanView(currentUser))
            return ApiResponse<bool>.FailResponse("Insufficient permission to read alerts.");

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE e
            SET e.ReadAt = COALESCE(e.ReadAt, GETUTCDATE()),
                e.ReadBy = COALESCE(e.ReadBy, @ReadBy)
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE e.Id = @Id
              AND e.IsDeleted = 0
              AND v.TenantId = @TenantId
              AND v.IsDeleted = 0
              AND e.Status <> 'archived'
              AND e.ReadAt IS NULL
            """,
            new
            {
                request.Id,
                TenantId = tenantContext.GetRequiredTenantId(),
                ReadBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Alert marked as read.")
            : ApiResponse<bool>.FailResponse("Alert not found or already read.");
    }
}

public record ResolveGpsAlertCommand(int Id, ResolveGpsAlertDto Resolution) : IRequest<ApiResponse<bool>>;

public class ResolveGpsAlertCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<ResolveGpsAlertCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ResolveGpsAlertCommand request, CancellationToken cancellationToken)
    {
        if (!GpsAlertAccess.CanResolve(currentUser))
            return ApiResponse<bool>.FailResponse("Insufficient permission to resolve alerts.");

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE e
            SET e.IsAcknowledged = 1,
                e.Status = 'resolved',
                e.ResolvedAt = COALESCE(e.ResolvedAt, GETUTCDATE()),
                e.ResolvedBy = COALESCE(e.ResolvedBy, @ResolvedBy),
                e.ResolutionNotes = @ResolutionNotes,
                e.AcknowledgedAt = COALESCE(e.AcknowledgedAt, GETUTCDATE()),
                e.AcknowledgedBy = COALESCE(e.AcknowledgedBy, @ResolvedBy),
                e.ReadAt = COALESCE(e.ReadAt, GETUTCDATE()),
                e.ReadBy = COALESCE(e.ReadBy, @ResolvedBy)
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE e.Id = @Id
              AND e.IsDeleted = 0
              AND v.TenantId = @TenantId
              AND v.IsDeleted = 0
              AND e.Status <> 'resolved'
              AND e.Status <> 'archived'
            """,
            new
            {
                request.Id,
                TenantId = tenantContext.GetRequiredTenantId(),
                ResolvedBy = currentUser.UserId?.ToString(),
                request.Resolution.ResolutionNotes
            },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Alert resolved.")
            : ApiResponse<bool>.FailResponse("Alert not found or already resolved.");
    }
}

public record ArchiveGpsAlertCommand(int Id, ArchiveGpsAlertDto Archive) : IRequest<ApiResponse<bool>>;

public class ArchiveGpsAlertCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<ArchiveGpsAlertCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ArchiveGpsAlertCommand request, CancellationToken cancellationToken)
    {
        if (!GpsAlertAccess.CanArchive(currentUser))
            return ApiResponse<bool>.FailResponse("Insufficient permission to archive alerts.");

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE e
            SET e.Status = 'archived',
                e.ArchivedAt = COALESCE(e.ArchivedAt, GETUTCDATE()),
                e.ArchivedBy = COALESCE(e.ArchivedBy, @ArchivedBy),
                e.ReadAt = COALESCE(e.ReadAt, GETUTCDATE()),
                e.ReadBy = COALESCE(e.ReadBy, @ArchivedBy),
                e.ResolutionNotes = COALESCE(NULLIF(@ArchiveReason, ''), e.ResolutionNotes)
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE e.Id = @Id
              AND e.IsDeleted = 0
              AND v.TenantId = @TenantId
              AND v.IsDeleted = 0
              AND e.Status IN ('acknowledged', 'resolved')
            """,
            new
            {
                request.Id,
                TenantId = tenantContext.GetRequiredTenantId(),
                ArchivedBy = currentUser.UserId?.ToString(),
                request.Archive.ArchiveReason
            },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Alert archived.")
            : ApiResponse<bool>.FailResponse("Only acknowledged or resolved alerts can be archived.");
    }
}

public record DeleteGpsAlertEventCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteGpsAlertEventCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<DeleteGpsAlertEventCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteGpsAlertEventCommand request, CancellationToken cancellationToken)
    {
        if (!GpsAlertAccess.CanDelete(currentUser))
            return ApiResponse<bool>.FailResponse("Insufficient permission to delete alerts.");

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE e
            SET e.IsDeleted = 1
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE e.Id = @Id
              AND e.IsDeleted = 0
              AND v.TenantId = @TenantId
              AND v.IsDeleted = 0
            """,
            new { request.Id, TenantId = tenantContext.GetRequiredTenantId() },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Alert deleted.")
            : ApiResponse<bool>.FailResponse("Alert not found.");
    }
}

public record UpdateAlertSettingsCommand(UpdateAlertSettingsDto Settings) : IRequest<ApiResponse<bool>>;

public class UpdateAlertSettingsCommandHandler(IDbConnectionFactory dbFactory, ICurrentUserService currentUser)
    : IRequestHandler<UpdateAlertSettingsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateAlertSettingsCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null)
            return ApiResponse<bool>.FailResponse("No authenticated user.");

        using var connection = dbFactory.CreateConnection();
        foreach (var s in request.Settings.Settings)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                MERGE AlertSettings AS target
                USING (SELECT @UserId AS UserId, @AlertType AS AlertType) AS source
                ON target.UserId = source.UserId AND target.AlertType = source.AlertType
                WHEN MATCHED THEN
                    UPDATE SET InAppEnabled = @InAppEnabled, EmailEnabled = @EmailEnabled,
                               PushEnabled = @PushEnabled, SmsEnabled = @SmsEnabled, UpdatedAt = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT (UserId, AlertType, InAppEnabled, EmailEnabled, PushEnabled, SmsEnabled, CreatedAt)
                    VALUES (@UserId, @AlertType, @InAppEnabled, @EmailEnabled, @PushEnabled, @SmsEnabled, GETUTCDATE());
                """,
                new
                {
                    UserId = userId.Value,
                    s.AlertType,
                    s.InAppEnabled,
                    s.EmailEnabled,
                    s.PushEnabled,
                    s.SmsEnabled
                },
                cancellationToken: cancellationToken));
        }

        return ApiResponse<bool>.SuccessResponse(true, "Alert settings saved.");
    }
}

public record SendDeviceCommandCommand(SendDeviceCommandDto Command) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Send";
    public string AuditEntityName => "GpsDeviceCommand";
    public int? AuditEntityId => null;
}

public class SendDeviceCommandCommandValidator : AbstractValidator<SendDeviceCommandCommand>
{
    public SendDeviceCommandCommandValidator()
    {
        RuleFor(x => x.Command.GpsDeviceId).GreaterThan(0);
        RuleFor(x => x.Command.CommandType).Must(t => GpsCommandCatalog.Find(t) is not null)
            .WithMessage("Unknown command type.");
    }
}

public class SendDeviceCommandCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITraccarClient traccar,
    ITenantContext tenantContext,
    INotificationDecisionEngine decisionEngine,
    IOptions<GpsSettings> gpsSettings)
    : IRequestHandler<SendDeviceCommandCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SendDeviceCommandCommand request, CancellationToken cancellationToken)
    {
        var definition = GpsCommandCatalog.Find(request.Command.CommandType);
        if (definition is null)
            return ApiResponse<int>.FailResponse("Unknown command type.");

        if (!currentUser.HasPermission(definition.Permission))
            return ApiResponse<int>.FailResponse("Insufficient permission for this command type.");

        using var connection = dbFactory.CreateConnection();
        var device = await connection.QueryFirstOrDefaultAsync<(int Id, bool SupportsEngineCutoff, bool SupportsRelay, string Name, int? TraccarDeviceId, int? VehicleId, string? RelayPurpose)>(
            new CommandDefinition(
                "SELECT Id, SupportsEngineCutoff, SupportsRelay, Name, TraccarDeviceId, VehicleId, RelayPurpose FROM GpsDevices WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.Command.GpsDeviceId },
                cancellationToken: cancellationToken));

        if (device.Id == 0)
            return ApiResponse<int>.FailResponse("Device not found.");

        if (definition.CapabilityColumn is not null)
        {
            var hasCapability = definition.CapabilityColumn switch
            {
                "SupportsEngineCutoff" => device.SupportsEngineCutoff,
                "SupportsRelay" => device.SupportsRelay,
                _ => true
            };
            if (!hasCapability)
                return ApiResponse<int>.FailResponse($"Device does not support {definition.Label}.");
        }

        if (definition.RequiresEngineSafetyCheck)
        {
            var isRelayCommand = request.Command.CommandType is "relayOn" or "relayOff";
            var needsSafetyCheck = !isRelayCommand
                || GpsCommandSafetyChecker.RelayNeedsEngineSafetyCheck(request.Command.CommandType, device.RelayPurpose);

            if (needsSafetyCheck)
            {
                var safetyError = await GpsCommandSafetyChecker.CheckEngineCutoffPreconditionAsync(
                    connection, device.VehicleId, cancellationToken);
                if (safetyError is not null)
                    return ApiResponse<int>.FailResponse(safetyError);
            }
        }

        var duplicatePending = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT CASE WHEN EXISTS(
                SELECT 1 FROM GpsDeviceCommands
                WHERE GpsDeviceId = @GpsDeviceId AND CommandType = @CommandType
                  AND Status IN ('pending', 'sent') AND IsDeleted = 0
            ) THEN 1 ELSE 0 END
            """,
            new { request.Command.GpsDeviceId, request.Command.CommandType },
            cancellationToken: cancellationToken));

        if (duplicatePending)
            return ApiResponse<int>.FailResponse($"A {definition.Label} command is already in flight for this device.");

        var attributesJson = request.Command.Attributes is { Count: > 0 }
            ? JsonSerializer.Serialize(request.Command.Attributes)
            : null;

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO GpsDeviceCommands
                (GpsDeviceId, CommandType, Status, Reason, Attributes, MaxRetries, TenantId, RequestedBy, RequestedAt, CreatedAt, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES
                (@GpsDeviceId, @CommandType, 'pending', @Reason, @Attributes, @MaxRetries, @TenantId, @RequestedBy, GETUTCDATE(), GETUTCDATE(), 0)
            """,
            new
            {
                request.Command.GpsDeviceId,
                request.Command.CommandType,
                request.Command.Reason,
                Attributes = attributesJson,
                MaxRetries = gpsSettings.Value.CommandMaxRetries,
                TenantId = tenantContext.TenantId,
                RequestedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        var dispatchSucceeded = false;

        if (definition.TraccarType is null)
        {
            // customSms (or any future channel with no Traccar equivalent) — no gateway wired yet.
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE GpsDeviceCommands SET Status = 'not_configured' WHERE Id = @Id",
                new { Id = id },
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO GpsCommandResponses (CommandId, Source, ResponseText, ReceivedAt, CreatedAt)
                VALUES (@CommandId, 'system', 'No SMS gateway configured for this tenant.', GETUTCDATE(), GETUTCDATE())
                """,
                new { CommandId = id },
                cancellationToken: cancellationToken));
        }
        else if (device.TraccarDeviceId.HasValue)
        {
            var sent = await traccar.SendCommandAsync(device.TraccarDeviceId.Value, definition.TraccarType, request.Command.Attributes, cancellationToken);
            if (sent)
            {
                dispatchSucceeded = true;
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE GpsDeviceCommands SET Status = 'sent', UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                    new { Id = id },
                    cancellationToken: cancellationToken));
            }
            else
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE GpsDeviceCommands
                    SET ErrorMessage = 'Traccar dispatch failed', NextRetryAt = DATEADD(SECOND, @RetrySeconds, GETUTCDATE()), UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id
                    """,
                    new { Id = id, RetrySeconds = gpsSettings.Value.CommandRetryIntervalSeconds },
                    cancellationToken: cancellationToken));
            }
        }
        // Else: device has no TraccarDeviceId — stays 'pending', owned by the commands/pending polling path.

        if (definition.NotifyAllUsers && dispatchSucceeded)
        {
            var verb = request.Command.CommandType == "engineStop" ? "cut off" : "restored";
            await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                "vehicle_offline",
                $"Engine {verb} — {device.Name}",
                $"Reason: {request.Command.Reason ?? "Not specified"}",
                NotificationType.EngineCommandSent,
                ReferenceId: id,
                TenantId: tenantContext.GetRequiredTenantId(),
                SuggestedPriority: 3,
                RequestedChannels: [NotificationChannels.InApp, NotificationChannels.Browser],
                Broadcast: false), cancellationToken);
        }

        return ApiResponse<int>.SuccessResponse(id, "Command queued.");
    }
}

public record RetryDeviceCommandCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Retry";
    public string AuditEntityName => "GpsDeviceCommand";
    public int? AuditEntityId => Id;
}

public class RetryDeviceCommandCommandHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITraccarClient traccar)
    : IRequestHandler<RetryDeviceCommandCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(RetryDeviceCommandCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<(int Id, string CommandType, string Status, int GpsDeviceId, string? Attributes, int? TraccarDeviceId, int? VehicleId, string? RelayPurpose)>(
            new CommandDefinition(
                """
                SELECT c.Id, c.CommandType, c.Status, c.GpsDeviceId, c.Attributes, d.TraccarDeviceId, d.VehicleId, d.RelayPurpose
                FROM GpsDeviceCommands c
                INNER JOIN GpsDevices d ON d.Id = c.GpsDeviceId
                WHERE c.Id = @Id AND c.IsDeleted = 0
                """,
                new { request.Id },
                cancellationToken: cancellationToken));

        if (row.Id == 0)
            return ApiResponse<bool>.FailResponse("Command not found.");

        if (row.Status is not ("failed" or "timeout"))
            return ApiResponse<bool>.FailResponse("Only failed or timed-out commands can be retried.");

        var definition = GpsCommandCatalog.Find(row.CommandType);
        if (definition is null || definition.TraccarType is null)
            return ApiResponse<bool>.FailResponse("Command type cannot be retried.");

        if (!currentUser.HasPermission(GpsPermissions.CommandRetry))
            return ApiResponse<bool>.FailResponse("Insufficient permission to retry commands.");

        if (!row.TraccarDeviceId.HasValue)
            return ApiResponse<bool>.FailResponse("Device is not linked to Traccar.");

        if (definition.RequiresEngineSafetyCheck)
        {
            var isRelayCommand = row.CommandType is "relayOn" or "relayOff";
            var needsSafetyCheck = !isRelayCommand
                || GpsCommandSafetyChecker.RelayNeedsEngineSafetyCheck(row.CommandType, row.RelayPurpose);

            if (needsSafetyCheck)
            {
                // Vehicle state may have changed since the original (failed/timed-out) attempt.
                var safetyError = await GpsCommandSafetyChecker.CheckEngineCutoffPreconditionAsync(
                    connection, row.VehicleId, cancellationToken);
                if (safetyError is not null)
                    return ApiResponse<bool>.FailResponse(safetyError);
            }
        }

        var attributes = string.IsNullOrWhiteSpace(row.Attributes)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(row.Attributes);

        var sent = await traccar.SendCommandAsync(row.TraccarDeviceId.Value, definition.TraccarType, attributes, cancellationToken);

        if (sent)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE GpsDeviceCommands
                SET Status = 'sent', RetryCount = RetryCount + 1, ErrorMessage = NULL, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id
                """,
                new { request.Id },
                cancellationToken: cancellationToken));

            return ApiResponse<bool>.SuccessResponse(true, "Command retried.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE GpsDeviceCommands
            SET Status = 'failed', RetryCount = RetryCount + 1, ErrorMessage = 'Retry dispatch failed', UpdatedAt = GETUTCDATE()
            WHERE Id = @Id
            """,
            new { request.Id },
            cancellationToken: cancellationToken));

        return ApiResponse<bool>.FailResponse("Retry dispatch failed.");
    }
}

public record CancelDeviceCommandCommand(int Id, string? Reason) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Cancel";
    public string AuditEntityName => "GpsDeviceCommand";
    public int? AuditEntityId => Id;
}

public class CancelDeviceCommandCommandHandler(IDbConnectionFactory dbFactory, ICurrentUserService currentUser)
    : IRequestHandler<CancelDeviceCommandCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(CancelDeviceCommandCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE GpsDeviceCommands
            SET Status = 'cancelled', CancelledAt = GETUTCDATE(), CancelledBy = @CancelledBy, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND Status = 'pending' AND IsDeleted = 0
            """,
            new { request.Id, CancelledBy = currentUser.UserId?.ToString() },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Command cancelled.")
            : ApiResponse<bool>.FailResponse("Only pending commands can be cancelled.");
    }
}

public record CompleteDeviceCommandCommand(int Id, string UniqueId, string Status, string? ResponseText = null, string? ErrorMessage = null)
    : IRequest<ApiResponse<bool>>;

public class CompleteDeviceCommandCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CompleteDeviceCommandCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(CompleteDeviceCommandCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE c
            SET c.Status = @Status, c.CompletedAt = GETUTCDATE(), c.ErrorMessage = @ErrorMessage, c.UpdatedAt = GETUTCDATE()
            FROM GpsDeviceCommands c
            INNER JOIN GpsDevices d ON d.Id = c.GpsDeviceId
            WHERE c.Id = @Id AND c.IsDeleted = 0 AND d.UniqueId = @UniqueId
            """,
            new { request.Id, request.UniqueId, request.Status, request.ErrorMessage },
            cancellationToken: cancellationToken));

        if (rows == 0)
            return ApiResponse<bool>.FailResponse("Command not found.");

        if (!string.IsNullOrWhiteSpace(request.ResponseText) || !string.IsNullOrWhiteSpace(request.ErrorMessage))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO GpsCommandResponses (CommandId, Source, ResponseText, ReceivedAt, CreatedAt)
                VALUES (@CommandId, 'device', @ResponseText, GETUTCDATE(), GETUTCDATE())
                """,
                new { CommandId = request.Id, ResponseText = request.ResponseText ?? request.ErrorMessage },
                cancellationToken: cancellationToken));
        }

        return ApiResponse<bool>.SuccessResponse(true, "Command updated.");
    }
}
