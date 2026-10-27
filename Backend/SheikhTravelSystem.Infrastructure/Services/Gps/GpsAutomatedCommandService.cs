using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class GpsAutomatedCommandService(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IGpsCommandTranslator commandTranslator,
    IGpsTransportRouter transportRouter,
    IOptions<GpsSettings> gpsSettings) : IGpsAutomatedCommandService
{
    public async Task<ApiResponse<int>> QueueAsync(
        int tenantId,
        int gpsDeviceId,
        string commandType,
        string reason,
        CancellationToken cancellationToken = default)
    {
        tenantContext.SetTenant(tenantId);
        var definition = GpsCommandCatalog.Find(commandType);
        if (definition is null)
            return ApiResponse<int>.FailResponse("Unknown command type.");

        using var connection = dbFactory.CreateConnection();
        var device = await connection.QueryFirstOrDefaultAsync<(int Id, int? TraccarDeviceId, int? TrackerModelId)>(
            new CommandDefinition(
                """
                SELECT Id, TraccarDeviceId, TrackerModelId
                FROM GpsDevices
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
                """,
                new { Id = gpsDeviceId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (device.Id == 0)
            return ApiResponse<int>.FailResponse("Device not found.");

        GpsTranslateResult? translated = null;
        if (device.TrackerModelId is > 0)
        {
            translated = await commandTranslator.TranslateAsync(new GpsTranslateRequest(
                gpsDeviceId, device.TrackerModelId, commandType, null), cancellationToken);
        }

        var traccarType = translated?.Success == true ? translated.TraccarType : definition.TraccarType;
        var transport = translated?.Success == true ? translated.Transport : (definition.TraccarType is null ? "Sms" : "Traccar");
        var rendered = translated?.Success == true ? translated.RenderedPayload : null;

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO GpsDeviceCommands
                (GpsDeviceId, CommandType, CommandKey, Transport, RenderedPayload, Status,
                 Reason, MaxRetries, TenantId, RequestedBy, RequestedAt, CreatedAt, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES
                (@GpsDeviceId, @CommandType, @CommandKey, @Transport, @RenderedPayload, N'pending',
                 @Reason, @MaxRetries, @TenantId, N'system:fatigue', GETUTCDATE(), GETUTCDATE(), 0)
            """,
            new
            {
                GpsDeviceId = gpsDeviceId,
                CommandType = commandType,
                CommandKey = commandType,
                Transport = transport,
                RenderedPayload = rendered,
                Reason = reason,
                MaxRetries = gpsSettings.Value.CommandMaxRetries,
                TenantId = tenantId
            },
            cancellationToken: cancellationToken));

        if (device.TraccarDeviceId.HasValue && traccarType is not null)
        {
            var sent = await transportRouter.SendAsync(new GpsTransportSendRequest(
                "Traccar",
                device.TraccarDeviceId,
                traccarType,
                rendered ?? traccarType,
                null), cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE GpsDeviceCommands
                SET Status = @Status, ErrorMessage = @Err, UpdatedAt = GETUTCDATE()
                WHERE Id = @Id
                """,
                new
                {
                    Id = id,
                    Status = sent.Success ? "sent" : "pending",
                    Err = sent.Error
                },
                cancellationToken: cancellationToken));
        }

        return ApiResponse<int>.SuccessResponse(id, "Automated command queued.");
    }
}
