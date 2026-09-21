using SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Commands;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Queries;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class GpsDeviceRepository(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IAppCache cache,
    ITraccarClient traccar,
    IOptions<TraccarOptions> traccarOptions,
    IGpsCommandSafetyChecker commandSafetyChecker,
    IGpsCommandTranslator commandTranslator,
    IGpsTransportRouter transportRouter,
    INotificationDecisionEngine decisionEngine,
    IOptions<GpsSettings> gpsSettings,
    ITraccarSyncOrchestrator traccarSync,
    ILogger<GpsDeviceRepository> logger) : IGpsDeviceRepository
{
    public async Task<int> CountTraccarLinkedAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"""
            SELECT COUNT(*)
            FROM GpsDevices d
            LEFT JOIN Vehicles v ON v.Id = d.VehicleId AND v.IsDeleted = 0
            WHERE d.IsDeleted = 0
              AND d.TraccarDeviceId IS NOT NULL
              {TrackerTenantSql.DeviceScopeFilter}
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    public async Task<ApiResponse<bool>> CancelDeviceCommandAsync(CancelDeviceCommandCommand request, CancellationToken cancellationToken = default)
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

    public async Task<ApiResponse<bool>> CompleteDeviceCommandAsync(CompleteDeviceCommandCommand request, CancellationToken cancellationToken = default)
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

    public async Task<ApiResponse<List<GpsDeviceDto>>> GetGpsDevicesAsync(GetGpsDevicesQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<GpsDeviceDto>(new CommandDefinition(
            @"SELECT d.Id, d.VehicleId,
                     CASE WHEN v.Status = 5 THEN NULL ELSE v.Name END AS VehicleName,
                     CASE WHEN v.Status = 5 OR v.RegistrationNumber LIKE 'DRAFT-%' THEN NULL
                          ELSE v.RegistrationNumber END AS PlateNumber,
                     COALESCE(assignDrv.DriverName, drVcl.FullName) AS DriverName,
                     d.UniqueId, d.Name, d.Protocol,
                     d.SupportsEngineCutoff, d.SupportsRelay, d.LastIgnition, d.LastSeenAt, d.IsActive,
                     CASE WHEN d.LastSeenAt IS NOT NULL AND d.LastSeenAt > DATEADD(minute, -30, GETUTCDATE())
                          THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsOnline,
                     COALESCE(d.LastSpeed, vcl.Speed) AS LastSpeed,
                     d.LastBatteryLevel, d.LastRssi,
                     d.TraccarDeviceId,
                     CASE WHEN d.TraccarDeviceId IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsTraccarLinked,
                     CASE WHEN d.UniqueId NOT LIKE '%[^0-9]%' AND LEN(d.UniqueId) BETWEEN 14 AND 20
                          THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsValidImei,
                     d.Model, d.SimNumber, d.Vendor,
                     d.SerialNumber, d.InstallationDate, d.InstalledBy, d.InstallationNotes, d.RelayOutput
              FROM GpsDevices d
              LEFT JOIN Vehicles v ON v.Id = d.VehicleId AND v.IsDeleted = 0
              LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = d.VehicleId
              LEFT JOIN Drivers drVcl ON drVcl.Id = vcl.DriverId AND drVcl.IsDeleted = 0
              OUTER APPLY (
                  SELECT TOP 1 dr.FullName AS DriverName
                  FROM AssignmentHistory a
                  INNER JOIN Drivers dr ON dr.Id = a.DriverId AND dr.IsDeleted = 0
                  WHERE a.VehicleId = d.VehicleId AND a.IsDeleted = 0
                    AND a.Status IN (N'Active', N'Scheduled') AND a.DriverId IS NOT NULL
                  ORDER BY CASE WHEN a.Status = N'Active' THEN 0 ELSE 1 END, a.StartAt DESC
              ) assignDrv
              WHERE d.IsDeleted = 0
              ORDER BY CASE WHEN v.Name IS NULL THEN 1 ELSE 0 END, v.Name, d.Name",
            cancellationToken: cancellationToken));

        var items = rows
            .Select(r => r with
            {
                LastSeenAt = GpsUtcDateTime.AsUtc(r.LastSeenAt),
                InstallationDate = GpsUtcDateTime.AsUtc(r.InstallationDate)
            })
            .ToList();

        return ApiResponse<List<GpsDeviceDto>>.SuccessResponse(items);
        }

    public async Task<ApiResponse<List<GpsDeviceCommandDto>>> GetDeviceCommandsAsync(GetDeviceCommandsQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT c.Id, c.GpsDeviceId, d.Name AS DeviceName, c.CommandType, c.Status,
                   c.RequestedBy, c.RequestedAt, c.CompletedAt,
                   c.RetryCount, c.MaxRetries, c.ErrorMessage, c.NextRetryAt, c.Reason
            FROM GpsDeviceCommands c
            INNER JOIN GpsDevices d ON d.Id = c.GpsDeviceId
            WHERE c.GpsDeviceId = @GpsDeviceId AND c.IsDeleted = 0
            """;

        if (!string.IsNullOrWhiteSpace(request.Status)) sql += " AND c.Status = @Status";
        if (!string.IsNullOrWhiteSpace(request.CommandType)) sql += " AND c.CommandType = @CommandType";
        if (request.From.HasValue) sql += " AND c.RequestedAt >= @From";
        if (request.To.HasValue) sql += " AND c.RequestedAt <= @To";

        sql += " ORDER BY c.RequestedAt DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        var rows = await connection.QueryAsync<GpsDeviceCommandDto>(new CommandDefinition(
            sql,
            new
            {
                request.GpsDeviceId,
                request.Status,
                request.CommandType,
                request.From,
                request.To,
                Offset = (Math.Max(request.Page, 1) - 1) * request.PageSize,
                request.PageSize
            },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GpsDeviceCommandDto>>.SuccessResponse(rows.ToList());
        }

    public async Task<ApiResponse<GpsDeviceCommandDetailDto>> GetDeviceCommandByIdAsync(GetDeviceCommandByIdQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var raw = await connection.QueryFirstOrDefaultAsync<(
            int Id, int GpsDeviceId, string? DeviceName, string CommandType, string Status,
            string? RequestedBy, DateTime RequestedAt, DateTime? CompletedAt,
            int RetryCount, int MaxRetries, string? ErrorMessage, DateTime? NextRetryAt, string? Reason,
            string? Attributes)>(
            new CommandDefinition(
                """
                SELECT c.Id, c.GpsDeviceId, d.Name AS DeviceName, c.CommandType, c.Status,
                       c.RequestedBy, c.RequestedAt, c.CompletedAt,
                       c.RetryCount, c.MaxRetries, c.ErrorMessage, c.NextRetryAt, c.Reason,
                       c.Attributes
                FROM GpsDeviceCommands c
                INNER JOIN GpsDevices d ON d.Id = c.GpsDeviceId
                WHERE c.Id = @Id AND c.IsDeleted = 0
                """,
                new { request.Id },
                cancellationToken: cancellationToken));

        if (raw.Id == 0)
            return ApiResponse<GpsDeviceCommandDetailDto>.FailResponse("Command not found.");

        var dto = new GpsDeviceCommandDto(raw.Id, raw.GpsDeviceId, raw.DeviceName, raw.CommandType, raw.Status,
            raw.RequestedBy, raw.RequestedAt, raw.CompletedAt, raw.RetryCount, raw.MaxRetries,
            raw.ErrorMessage, raw.NextRetryAt, raw.Reason);

        var responses = (await connection.QueryAsync<GpsCommandResponseDto>(new CommandDefinition(
            """
            SELECT Id, Source, ResponseCode, ResponseText, ReceivedAt
            FROM GpsCommandResponses
            WHERE CommandId = @Id
            ORDER BY ReceivedAt
            """,
            new { request.Id },
            cancellationToken: cancellationToken))).ToList();

        return ApiResponse<GpsDeviceCommandDetailDto>.SuccessResponse(
            new GpsDeviceCommandDetailDto(dto, raw.Attributes, responses));
        }

    public async Task<ApiResponse<List<GpsDeviceCommandDto>>> GetVehicleCommandsAsync(GetVehicleCommandsQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<GpsDeviceCommandDto>(new CommandDefinition(
            """
            SELECT c.Id, c.GpsDeviceId, d.Name AS DeviceName, c.CommandType, c.Status,
                   c.RequestedBy, c.RequestedAt, c.CompletedAt,
                   c.RetryCount, c.MaxRetries, c.ErrorMessage, c.NextRetryAt, c.Reason
            FROM GpsDeviceCommands c
            INNER JOIN GpsDevices d ON d.Id = c.GpsDeviceId
            WHERE d.VehicleId = @VehicleId AND c.IsDeleted = 0
            ORDER BY c.RequestedAt DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """,
            new { request.VehicleId, Offset = (Math.Max(request.Page, 1) - 1) * request.PageSize, request.PageSize },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GpsDeviceCommandDto>>.SuccessResponse(rows.ToList());
        }

    public async Task<ApiResponse<List<GpsDeviceCommandDto>>> GetPendingDeviceCommandsAsync(GetPendingDeviceCommandsQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<GpsDeviceCommandDto>(new CommandDefinition(
            @"SELECT c.Id, c.GpsDeviceId, d.Name AS DeviceName, c.CommandType, c.Status,
                     c.RequestedBy, c.RequestedAt, c.CompletedAt
              FROM GpsDeviceCommands c
              INNER JOIN GpsDevices d ON d.Id = c.GpsDeviceId
              WHERE d.UniqueId = @UniqueId AND c.Status = 'pending' AND c.IsDeleted = 0
              ORDER BY c.RequestedAt ASC",
            new { request.UniqueId },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GpsDeviceCommandDto>>.SuccessResponse(rows.ToList());
        }

    public Task<ApiResponse<List<TrackerBrandDto>>> GetTrackerBrandsAsync(GetTrackerBrandsQuery request, CancellationToken cancellationToken = default)
        => cache.GetOrCreateAsync(
            "trackers:brands",
            AppCacheTtl.TrackerCatalog,
            async ct =>
            {
                using var connection = dbFactory.CreateConnection();
                var rows = await connection.QueryAsync<TrackerBrandDto>(new CommandDefinition(
                    """
                    SELECT Id, Name, LogoUrl, IsActive
                    FROM TrackerBrands
                    WHERE IsActive = 1
                    ORDER BY Name
                    """,
                    cancellationToken: ct));
                return ApiResponse<List<TrackerBrandDto>>.SuccessResponse(rows.ToList());
            },
            cancellationToken);

    public Task<ApiResponse<List<TrackerModelDto>>> GetTrackerModelsAsync(GetTrackerModelsQuery request, CancellationToken cancellationToken = default)
    {
        var key = request.BrandId.HasValue
            ? $"trackers:models:brand:{request.BrandId.Value}"
            : "trackers:models:all";

        return cache.GetOrCreateAsync(
            key,
            AppCacheTtl.TrackerCatalog,
            async ct =>
            {
                using var connection = dbFactory.CreateConnection();
                var sql = """
                    SELECT m.Id, m.TrackerBrandId, b.Name AS BrandName, m.Name, m.Protocol, m.ProtocolLabel,
                           m.DefaultPort, m.SupportsEngineCutOff, m.SupportsFuelSensor, m.SupportsTemperatureSensor,
                           m.SupportsDriverIdentification, m.SupportsCanBus, m.SupportsObd, m.SupportsBle,
                           m.SupportsCamera, m.SupportsRelay, m.SupportsDoorSensor, m.SupportsIgnition,
                           m.SupportsOdometer, m.SupportsBatteryMonitoring, m.DefaultRelayOutput,
                           m.CatalogKey, m.Description, m.IsActive
                    FROM TrackerModels m
                    INNER JOIN TrackerBrands b ON b.Id = m.TrackerBrandId AND b.IsActive = 1
                    WHERE m.IsActive = 1
                    """;

                if (request.BrandId.HasValue)
                    sql += " AND m.TrackerBrandId = @BrandId";

                sql += " ORDER BY b.Name, m.Name";

                var rows = await connection.QueryAsync<TrackerModelDto>(new CommandDefinition(
                    sql,
                    new { request.BrandId },
                    cancellationToken: ct));

                return ApiResponse<List<TrackerModelDto>>.SuccessResponse(rows.ToList());
            },
            cancellationToken);
    }

    public async Task<ApiResponse<bool>> RetryDeviceCommandAsync(RetryDeviceCommandCommand request, CancellationToken cancellationToken = default)
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
                || GpsCommandSafetyRules.RelayNeedsEngineSafetyCheck(row.CommandType, row.RelayPurpose);

            if (needsSafetyCheck)
            {
                // Vehicle state may have changed since the original (failed/timed-out) attempt.
                var safetyError = await commandSafetyChecker.CheckEngineCutoffPreconditionAsync(
                    row.VehicleId, cancellationToken);
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

    public async Task<ApiResponse<List<SupportedCommandDto>>> GetDeviceSupportedCommandsAsync(GetDeviceSupportedCommandsQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var device = await connection.QueryFirstOrDefaultAsync<(int Id, bool SupportsEngineCutoff, bool SupportsRelay, int? TraccarDeviceId)>(
            new CommandDefinition(
                "SELECT Id, SupportsEngineCutoff, SupportsRelay, TraccarDeviceId FROM GpsDevices WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.GpsDeviceId },
                cancellationToken: cancellationToken));

        if (device.Id == 0)
            return ApiResponse<List<SupportedCommandDto>>.FailResponse("Device not found.");

        IReadOnlyList<string> traccarTypes = [];
        var traccarChecked = false;
        if (device.TraccarDeviceId.HasValue)
        {
            traccarTypes = await traccar.GetSupportedCommandTypesAsync(device.TraccarDeviceId.Value, cancellationToken);
            traccarChecked = traccarTypes.Count > 0;
        }

        var result = GpsCommandCatalog.All.Select(def =>
        {
            var hasCapability = def.CapabilityColumn switch
            {
                "SupportsEngineCutoff" => device.SupportsEngineCutoff,
                "SupportsRelay" => device.SupportsRelay,
                _ => true
            };

            if (!hasCapability)
                return new SupportedCommandDto(def.Type, def.Label, false, "Not supported by this device model.");

            if (def.TraccarType is null)
                return new SupportedCommandDto(def.Type, def.Label, true, "Delivery channel not yet configured for this tenant.");

            if (!traccarChecked)
                return new SupportedCommandDto(def.Type, def.Label, true, "Traccar unreachable — showing catalog defaults.");

            var supportedByTraccar = traccarTypes.Contains(def.TraccarType, StringComparer.OrdinalIgnoreCase);
            return supportedByTraccar
                ? new SupportedCommandDto(def.Type, def.Label, true, null)
                : new SupportedCommandDto(def.Type, def.Label, false, "Not supported by this device's Traccar profile.");
        }).ToList();

        return ApiResponse<List<SupportedCommandDto>>.SuccessResponse(result);
        }

    public async Task<ApiResponse<TraccarSyncRunResult>> SyncTrackerAsync(SyncTrackerCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var allowed = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            TrackerTenantSql.DeviceExistsForTenant,
            new { Id = request.Id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (!allowed)
            return ApiResponse<TraccarSyncRunResult>.FailResponse("Tracker not found.");

        var result = await traccarSync.SyncTrackerAsync(request.Id, cancellationToken);
        return ApiResponse<TraccarSyncRunResult>.SuccessResponse(result);
        }


    public async Task<ApiResponse<int>> CreateGpsDeviceAsync(CreateGpsDeviceCommand request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Device;

        var duplicate = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM GpsDevices WHERE UniqueId = @UniqueId AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { dto.UniqueId },
            cancellationToken: cancellationToken));

        if (duplicate)
            return ApiResponse<int>.FailResponse("IMEI already registered.");

        if (dto.VehicleId.HasValue)
        {
            var vehicleError = await ValidateVehicleForDeviceAsync(connection, dto.VehicleId.Value, cancellationToken);
            if (vehicleError is not null)
                return ApiResponse<int>.FailResponse(vehicleError);
        }

        int? traccarDeviceId = null;
        if (traccarOptions.Value.IsConfigured && traccarOptions.Value.Enabled)
        {
            var created = await traccar.CreateDeviceAsync(dto.Name, dto.UniqueId, ct: cancellationToken);
            if (created is null)
                return ApiResponse<int>.FailResponse(
                    "Failed to create device in Traccar. Check server connectivity and credentials.");

            traccarDeviceId = created.Id;
        }

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO GpsDevices (VehicleId, UniqueId, Name, Protocol, Model, SimNumber, Vendor,
              SupportsEngineCutoff, RelayOutput, SerialNumber, InstallationDate, InstalledBy, InstallationNotes,
              TraccarDeviceId, IsActive, CreatedAt, CreatedBy, IsDeleted)
              OUTPUT INSERTED.Id
              VALUES (@VehicleId, @UniqueId, @Name, @Protocol, @Model, @SimNumber, @Vendor,
              @SupportsEngineCutoff, @RelayOutput, @SerialNumber, @InstallationDate, @InstalledBy, @InstallationNotes,
              @TraccarDeviceId, 1, GETUTCDATE(), @CreatedBy, 0)",
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
                RelayOutput = dto.SupportsEngineCutoff ? dto.RelayOutput : null,
                dto.SerialNumber,
                dto.InstallationDate,
                dto.InstalledBy,
                dto.InstallationNotes,
                TraccarDeviceId = traccarDeviceId,
                CreatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "GPS device created.");
        }

    public async Task<ApiResponse<bool>> UpdateGpsDeviceAsync(UpdateGpsDeviceCommand request, CancellationToken cancellationToken = default)
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
            var vehicleError = await ValidateVehicleForDeviceAsync(connection, dto.VehicleId.Value, cancellationToken);
            if (vehicleError is not null)
                return ApiResponse<bool>.FailResponse(vehicleError);
        }

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE GpsDevices SET VehicleId = @VehicleId, Name = @Name, Protocol = @Protocol,
              SupportsEngineCutoff = @SupportsEngineCutoff, RelayOutput = @RelayOutput,
              SimNumber = @SimNumber, SerialNumber = @SerialNumber,
              InstallationDate = @InstallationDate, InstalledBy = @InstalledBy,
              InstallationNotes = @InstallationNotes, IsActive = @IsActive,
              UpdatedAt = GETUTCDATE(), UpdatedBy = @UpdatedBy
              WHERE Id = @Id AND IsDeleted = 0",
            new
            {
                request.Id,
                dto.VehicleId,
                dto.Name,
                dto.Protocol,
                dto.SupportsEngineCutoff,
                RelayOutput = dto.SupportsEngineCutoff ? dto.RelayOutput : null,
                dto.SimNumber,
                dto.SerialNumber,
                dto.InstallationDate,
                dto.InstalledBy,
                dto.InstallationNotes,
                dto.IsActive,
                UpdatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        if (rows == 0)
            return ApiResponse<bool>.FailResponse("Device not found.");

        if (traccarOptions.Value.IsConfigured && traccarOptions.Value.Enabled && existing.TraccarDeviceId.HasValue)
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

    public async Task<ApiResponse<bool>> DeleteGpsDeviceAsync(DeleteGpsDeviceCommand request, CancellationToken cancellationToken = default)
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

        if (traccarOptions.Value.IsConfigured && traccarOptions.Value.Enabled && traccarDeviceId.HasValue)
            await traccar.DeleteDeviceAsync(traccarDeviceId.Value, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "GPS device deleted.");
        }

    public async Task<ApiResponse<int>> SendDeviceCommandAsync(SendDeviceCommandCommand request, CancellationToken cancellationToken = default)
    {
        var definition = GpsCommandCatalog.Find(request.Command.CommandType);
        if (definition is null)
            return ApiResponse<int>.FailResponse("Unknown command type.");

        if (!currentUser.HasPermission(definition.Permission))
            return ApiResponse<int>.FailResponse("Insufficient permission for this command type.");

        using var connection = dbFactory.CreateConnection();
        var device = await connection.QueryFirstOrDefaultAsync<(int Id, bool SupportsEngineCutoff, bool SupportsRelay, string Name, int? TraccarDeviceId, int? VehicleId, string? RelayPurpose, int? TrackerModelId)>(
            new CommandDefinition(
                """
                SELECT Id, SupportsEngineCutoff, SupportsRelay, Name, TraccarDeviceId, VehicleId, RelayPurpose, TrackerModelId
                FROM GpsDevices WHERE Id = @Id AND IsDeleted = 0
                """,
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
                || GpsCommandSafetyRules.RelayNeedsEngineSafetyCheck(request.Command.CommandType, device.RelayPurpose);

            if (needsSafetyCheck)
            {
                var safetyError = await commandSafetyChecker.CheckEngineCutoffPreconditionAsync(device.VehicleId, cancellationToken);
                if (safetyError is not null)
                    return ApiResponse<int>.FailResponse(safetyError);
            }
        }

        var duplicatePending = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT CASE WHEN EXISTS(
                SELECT 1 FROM GpsDeviceCommands
                WHERE GpsDeviceId = @GpsDeviceId AND CommandType = @CommandType
                  AND Status IN ('pending', 'sent', 'PendingApproval') AND IsDeleted = 0
            ) THEN 1 ELSE 0 END
            """,
            new { request.Command.GpsDeviceId, request.Command.CommandType },
            cancellationToken: cancellationToken));

        if (duplicatePending)
            return ApiResponse<int>.FailResponse($"A {definition.Label} command is already in flight for this device.");

        Dictionary<string, string>? stringParams = null;
        if (request.Command.Attributes is { Count: > 0 })
        {
            stringParams = request.Command.Attributes.ToDictionary(
                kv => kv.Key,
                kv => kv.Value?.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
        }

        GpsTranslateResult? translated = null;
        if (device.TrackerModelId is > 0)
        {
            translated = await commandTranslator.TranslateAsync(new GpsTranslateRequest(
                request.Command.GpsDeviceId,
                device.TrackerModelId,
                request.Command.CommandType,
                stringParams), cancellationToken);

            if (!translated.Success && definition.TraccarType is null)
                return ApiResponse<int>.FailResponse(translated.Error ?? "Command translation failed.");
        }

        var requiresApproval = translated?.RequiresApproval == true
            && !currentUser.HasPermission(PlatformPermissions.GpsApprove)
            && !currentUser.HasPermission("Gps.CommandApprove");

        if (requiresApproval && string.IsNullOrWhiteSpace(request.Command.Reason))
            return ApiResponse<int>.FailResponse("Reason is required for this command.");

        var attributesJson = request.Command.Attributes is { Count: > 0 }
            ? JsonSerializer.Serialize(request.Command.Attributes)
            : null;

        var initialStatus = requiresApproval ? "PendingApproval" : "pending";
        var traccarType = translated?.Success == true ? translated.TraccarType : definition.TraccarType;
        var transport = translated?.Success == true ? translated.Transport : (definition.TraccarType is null ? "Sms" : "Traccar");
        var rendered = translated?.Success == true ? translated.RenderedPayload : null;

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO GpsDeviceCommands
                (GpsDeviceId, CommandType, CommandKey, Transport, RenderedPayload, Status, ApprovalStatus,
                 Reason, Attributes, MaxRetries, TenantId, RequestedBy, RequestedAt, CreatedAt, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES
                (@GpsDeviceId, @CommandType, @CommandKey, @Transport, @RenderedPayload, @Status, @ApprovalStatus,
                 @Reason, @Attributes, @MaxRetries, @TenantId, @RequestedBy, GETUTCDATE(), GETUTCDATE(), 0)
            """,
            new
            {
                request.Command.GpsDeviceId,
                request.Command.CommandType,
                CommandKey = request.Command.CommandType,
                Transport = transport,
                RenderedPayload = rendered,
                Status = initialStatus,
                ApprovalStatus = requiresApproval ? "Pending" : null,
                request.Command.Reason,
                Attributes = attributesJson,
                MaxRetries = gpsSettings.Value.CommandMaxRetries,
                TenantId = tenantContext.TenantId,
                RequestedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        if (requiresApproval)
            return ApiResponse<int>.SuccessResponse(id, "Command awaiting approval.");

        var dispatchSucceeded = false;

        if (string.Equals(transport, "Sms", StringComparison.OrdinalIgnoreCase)
            || (traccarType is null && translated?.Success != true))
        {
            var smsResult = await transportRouter.SendAsync(new GpsTransportSendRequest(
                "Sms", null, null, rendered ?? request.Command.CommandType, null,
                stringParams?.GetValueOrDefault("phone")), cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE GpsDeviceCommands SET Status = 'not_configured', ErrorMessage = @Err, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
                new { Id = id, Err = smsResult.Error },
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO GpsCommandResponses (CommandId, Source, ResponseText, ReceivedAt, CreatedAt)
                VALUES (@CommandId, 'system', @Text, GETUTCDATE(), GETUTCDATE())
                """,
                new { CommandId = id, Text = smsResult.Error ?? "No SMS gateway configured." },
                cancellationToken: cancellationToken));
        }
        else if (device.TraccarDeviceId.HasValue && traccarType is not null)
        {
            var attrs = request.Command.Attributes;
            if (string.Equals(traccarType, "custom", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(rendered))
            {
                attrs ??= new Dictionary<string, object>();
                if (!attrs.ContainsKey("data"))
                    attrs["data"] = rendered;
            }

            var sent = await transportRouter.SendAsync(new GpsTransportSendRequest(
                "Traccar",
                device.TraccarDeviceId,
                traccarType,
                rendered ?? traccarType,
                attrs is null ? null : attrs.ToDictionary(kv => kv.Key, kv => kv.Value)),
                cancellationToken);

            if (sent.Success)
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
                    SET ErrorMessage = @Err, NextRetryAt = DATEADD(SECOND, @RetrySeconds, GETUTCDATE()), UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id
                    """,
                    new
                    {
                        Id = id,
                        Err = sent.Error ?? "Traccar dispatch failed",
                        RetrySeconds = gpsSettings.Value.CommandRetryIntervalSeconds
                    },
                    cancellationToken: cancellationToken));
            }
        }
        else if (device.TraccarDeviceId.HasValue && definition.TraccarType is not null)
        {
            var sent = await traccar.SendCommandAsync(
                device.TraccarDeviceId.Value, definition.TraccarType, request.Command.Attributes, cancellationToken);
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

    public async Task<ApiResponse<List<TrackerDetailDto>>> GetTrackersAsync(GetTrackersQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await connection.QueryAsync<TrackerDetailDto>(new CommandDefinition(
            TrackerSql.ListQuery + TrackerTenantSql.DeviceScopeFilter + """
             ORDER BY CASE WHEN v.Name IS NULL THEN 1 ELSE 0 END, v.Name, d.Name
             """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
        return ApiResponse<List<TrackerDetailDto>>.SuccessResponse(rows.ToList());
        }

    public async Task<ApiResponse<TrackerDetailDto>> GetTrackerByIdAsync(GetTrackerByIdQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await connection.QueryFirstOrDefaultAsync<TrackerDetailDto>(new CommandDefinition(
            TrackerSql.ListQuery + TrackerTenantSql.DeviceScopeFilter + " AND d.Id = @Id",
            new { request.Id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        return row is null
            ? ApiResponse<TrackerDetailDto>.FailResponse("Tracker not found.")
            : ApiResponse<TrackerDetailDto>.SuccessResponse(row);
        }

    public async Task<ApiResponse<List<TrackerAssignmentDto>>> GetTrackerAssignmentsAsync(GetTrackerAssignmentsQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var allowed = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            TrackerTenantSql.DeviceExistsForTenant,
            new { Id = request.TrackerId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (!allowed)
            return ApiResponse<List<TrackerAssignmentDto>>.FailResponse("Tracker not found.");

        var rows = await connection.QueryAsync<TrackerAssignmentDto>(new CommandDefinition(
            """
            SELECT a.Id, a.GpsDeviceId, a.VehicleId,
                   v.Name AS VehicleName, v.RegistrationNumber AS PlateNumber,
                   a.DriverId, dr.FullName AS DriverName,
                   a.InstalledDate, a.RemovedDate, a.InstalledBy, a.RemovedBy, a.Reason, a.IsActive
            FROM GpsDeviceAssignments a
            INNER JOIN Vehicles v ON v.Id = a.VehicleId AND v.IsDeleted = 0
            LEFT JOIN Drivers dr ON dr.Id = a.DriverId AND dr.IsDeleted = 0
            WHERE a.GpsDeviceId = @TrackerId AND a.TenantId = @TenantId
            ORDER BY a.InstalledDate DESC
            """,
            new { request.TrackerId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        return ApiResponse<List<TrackerAssignmentDto>>.SuccessResponse(rows.ToList());
        }

    public async Task<ApiResponse<List<TrackerInstallVehicleDto>>> GetTrackerInstallVehiclesAsync(GetTrackerInstallVehiclesQuery request, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        if (request.TrackerId is > 0)
        {
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                TrackerTenantSql.DeviceExistsForTenant,
                new { Id = request.TrackerId.Value, TenantId = tenantId },
                cancellationToken: cancellationToken));

            if (!exists)
                return ApiResponse<List<TrackerInstallVehicleDto>>.FailResponse("Tracker not found.");
        }

        var rows = await connection.QueryAsync<TrackerInstallVehicleDto>(new CommandDefinition(
            """
            SELECT
                v.Id AS VehicleId,
                v.Name,
                v.RegistrationNumber AS PlateNumber,
                v.VehicleCode,
                CASE
                    WHEN v.Status = 5 THEN CAST(0 AS BIT)
                    WHEN v.Status = 4 THEN CAST(0 AS BIT)
                    WHEN activeTracker.GpsDeviceId IS NULL THEN CAST(1 AS BIT)
                    WHEN @TrackerId IS NOT NULL AND activeTracker.GpsDeviceId = @TrackerId THEN CAST(0 AS BIT)
                    ELSE CAST(0 AS BIT)
                END AS IsSelectable,
                activeTracker.TrackerName AS AssignedTrackerName,
                CASE
                    WHEN v.Status = 5 THEN 'Publish this vehicle before installing a tracker'
                    WHEN v.Status = 4 THEN 'Vehicle is retired'
                    WHEN activeTracker.GpsDeviceId IS NULL THEN NULL
                    WHEN @TrackerId IS NOT NULL AND activeTracker.GpsDeviceId = @TrackerId
                        THEN 'Currently installed on this tracker'
                    WHEN activeTracker.TrackerName IS NOT NULL THEN CONCAT('Already assigned to ', activeTracker.TrackerName)
                    ELSE 'Already has an active tracker'
                END AS BlockedReason
            FROM Vehicles v
            OUTER APPLY (
                SELECT TOP 1 d.Id AS GpsDeviceId, d.Name AS TrackerName
                FROM GpsDevices d
                WHERE d.IsDeleted = 0
                  AND (d.TenantId = @TenantId OR d.TenantId IS NULL)
                  AND (
                      EXISTS (
                          SELECT 1 FROM GpsDeviceAssignments aa
                          WHERE aa.GpsDeviceId = d.Id AND aa.VehicleId = v.Id
                            AND aa.IsActive = 1 AND aa.TenantId = @TenantId
                      )
                      OR d.VehicleId = v.Id
                      OR d.Id = v.GpsDeviceId
                  )
                ORDER BY
                    CASE WHEN EXISTS (
                        SELECT 1 FROM GpsDeviceAssignments aa
                        WHERE aa.GpsDeviceId = d.Id AND aa.VehicleId = v.Id
                          AND aa.IsActive = 1 AND aa.TenantId = @TenantId
                    ) THEN 0 ELSE 1 END,
                    d.UpdatedAt DESC
            ) activeTracker
            WHERE v.TenantId = @TenantId
              AND v.IsDeleted = 0
            ORDER BY IsSelectable DESC, v.Name
            """,
            new { TenantId = tenantId, TrackerId = request.TrackerId },
            cancellationToken: cancellationToken));

        return ApiResponse<List<TrackerInstallVehicleDto>>.SuccessResponse(rows.ToList());
        }


    private static async Task<string?> ValidateVehicleForDeviceAsync(
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
