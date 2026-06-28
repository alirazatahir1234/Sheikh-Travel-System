using System.Collections.Concurrent;
using Dapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Infrastructure.Traccar;

public sealed class TraccarSyncOrchestrator(
    ITraccarClient traccar,
    IServiceScopeFactory scopeFactory,
    ITraccarSyncState syncState,
    IOptions<TraccarOptions> options,
    ILogger<TraccarSyncOrchestrator> logger) : ITraccarSyncOrchestrator
{
    private readonly ConcurrentDictionary<int, DateTime> _lastIngested = new();
    private readonly ConcurrentDictionary<int, DateTime> _lastEventSync = new();

    private bool IsTraccarActive => options.Value.Enabled && options.Value.IsConfigured;

    private string TraccarInactiveReason =>
        !options.Value.Enabled
            ? "Traccar sync is disabled."
            : !options.Value.IsConfigured
                ? "Traccar BaseUrl is not configured."
                : "";

    public async Task<TraccarSyncRunResult> RunManualSyncAsync(CancellationToken ct = default)
    {
        syncState.MarkRunning(true);
        try
        {
            var jobs = new List<TraccarSyncJobResult>();
            jobs.Add((await SyncDevicesAsync(ct)).Jobs[0]);
            jobs.Add((await SyncPositionsAsync(ct)).Jobs[0]);
            jobs.Add((await SyncEventsAsync(ct)).Jobs[0]);
            return new TraccarSyncRunResult(DateTime.UtcNow, jobs);
        }
        finally
        {
            syncState.MarkRunning(false);
        }
    }

    public async Task<TraccarSyncRunResult> SyncTrackerAsync(int gpsDeviceId, CancellationToken ct = default)
    {
        if (!IsTraccarActive)
            return SingleJob("tracker", 0, 0, 0, 0, TraccarInactiveReason);

        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        using var connection = dbFactory.CreateConnection();

        var device = await connection.QueryFirstOrDefaultAsync<(int Id, int? TraccarDeviceId, int? VehicleId)>(
            new CommandDefinition(
                "SELECT Id, TraccarDeviceId, VehicleId FROM GpsDevices WHERE Id = @Id AND IsDeleted = 0",
                new { Id = gpsDeviceId },
                cancellationToken: ct));

        if (device.Id == 0 || !device.TraccarDeviceId.HasValue)
            return SingleJob("tracker", 0, 0, 0, 0, "Tracker is not linked to Traccar.");

        var traccarDeviceId = device.TraccarDeviceId.Value;
        var traccarDevice = await traccar.GetDeviceByIdAsync(traccarDeviceId, ct);
        if (traccarDevice is null)
            return SingleJob("tracker", 0, 0, 0, 0, "Device not found on Traccar server.");

        var lastSeen = traccarDevice.LastUpdate?.ToUniversalTime();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE GpsDevices SET
                Name = @Name, IsActive = @IsActive, LastSeenAt = COALESCE(@LastSeenAt, LastSeenAt),
                LastSyncAt = GETUTCDATE(), UpdatedAt = GETUTCDATE()
            WHERE Id = @Id
            """,
            new
            {
                Id = gpsDeviceId,
                traccarDevice.Name,
                IsActive = !traccarDevice.Disabled,
                LastSeenAt = lastSeen
            },
            cancellationToken: ct));

        var positions = await traccar.GetLivePositionsAsync(ct);
        var pos = positions.FirstOrDefault(p => p.DeviceId == traccarDeviceId);
        var telemetryUpdated = 0;

        if (pos is not null)
        {
            var ignition = pos.Attributes.Ignition;
            var speedKmh = (decimal)(pos.Speed * 1.852);
            var battery = pos.Attributes.BatteryLevel;
            var rssi = pos.Attributes.Rssi;
            var recordedAt = pos.FixTime.ToUniversalTime();

            if (device.VehicleId.HasValue)
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var dto = new IngestPositionDto(
                    VehicleId: device.VehicleId.Value,
                    DriverId: null,
                    BookingId: null,
                    GpsDeviceId: gpsDeviceId,
                    Latitude: pos.Latitude,
                    Longitude: pos.Longitude,
                    Speed: speedKmh,
                    Heading: pos.Course,
                    Altitude: pos.Altitude,
                    Ignition: ignition);

                try
                {
                    await mediator.Send(new IngestPositionCommand(dto), ct);
                    telemetryUpdated = 1;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to ingest position during tracker sync for device {Id}", gpsDeviceId);
                }
            }
            else
            {
                try
                {
                    await GpsDeviceTelemetryUpdater.UpdateAsync(
                        connection, gpsDeviceId, recordedAt, ignition, speedKmh, battery, rssi, ct);
                    telemetryUpdated = 1;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed telemetry update during tracker sync for device {Id}", gpsDeviceId);
                }
            }
        }

        return new TraccarSyncRunResult(DateTime.UtcNow, [
            new TraccarSyncJobResult("tracker", 1, telemetryUpdated > 0 ? 1 : 0, 1, 0)
        ]);
    }

    public async Task<TraccarSyncRunResult> SyncDevicesAsync(CancellationToken ct = default)
    {
        if (!IsTraccarActive)
            return SingleJob("devices", 0, 0, 0, 0, TraccarInactiveReason);

        try
        {
            var traccarDevices = await traccar.GetDevicesAsync(ct);
            if (traccarDevices.Count == 0)
            {
                var empty = SingleJob("devices", 0, 0, 0, 0);
                syncState.RecordJobComplete("devices", empty.Jobs[0]);
                return empty;
            }

            using var scope = scopeFactory.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var connection = dbFactory.CreateConnection();

            int imported = 0, updated = 0, skipped = 0;

            foreach (var td in traccarDevices)
            {
                var isActive = !td.Disabled;
                var lastSeen = td.LastUpdate?.ToUniversalTime();

                var existing = await connection.QuerySingleOrDefaultAsync<(int Id, int? TraccarDeviceId)>(
                    new CommandDefinition(
                        "SELECT Id, TraccarDeviceId FROM GpsDevices WHERE UniqueId = @UniqueId AND IsDeleted = 0",
                        new { td.UniqueId },
                        cancellationToken: ct));

                if (existing.Id == 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition("""
                        INSERT INTO GpsDevices (UniqueId, Name, TraccarDeviceId, IsActive, LastSeenAt, CreatedAt, IsDeleted)
                        VALUES (@UniqueId, @Name, @TraccarDeviceId, @IsActive, @LastSeenAt, GETUTCDATE(), 0)
                        """,
                        new { td.UniqueId, td.Name, TraccarDeviceId = td.Id, IsActive = isActive, LastSeenAt = lastSeen },
                        cancellationToken: ct));
                    imported++;
                }
                else if (existing.TraccarDeviceId != td.Id)
                {
                    await connection.ExecuteAsync(new CommandDefinition("""
                        UPDATE GpsDevices SET TraccarDeviceId = @TraccarDeviceId, Name = @Name, IsActive = @IsActive,
                            LastSeenAt = COALESCE(@LastSeenAt, LastSeenAt), UpdatedAt = GETUTCDATE()
                        WHERE Id = @Id
                        """,
                        new { TraccarDeviceId = td.Id, Name = td.Name, IsActive = isActive, LastSeenAt = lastSeen, Id = existing.Id },
                        cancellationToken: ct));
                    updated++;
                }
                else
                {
                    await connection.ExecuteAsync(new CommandDefinition("""
                        UPDATE GpsDevices SET Name = @Name, IsActive = @IsActive,
                            LastSeenAt = COALESCE(@LastSeenAt, LastSeenAt), UpdatedAt = GETUTCDATE()
                        WHERE Id = @Id
                        """,
                        new { Name = td.Name, IsActive = isActive, LastSeenAt = lastSeen, Id = existing.Id },
                        cancellationToken: ct));
                    skipped++;
                }
            }

            var result = SingleJob("devices", traccarDevices.Count, imported, updated, skipped);
            syncState.RecordJobComplete("devices", result.Jobs[0]);
            logger.LogDebug("Traccar device sync: imported {Imported}, updated {Updated}, skipped {Skipped}",
                imported, updated, skipped);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar device sync failed");
            var failed = SingleJob("devices", 0, 0, 0, 0, ex.Message);
            syncState.RecordJobComplete("devices", failed.Jobs[0]);
            return failed;
        }
    }

    public async Task<TraccarSyncRunResult> SyncPositionsAsync(CancellationToken ct = default)
    {
        if (!IsTraccarActive)
            return SingleJob("positions", 0, 0, 0, 0, TraccarInactiveReason);

        try
        {
            var positions = await traccar.GetLivePositionsAsync(ct);
            if (positions.Count == 0)
            {
                var empty = SingleJob("positions", 0, 0, 0, 0);
                syncState.RecordJobComplete("positions", empty.Jobs[0]);
                return empty;
            }

            using var scope = scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var connection = dbFactory.CreateConnection();

            var allDevices = (await connection.QueryAsync<(int TraccarDeviceId, int GpsDeviceId, int? VehicleId)>(
                new CommandDefinition(
                    "SELECT TraccarDeviceId, Id AS GpsDeviceId, VehicleId FROM GpsDevices WHERE TraccarDeviceId IS NOT NULL AND IsDeleted = 0",
                    cancellationToken: ct)))
                .ToDictionary(x => x.TraccarDeviceId);

            if (allDevices.Count == 0)
            {
                var none = SingleJob("positions", 0, 0, 0, 0);
                syncState.RecordJobComplete("positions", none.Jobs[0]);
                return none;
            }

            var linkedDevices = allDevices
                .Where(kv => kv.Value.VehicleId.HasValue)
                .ToDictionary(kv => kv.Key, kv => (kv.Value.GpsDeviceId, VehicleId: kv.Value.VehicleId!.Value));

            int ingested = 0;
            int telemetryOnly = 0;

            foreach (var pos in positions)
            {
                if (!allDevices.TryGetValue(pos.DeviceId, out var device))
                    continue;

                if (_lastIngested.TryGetValue(pos.DeviceId, out var last) && pos.FixTime <= last)
                    continue;

                var ignition = pos.Attributes.Ignition;
                var recordedAt = pos.FixTime.ToUniversalTime();
                var speedKmh = (decimal)(pos.Speed * 1.852);
                var battery = pos.Attributes.BatteryLevel;
                var rssi = pos.Attributes.Rssi;

                if (linkedDevices.TryGetValue(pos.DeviceId, out var linked))
                {
                    var dto = new IngestPositionDto(
                        VehicleId: linked.VehicleId,
                        DriverId: null,
                        BookingId: null,
                        GpsDeviceId: linked.GpsDeviceId,
                        Latitude: pos.Latitude,
                        Longitude: pos.Longitude,
                        Speed: speedKmh,
                        Heading: pos.Course,
                        Altitude: pos.Altitude,
                        Ignition: ignition);

                    try
                    {
                        await mediator.Send(new IngestPositionCommand(dto), ct);
                        ingested++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to ingest position for Traccar device {DeviceId}", pos.DeviceId);
                    }
                    finally
                    {
                        // Always advance the watermark — prevents infinite retry of a permanently-failing position.
                        _lastIngested[pos.DeviceId] = pos.FixTime;
                    }
                }
                else
                {
                    try
                    {
                        await GpsDeviceTelemetryUpdater.UpdateAsync(
                            connection, device.GpsDeviceId, recordedAt, ignition,
                            speedKmh, battery, rssi, ct);
                        _lastIngested[pos.DeviceId] = pos.FixTime;
                        telemetryOnly++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed telemetry update for Traccar device {DeviceId}", pos.DeviceId);
                    }
                }
            }

            var result = new TraccarSyncRunResult(DateTime.UtcNow, [
                new TraccarSyncJobResult("positions", positions.Count, ingested, telemetryOnly, 0)
            ]);
            syncState.RecordJobComplete("positions", result.Jobs[0]);
            if (ingested > 0 || telemetryOnly > 0)
                logger.LogDebug("Traccar position sync: ingested {Ingested}, telemetry-only {TelemetryOnly}",
                    ingested, telemetryOnly);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar position sync failed");
            var failed = SingleJob("positions", 0, 0, 0, 0, ex.Message);
            syncState.RecordJobComplete("positions", failed.Jobs[0]);
            return failed;
        }
    }

    public async Task<TraccarSyncRunResult> SyncEventsAsync(CancellationToken ct = default)
    {
        if (!IsTraccarActive)
            return SingleJob("events", 0, 0, 0, 0, TraccarInactiveReason);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var connection = dbFactory.CreateConnection();

            var linked = (await connection.QueryAsync<(int TraccarDeviceId, int VehicleId)>(
                new CommandDefinition(
                    @"SELECT TraccarDeviceId, VehicleId FROM GpsDevices
                      WHERE TraccarDeviceId IS NOT NULL AND VehicleId IS NOT NULL AND IsDeleted = 0",
                    cancellationToken: ct))).ToList();

            if (linked.Count == 0)
            {
                var empty = SingleJob("events", 0, 0, 0, 0);
                syncState.RecordJobComplete("events", empty.Jobs[0]);
                return empty;
            }

            int imported = 0;
            int skipped = 0;
            var to = DateTime.UtcNow;
            const int lookbackMinutes = 2;

            foreach (var device in linked)
            {
                var from = _lastEventSync.TryGetValue(device.TraccarDeviceId, out var last)
                    ? last.AddMinutes(-1)
                    : to.AddMinutes(-lookbackMinutes);

                IReadOnlyList<TraccarEvent> events;
                try
                {
                    events = await traccar.GetEventsAsync(device.TraccarDeviceId, from, to, ct: ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to fetch Traccar events for device {DeviceId}", device.TraccarDeviceId);
                    continue;
                }

                foreach (var ev in events)
                {
                    var externalId = $"traccar:{ev.Id}";
                    var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                        @"SELECT CASE WHEN EXISTS(
                            SELECT 1 FROM GpsAlertEvents WHERE ExternalEventId = @ExternalEventId AND IsDeleted = 0
                          ) THEN 1 ELSE 0 END",
                        new { ExternalEventId = externalId },
                        cancellationToken: ct));

                    if (exists)
                    {
                        skipped++;
                        continue;
                    }

                    var eventType = MapEventType(ev.Type);
                    var message = FormatEventMessage(ev.Type);
                    var timestamp = ev.EventTime.ToUniversalTime();

                    await connection.ExecuteAsync(new CommandDefinition(
                        @"INSERT INTO GpsAlertEvents
                          (RuleId, VehicleId, GeofenceId, EventType, Latitude, Longitude, Speed, Message,
                           Timestamp, ExternalEventId, CreatedAt, IsDeleted)
                          VALUES (NULL, @VehicleId, NULL, @EventType, 0, 0, 0, @Message,
                                  @Timestamp, @ExternalEventId, GETUTCDATE(), 0)",
                        new
                        {
                            device.VehicleId,
                            EventType = eventType,
                            Message = message,
                            Timestamp = timestamp,
                            ExternalEventId = externalId
                        },
                        cancellationToken: ct));
                    imported++;
                }

                _lastEventSync[device.TraccarDeviceId] = to;
            }

            var result = SingleJob("events", linked.Count, imported, 0, skipped);
            syncState.RecordJobComplete("events", result.Jobs[0]);
            if (imported > 0)
                logger.LogDebug("Traccar event sync: imported {Imported}, skipped {Skipped}", imported, skipped);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar event sync failed");
            var failed = SingleJob("events", 0, 0, 0, 0, ex.Message);
            syncState.RecordJobComplete("events", failed.Jobs[0]);
            return failed;
        }
    }

    private static string MapEventType(string traccarType) => traccarType switch
    {
        "deviceOnline" => "device_online",
        "deviceOffline" => "device_offline",
        "geofenceEnter" => "geofence_enter",
        "geofenceExit" => "geofence_exit",
        "alarm" => "alarm",
        _ => traccarType.ToLowerInvariant()
    };

    private static string FormatEventMessage(string traccarType) => traccarType switch
    {
        "deviceOnline" => "Device came online",
        "deviceOffline" => "Device went offline",
        "geofenceEnter" => "Entered geofence",
        "geofenceExit" => "Exited geofence",
        "alarm" => "Device alarm",
        _ => $"Traccar event: {traccarType}"
    };

    private static TraccarSyncRunResult SingleJob(
        string job, int processed, int imported, int updated, int skipped, string? error = null) =>
        new(DateTime.UtcNow, [new TraccarSyncJobResult(job, processed, imported, updated, skipped, error)]);
}
