using System.Collections.Concurrent;
using System.Data;
using Dapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Infrastructure.Services.Gps;
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
        var telemetryUpdater = scope.ServiceProvider.GetRequiredService<IGpsDeviceTelemetryUpdater>();
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
        // Fill Phone from Traccar only when SheikhGo has neither Phone nor SimNumber — never overwrite.
        var traccarPhone = string.IsNullOrWhiteSpace(traccarDevice.Phone) ? null : traccarDevice.Phone.Trim();
        await ClearTraccarDeviceIdFromOthersAsync(connection, traccarDeviceId, gpsDeviceId, ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE GpsDevices SET
                Name = @Name, IsActive = @IsActive, LastSeenAt = COALESCE(@LastSeenAt, LastSeenAt),
                Phone = CASE
                    WHEN @Phone IS NOT NULL
                         AND (Phone IS NULL OR LTRIM(RTRIM(Phone)) = N'')
                         AND (SimNumber IS NULL OR LTRIM(RTRIM(SimNumber)) = N'')
                    THEN @Phone ELSE Phone END,
                LastSyncAt = GETUTCDATE(), UpdatedAt = GETUTCDATE()
            WHERE Id = @Id
            """,
            new
            {
                Id = gpsDeviceId,
                traccarDevice.Name,
                IsActive = !traccarDevice.Disabled,
                LastSeenAt = lastSeen,
                Phone = traccarPhone
            },
            cancellationToken: ct));

        var positions = await traccar.GetLivePositionsAsync(ct);
        var pos = positions.FirstOrDefault(p => p.DeviceId == traccarDeviceId);
        var telemetryUpdated = 0;

        if (pos is not null)
        {
            var speedKmh = (decimal)(pos.Speed * 1.852);
            var movingThreshold = options.Value.MovingSpeedKmh > 0
                ? options.Value.MovingSpeedKmh
                : GpsFleetStatusBuckets.DefaultMovingSpeedKmh;
            // Prefer device ACC/ignition; motion must not force Ignition Off for UI/VCL.
            var ignition = GpsTelemetryNormalizer.ResolveIgnition(
                pos.Attributes.Ignition, pos.Attributes.Motion, speedKmh, movingThreshold);
            var clearSticky = GpsTelemetryNormalizer.ShouldClearStickyIgnition(
                pos.Attributes.Ignition, pos.Attributes.Motion, speedKmh, movingThreshold);
            var battery = pos.Attributes.BatteryLevel;
            var rssi = pos.Attributes.Rssi;
            var recordedAt = pos.FixTime.ToUniversalTime();

            if (device.VehicleId.HasValue)
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var dto = GpsObdTelemetryMapper.ApplyTraccarObd(new IngestPositionDto(
                    VehicleId: device.VehicleId.Value,
                    DriverId: null,
                    BookingId: null,
                    GpsDeviceId: gpsDeviceId,
                    Latitude: pos.Latitude,
                    Longitude: pos.Longitude,
                    Speed: speedKmh,
                    Heading: pos.Course,
                    Altitude: pos.Altitude,
                    Ignition: ignition,
                    FuelLevel: pos.Attributes.Fuel,
                    BatteryLevel: battery,
                    GsmSignal: rssi,
                    // Traccar reports totalDistance in meters; convert to km for storage/display.
                    TotalDistanceKm: pos.Attributes.TotalDistance.HasValue ? pos.Attributes.TotalDistance / 1000m : null,
                    Address: pos.Address,
                    AlarmType: pos.Attributes.Alarm,
                    Temperature: pos.Attributes.ResolvedTemperature), pos.Attributes);

                try
                {
                    var source = TraccarPositionIngestMapper.ToSourceContext(pos, movingThreshold);
                    await mediator.Send(new IngestPositionCommand(dto, source), ct);
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
                    await telemetryUpdater.UpdateAsync(
                        connection, gpsDeviceId, recordedAt, ignition, speedKmh, battery, rssi,
                        clearStickyIgnition: clearSticky, cancellationToken: ct);
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

                // Match by IMEI/UniqueId only — phone/SIM must never be required for identity.
                var existing = await connection.QuerySingleOrDefaultAsync<(int Id, int? TraccarDeviceId)>(
                    new CommandDefinition(
                        "SELECT Id, TraccarDeviceId FROM GpsDevices WHERE UniqueId = @UniqueId AND IsDeleted = 0",
                        new { td.UniqueId },
                        cancellationToken: ct));

                var phone = string.IsNullOrWhiteSpace(td.Phone) ? null : td.Phone.Trim();

                if (existing.Id == 0)
                {
                    // Claim Traccar id before insert so two rows never share it (position sync key).
                    await ClearTraccarDeviceIdFromOthersAsync(connection, td.Id, keepGpsDeviceId: null, ct);

                    await connection.ExecuteAsync(new CommandDefinition("""
                        INSERT INTO GpsDevices (UniqueId, Name, TraccarDeviceId, Phone, IsActive, LastSeenAt, CreatedAt, IsDeleted)
                        VALUES (@UniqueId, @Name, @TraccarDeviceId, @Phone, @IsActive, @LastSeenAt, GETUTCDATE(), 0)
                        """,
                        new { td.UniqueId, td.Name, TraccarDeviceId = td.Id, Phone = phone, IsActive = isActive, LastSeenAt = lastSeen },
                        cancellationToken: ct));
                    imported++;
                }
                else if (existing.TraccarDeviceId != td.Id)
                {
                    await ClearTraccarDeviceIdFromOthersAsync(connection, td.Id, existing.Id, ct);

                    await connection.ExecuteAsync(new CommandDefinition("""
                        UPDATE GpsDevices SET TraccarDeviceId = @TraccarDeviceId, Name = @Name, IsActive = @IsActive,
                            LastSeenAt = COALESCE(@LastSeenAt, LastSeenAt),
                            Phone = CASE
                                WHEN @Phone IS NOT NULL
                                     AND (Phone IS NULL OR LTRIM(RTRIM(Phone)) = N'')
                                     AND (SimNumber IS NULL OR LTRIM(RTRIM(SimNumber)) = N'')
                                THEN @Phone ELSE Phone END,
                            UpdatedAt = GETUTCDATE()
                        WHERE Id = @Id
                        """,
                        new { TraccarDeviceId = td.Id, Name = td.Name, Phone = phone, IsActive = isActive, LastSeenAt = lastSeen, Id = existing.Id },
                        cancellationToken: ct));
                    updated++;
                }
                else
                {
                    // Same Traccar id already linked — still clear any accidental duplicate holders.
                    await ClearTraccarDeviceIdFromOthersAsync(connection, td.Id, existing.Id, ct);

                    await connection.ExecuteAsync(new CommandDefinition("""
                        UPDATE GpsDevices SET Name = @Name, IsActive = @IsActive,
                            LastSeenAt = COALESCE(@LastSeenAt, LastSeenAt),
                            Phone = CASE
                                WHEN @Phone IS NOT NULL
                                     AND (Phone IS NULL OR LTRIM(RTRIM(Phone)) = N'')
                                     AND (SimNumber IS NULL OR LTRIM(RTRIM(SimNumber)) = N'')
                                THEN @Phone ELSE Phone END,
                            UpdatedAt = GETUTCDATE()
                        WHERE Id = @Id
                        """,
                        new { Name = td.Name, Phone = phone, IsActive = isActive, LastSeenAt = lastSeen, Id = existing.Id },
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

        syncState.MarkRunning(true);
        try
        {
            var positions = await traccar.GetLivePositionsAsync(ct);
            if (positions.Count == 0)
            {
                ApplyAdaptiveInterval([]);
                var empty = SingleJob("positions", 0, 0, 0, 0);
                syncState.RecordJobComplete("positions", empty.Jobs[0]);
                return empty;
            }

            using var scope = scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            var telemetryUpdater = scope.ServiceProvider.GetRequiredService<IGpsDeviceTelemetryUpdater>();
            using var connection = dbFactory.CreateConnection();

            // PreferCanonical: duplicate TraccarDeviceId must not throw (ToDictionary crash stopped all live ingest).
            var deviceRows = await connection.QueryAsync<(int TraccarDeviceId, int GpsDeviceId, int? VehicleId, DateTime? LastSeenAt)>(
                new CommandDefinition(
                    """
                    SELECT TraccarDeviceId, Id AS GpsDeviceId, VehicleId, LastSeenAt
                    FROM GpsDevices
                    WHERE TraccarDeviceId IS NOT NULL AND IsDeleted = 0
                    """,
                    cancellationToken: ct));
            var allDevices = TraccarDeviceLinkResolver.PreferCanonical(
                deviceRows.Select(r => new TraccarDeviceLinkResolver.DeviceLink(
                    r.TraccarDeviceId, r.GpsDeviceId, r.VehicleId, r.LastSeenAt)));

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
            int heartbeats = 0;
            var adaptiveSamples = new List<TraccarAdaptiveInterval.Sample>(positions.Count);

            foreach (var pos in positions)
            {
                if (!allDevices.TryGetValue(pos.DeviceId, out var device))
                    continue;

                var speedKmh = (decimal)(pos.Speed * 1.852);
                var movingThreshold = options.Value.MovingSpeedKmh > 0
                    ? options.Value.MovingSpeedKmh
                    : GpsFleetStatusBuckets.DefaultMovingSpeedKmh;
                // Prefer device ACC; never invent OFF. Clear sticky ON separately when ACC omitted + settled.
                var ignition = GpsTelemetryNormalizer.ResolveIgnition(
                    pos.Attributes.Ignition, pos.Attributes.Motion, speedKmh, movingThreshold);
                var clearSticky = GpsTelemetryNormalizer.ShouldClearStickyIgnition(
                    pos.Attributes.Ignition, pos.Attributes.Motion, speedKmh, movingThreshold);
                adaptiveSamples.Add(new TraccarAdaptiveInterval.Sample(speedKmh, ignition, pos.Attributes.Alarm));

                // Jimi parked keepalives advance serverTime/deviceTime while fixTime stays frozen.
                // Online badge uses VCL.LastUpdate / GpsDevices.LastSeenAt — must follow connectivity, not only GNSS fix.
                var seenAt = MaxUtc(pos.ServerTime, pos.DeviceTime, pos.FixTime);

                if (_lastIngested.TryGetValue(pos.DeviceId, out var last) && pos.FixTime <= last)
                {
                    int? heartbeatVehicleId = linkedDevices.TryGetValue(pos.DeviceId, out var linkedHb)
                        ? linkedHb.VehicleId
                        : null;
                    await RefreshConnectivityAsync(
                        connection, device.GpsDeviceId, heartbeatVehicleId, seenAt, ct);
                    heartbeats++;
                    continue;
                }

                var battery = pos.Attributes.BatteryLevel;
                var rssi = pos.Attributes.Rssi;

                if (linkedDevices.TryGetValue(pos.DeviceId, out var linked))
                {
                    var dto = GpsObdTelemetryMapper.ApplyTraccarObd(new IngestPositionDto(
                        VehicleId: linked.VehicleId,
                        DriverId: null,
                        BookingId: null,
                        GpsDeviceId: linked.GpsDeviceId,
                        Latitude: pos.Latitude,
                        Longitude: pos.Longitude,
                        Speed: speedKmh,
                        Heading: pos.Course,
                        Altitude: pos.Altitude,
                        Ignition: ignition,
                        FuelLevel: pos.Attributes.Fuel,
                        BatteryLevel: battery,
                        GsmSignal: rssi,
                        // Traccar reports totalDistance in meters; convert to km for storage/display.
                        TotalDistanceKm: pos.Attributes.TotalDistance.HasValue ? pos.Attributes.TotalDistance / 1000m : null,
                        Address: pos.Address,
                        AlarmType: pos.Attributes.Alarm,
                        Temperature: pos.Attributes.ResolvedTemperature), pos.Attributes);

                    try
                    {
                        var source = TraccarPositionIngestMapper.ToSourceContext(pos, movingThreshold);
                        await mediator.Send(new IngestPositionCommand(dto, source), ct);
                        await RefreshConnectivityAsync(
                            connection, linked.GpsDeviceId, linked.VehicleId, seenAt, ct);
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
                        await telemetryUpdater.UpdateAsync(
                            connection, device.GpsDeviceId, seenAt, ignition,
                            speedKmh, battery, rssi,
                            clearStickyIgnition: clearSticky, cancellationToken: ct);
                        _lastIngested[pos.DeviceId] = pos.FixTime;
                        telemetryOnly++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed telemetry update for Traccar device {DeviceId}", pos.DeviceId);
                    }
                }
            }

            ApplyAdaptiveInterval(adaptiveSamples);

            var result = new TraccarSyncRunResult(DateTime.UtcNow, [
                new TraccarSyncJobResult("positions", positions.Count, ingested, telemetryOnly + heartbeats, 0)
            ]);
            syncState.RecordJobComplete("positions", result.Jobs[0]);
            if (ingested > 0 || telemetryOnly > 0 || heartbeats > 0)
                logger.LogDebug(
                    "Traccar position sync: ingested {Ingested}, telemetry-only {TelemetryOnly}, heartbeats {Heartbeats}",
                    ingested, telemetryOnly, heartbeats);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Traccar position sync failed");
            var failed = SingleJob("positions", 0, 0, 0, 0, ex.Message);
            syncState.RecordJobComplete("positions", failed.Jobs[0]);
            return failed;
        }
        finally
        {
            syncState.MarkRunning(false);
        }
    }

    private void ApplyAdaptiveInterval(IReadOnlyList<TraccarAdaptiveInterval.Sample> samples)
    {
        if (!options.Value.AdaptivePositionSync)
        {
            syncState.SetAdaptivePositionInterval(
                Math.Max(1, options.Value.ResolvedPositionIntervalSeconds),
                TraccarAdaptiveInterval.ReasonDefault);
            logger.LogInformation(
                "Traccar adaptive sync disabled — using fixed interval {Seconds}s (Default). Samples={SampleCount}",
                Math.Max(1, options.Value.ResolvedPositionIntervalSeconds),
                samples.Count);
            return;
        }

        var resolved = TraccarAdaptiveInterval.Resolve(samples, options.Value);
        syncState.SetAdaptivePositionInterval(resolved.IntervalSeconds, resolved.Reason);

        var maxSpeed = samples.Count == 0 ? 0m : samples.Max(s => s.SpeedKmh);
        var ignitionOn = samples.Count(s => s.Ignition == true);
        var ignitionOff = samples.Count(s => s.Ignition == false);
        var ignitionUnknown = samples.Count(s => s.Ignition == null);
        var movingCount = samples.Count(s => s.SpeedKmh >= (options.Value.MovingSpeedKmh > 0 ? options.Value.MovingSpeedKmh : 10m));

        logger.LogInformation(
            "Traccar adaptive position interval → {Seconds}s reason={Reason}. Samples={SampleCount} MaxSpeedKmh={MaxSpeed} Moving={MovingCount} IgnitionOn={IgnitionOn} IgnitionOff={IgnitionOff} IgnitionUnknown={IgnitionUnknown}",
            resolved.IntervalSeconds,
            resolved.Reason,
            samples.Count,
            maxSpeed,
            movingCount,
            ignitionOn,
            ignitionOff,
            ignitionUnknown);
    }

    public async Task<TraccarSyncRunResult> SyncEventsAsync(CancellationToken ct = default)
    {
        if (!IsTraccarActive)
            return SingleJob("events", 0, 0, 0, 0, TraccarInactiveReason);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            var alertWriter = scope.ServiceProvider.GetRequiredService<IGpsAlertWriter>();
            using var connection = dbFactory.CreateConnection();

            // Canonicalize first so a duplicate TraccarDeviceId does not double-process events.
            var linkedRows = await connection.QueryAsync<(int TraccarDeviceId, int GpsDeviceId, int? VehicleId, DateTime? LastSeenAt)>(
                new CommandDefinition(
                    """
                    SELECT TraccarDeviceId, Id AS GpsDeviceId, VehicleId, LastSeenAt
                    FROM GpsDevices
                    WHERE TraccarDeviceId IS NOT NULL AND VehicleId IS NOT NULL AND IsDeleted = 0
                    """,
                    cancellationToken: ct));
            var linked = TraccarDeviceLinkResolver.PreferCanonical(
                    linkedRows.Select(r => new TraccarDeviceLinkResolver.DeviceLink(
                        r.TraccarDeviceId, r.GpsDeviceId, r.VehicleId, r.LastSeenAt)))
                .Select(kv => (kv.Value.TraccarDeviceId, VehicleId: kv.Value.VehicleId!.Value))
                .ToList();

            if (linked.Count == 0)
            {
                var empty = SingleJob("events", 0, 0, 0, 0);
                syncState.RecordJobComplete("events", empty.Jobs[0]);
                return empty;
            }

            int imported = 0;
            int skipped = 0;
            var to = DateTime.UtcNow;
            // Cold-start lookback: short windows miss ignition/geofence/offline bursts after API restarts.
            const int lookbackMinutes = 30;

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

                foreach (var ev in events.OrderBy(e => e.EventTime))
                {
                    var externalId = $"traccar:{ev.Id}";
                    var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                        @"SELECT CASE WHEN EXISTS(
                            SELECT 1 FROM GpsAlertEvents WHERE ExternalEventId = @ExternalEventId AND IsDeleted = 0
                          ) THEN 1 ELSE 0 END",
                        new { ExternalEventId = externalId },
                        cancellationToken: ct));

                    // Device ACK for engineStop/Resume — clear false timeouts (noise for alert table).
                    if (string.Equals(ev.Type, "commandResult", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!exists)
                        {
                            var resultText = ev.Attributes?.Result;
                            var cmdId = await connection.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(
                                """
                                SELECT TOP 1 c.Id
                                FROM GpsDeviceCommands c
                                INNER JOIN GpsDevices d ON d.Id = c.GpsDeviceId AND d.IsDeleted = 0
                                WHERE d.TraccarDeviceId = @TraccarDeviceId
                                  AND c.IsDeleted = 0
                                  AND c.Status IN (N'sent', N'pending', N'timeout')
                                  AND c.CommandType IN (N'engineStop', N'engineResume', N'restart', N'positionSingle', N'relayOn', N'relayOff')
                                ORDER BY c.RequestedAt DESC
                                """,
                                new { TraccarDeviceId = device.TraccarDeviceId },
                                cancellationToken: ct));

                            if (cmdId is > 0)
                            {
                                await connection.ExecuteAsync(new CommandDefinition(
                                    """
                                    UPDATE GpsDeviceCommands
                                    SET Status = N'acknowledged',
                                        CompletedAt = SYSUTCDATETIME(),
                                        ErrorMessage = NULL,
                                        UpdatedAt = SYSUTCDATETIME()
                                    WHERE Id = @Id;

                                    INSERT INTO GpsCommandResponses (CommandId, Source, ResponseText, ReceivedAt, CreatedAt)
                                    VALUES (@Id, N'traccar', @ResultText, @EventTime, SYSUTCDATETIME());
                                    """,
                                    new
                                    {
                                        Id = cmdId.Value,
                                        ResultText = resultText ?? "(commandResult)",
                                        EventTime = ev.EventTime.ToUniversalTime()
                                    },
                                    cancellationToken: ct));
                            }

                            // Marker row so we do not re-apply this commandResult.
                            await alertWriter.InsertAsync(
                                connection,
                                device.VehicleId,
                                ev.Latitude ?? 0,
                                ev.Longitude ?? 0,
                                0,
                                "command_result",
                                resultText ?? "commandResult",
                                ev.EventTime.ToUniversalTime(),
                                externalEventId: externalId,
                                cancellationToken: ct);
                        }

                        skipped++;
                        continue;
                    }

                    if (exists)
                    {
                        skipped++;
                        continue;
                    }

                    // ACC edge — update display ignition once per event (before alert insert).
                    if (string.Equals(ev.Type, "ignitionOn", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(ev.Type, "ignitionOff", StringComparison.OrdinalIgnoreCase))
                    {
                        var accOn = string.Equals(ev.Type, "ignitionOn", StringComparison.OrdinalIgnoreCase);
                        await connection.ExecuteAsync(new CommandDefinition(
                            """
                            UPDATE d SET
                                LastIgnition = @Ignition,
                                UpdatedAt = SYSUTCDATETIME()
                            FROM GpsDevices d
                            WHERE d.TraccarDeviceId = @TraccarDeviceId AND d.IsDeleted = 0;

                            UPDATE vcl SET
                                Ignition = @Ignition
                            FROM VehicleCurrentLocation vcl
                            INNER JOIN GpsDevices d ON d.VehicleId = vcl.VehicleId AND d.IsDeleted = 0
                            WHERE d.TraccarDeviceId = @TraccarDeviceId;
                            """,
                            new
                            {
                                TraccarDeviceId = device.TraccarDeviceId,
                                Ignition = accOn
                            },
                            cancellationToken: ct));
                    }

                    var eventType = TraccarAlertEventMapper.MapEventType(ev.Type, ev.Attributes?.Alarm);
                    if (eventType is null)
                    {
                        skipped++;
                        continue;
                    }

                    var message = TraccarAlertEventMapper.FormatMessage(eventType);
                    var timestamp = ev.EventTime.ToUniversalTime();

                    if (string.Equals(eventType, TraccarHarshEventMapper.HarshBraking, StringComparison.OrdinalIgnoreCase))
                    {
                        var recentHarsh = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                            @"SELECT CASE WHEN EXISTS(
                                SELECT 1 FROM GpsAlertEvents
                                WHERE VehicleId = @VehicleId AND EventType = @EventType AND IsDeleted = 0
                                  AND Timestamp > DATEADD(MINUTE, -@Minutes, @Timestamp)
                              ) THEN 1 ELSE 0 END",
                            new
                            {
                                device.VehicleId,
                                EventType = TraccarHarshEventMapper.HarshBraking,
                                Minutes = GpsHarshBrakingAlertDispatch.DedupMinutes,
                                Timestamp = timestamp
                            },
                            cancellationToken: ct));
                        if (recentHarsh)
                        {
                            skipped++;
                            continue;
                        }
                    }

                    var alertId = await alertWriter.InsertAsync(
                        connection,
                        device.VehicleId,
                        ev.Latitude ?? 0,
                        ev.Longitude ?? 0,
                        (decimal)(ev.SpeedKnots.HasValue ? ev.SpeedKnots.Value * 1.852 : 0),
                        eventType,
                        message,
                        timestamp,
                        externalEventId: externalId,
                        cancellationToken: ct);
                    if (alertId <= 0)
                    {
                        skipped++;
                        continue;
                    }

                    imported++;

                    if (string.Equals(eventType, TraccarHarshEventMapper.HarshBraking, StringComparison.OrdinalIgnoreCase))
                    {
                        var decisionEngine = scope.ServiceProvider.GetRequiredService<INotificationDecisionEngine>();
                        var speedKmh = (decimal)(ev.SpeedKnots.HasValue ? ev.SpeedKnots.Value * 1.852 : 0);
                        try
                        {
                            await GpsHarshBrakingAlertDispatch.DispatchIfAllowedAsync(
                                decisionEngine,
                                connection,
                                device.VehicleId,
                                alertId,
                                speedKmh,
                                tenantId: null,
                                cancellationToken: ct);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex,
                                "Harsh braking notification failed for alert {AlertId} vehicle {VehicleId}",
                                alertId, device.VehicleId);
                        }
                    }

                    if (DriverTelematicsEnforcementPolicy.ShouldCreateViolation(eventType))
                    {
                        var enforcement = scope.ServiceProvider.GetRequiredService<IDriverTelematicsEnforcementService>();
                        try
                        {
                            await enforcement.ProcessAlertAsync(connection, alertId, ct);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex,
                                "Telematics enforcement failed for alert {AlertId} ({EventType})",
                                alertId, eventType);
                        }
                    }
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

    /// <summary>
    /// Ensures at most one active GpsDevices row owns a TraccarDeviceId (IMEI sync key).
    /// Clears the id from other rows — does not delete devices or history.
    /// </summary>
    private static Task ClearTraccarDeviceIdFromOthersAsync(
        IDbConnection connection,
        int traccarDeviceId,
        int? keepGpsDeviceId,
        CancellationToken ct) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE GpsDevices
            SET TraccarDeviceId = NULL, UpdatedAt = GETUTCDATE()
            WHERE TraccarDeviceId = @TraccarDeviceId
              AND IsDeleted = 0
              AND (@KeepId IS NULL OR Id <> @KeepId)
            """,
            new { TraccarDeviceId = traccarDeviceId, KeepId = keepGpsDeviceId },
            cancellationToken: ct));

    /// <summary>
    /// Advances LastSeenAt / VCL.LastUpdate from Traccar server/device time so parked
    /// keepalives (frozen fixTime) stay Online. Does not move lat/lng.
    /// </summary>
    private static async Task RefreshConnectivityAsync(
        IDbConnection connection,
        int gpsDeviceId,
        int? vehicleId,
        DateTime seenAtUtc,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE GpsDevices
            SET LastSeenAt = CASE
                    WHEN LastSeenAt IS NULL OR LastSeenAt < @SeenAt THEN @SeenAt
                    ELSE LastSeenAt
                END,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @GpsDeviceId AND IsDeleted = 0
            """,
            new { GpsDeviceId = gpsDeviceId, SeenAt = seenAtUtc },
            cancellationToken: ct));

        if (!vehicleId.HasValue)
            return;

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE VehicleCurrentLocation
            SET LastUpdate = CASE
                    WHEN LastUpdate < @SeenAt THEN @SeenAt
                    ELSE LastUpdate
                END,
                -- Parked Jimi keepalives often lack sat/accuracy; clear sticky invalid from prior LBS heuristic.
                IsGpsValid = CASE
                    WHEN IsGpsValid = 0 AND ISNULL(Speed, 0) < 10 THEN CAST(1 AS BIT)
                    ELSE IsGpsValid
                END
            WHERE VehicleId = @VehicleId
            """,
            new { VehicleId = vehicleId.Value, SeenAt = seenAtUtc },
            cancellationToken: ct));
    }

    private static DateTime MaxUtc(params DateTime[] values)
    {
        var max = DateTime.MinValue;
        foreach (var v in values)
        {
            var utc = v.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(v, DateTimeKind.Utc)
                : v.ToUniversalTime();
            if (utc > max)
                max = utc;
        }

        return max;
    }

    private static TraccarSyncRunResult SingleJob(
        string job, int processed, int imported, int updated, int skipped, string? error = null) =>
        new(DateTime.UtcNow, [new TraccarSyncJobResult(job, processed, imported, updated, skipped, error)]);
}
