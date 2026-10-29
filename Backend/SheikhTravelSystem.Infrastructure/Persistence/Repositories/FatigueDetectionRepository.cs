using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.FatigueDetection;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class FatigueDetectionRepository(IDbConnectionFactory dbFactory) : IFatigueDetectionRepository
{
    private static readonly string[] OpenStatuses = ["AwaitingAck", "Escalating"];

    public async Task<FatigueTenantSettingsDto> GetOrCreateSettingsAsync(int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            IF NOT EXISTS (SELECT 1 FROM FatigueTenantSettings WHERE TenantId = @TenantId)
                INSERT INTO FatigueTenantSettings (TenantId) VALUES (@TenantId);
            """,
            new { TenantId = tenantId },
            cancellationToken: ct));

        return await connection.QuerySingleAsync<FatigueTenantSettingsDto>(new CommandDefinition(
            """
            SELECT AckWindowSeconds, StageIntervalSeconds, AutoStopEnabled
            FROM FatigueTenantSettings WHERE TenantId = @TenantId
            """,
            new { TenantId = tenantId },
            cancellationToken: ct));
    }

    public async Task UpsertSettingsAsync(int tenantId, UpsertFatigueTenantSettingsRequest body, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            MERGE FatigueTenantSettings AS t
            USING (SELECT @TenantId AS TenantId) AS s ON t.TenantId = s.TenantId
            WHEN MATCHED THEN UPDATE SET
                AckWindowSeconds = @AckWindowSeconds,
                StageIntervalSeconds = @StageIntervalSeconds,
                AutoStopEnabled = @AutoStopEnabled,
                UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN INSERT (TenantId, AckWindowSeconds, StageIntervalSeconds, AutoStopEnabled)
                VALUES (@TenantId, @AckWindowSeconds, @StageIntervalSeconds, @AutoStopEnabled);
            """,
            new
            {
                TenantId = tenantId,
                body.AckWindowSeconds,
                body.StageIntervalSeconds,
                body.AutoStopEnabled
            },
            cancellationToken: ct));
    }

    public async Task<int?> GetOpenEventIdForDriverAsync(int tenantId, int driverId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 Id FROM DriverFatigueEvents
            WHERE TenantId = @TenantId AND DriverId = @DriverId AND IsDeleted = 0
              AND Status IN (N'AwaitingAck', N'Escalating')
            ORDER BY CreatedAt DESC
            """,
            new { TenantId = tenantId, DriverId = driverId },
            cancellationToken: ct));
    }

    public async Task<int> InsertEventAsync(FatigueEventInsertRow row, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO DriverFatigueEvents
                (TenantId, DriverId, VehicleId, BookingId, Source, SignalType, Confidence, EarScore,
                 ExpressionHint, Latitude, Longitude, Status, EscalationStage, AckDeadlineUtc, PayloadJson,
                 DriverRiskScore, CompositeRiskScore, SupportingSignalsJson)
            OUTPUT INSERTED.Id
            VALUES
                (@TenantId, @DriverId, @VehicleId, @BookingId, @Source, @SignalType, @Confidence, @EarScore,
                 @ExpressionHint, @Latitude, @Longitude, N'AwaitingAck', 0, @AckDeadlineUtc, @PayloadJson,
                 @DriverRiskScore, @CompositeRiskScore, @SupportingSignalsJson)
            """,
            new
            {
                row.TenantId,
                row.DriverId,
                row.VehicleId,
                row.BookingId,
                row.Source,
                row.Metrics.SignalType,
                row.Metrics.Confidence,
                row.Metrics.EarScore,
                row.Metrics.ExpressionHint,
                row.Metrics.Latitude,
                row.Metrics.Longitude,
                row.AckDeadlineUtc,
                row.Metrics.PayloadJson,
                row.DriverRiskScore,
                row.CompositeRiskScore,
                row.SupportingSignalsJson
            },
            cancellationToken: ct));
    }

    public async Task TouchOpenEventAsync(int tenantId, int eventId, FatigueSignalMetrics metrics, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE DriverFatigueEvents SET
                SignalType = @SignalType,
                Confidence = @Confidence,
                EarScore = @EarScore,
                ExpressionHint = @ExpressionHint,
                Latitude = COALESCE(@Latitude, Latitude),
                Longitude = COALESCE(@Longitude, Longitude),
                PayloadJson = COALESCE(@PayloadJson, PayloadJson)
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
              AND Status IN (N'AwaitingAck', N'Escalating')
            """,
            new
            {
                Id = eventId,
                TenantId = tenantId,
                metrics.SignalType,
                metrics.Confidence,
                metrics.EarScore,
                metrics.ExpressionHint,
                metrics.Latitude,
                metrics.Longitude,
                metrics.PayloadJson
            },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<FatigueEventListItemDto>> ListEventsAsync(
        int tenantId, string? status, int limit, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<FatigueEventListItemDto>(new CommandDefinition(
            """
            SELECT TOP (@Limit)
                e.Id,
                e.TenantId,
                t.Name AS TenantName,
                e.DriverId,
                d.FullName AS DriverName,
                d.Phone AS DriverPhone,
                e.VehicleId,
                v.Name AS VehicleName,
                e.Source,
                e.SignalType,
                e.Confidence,
                e.EarScore,
                e.ExpressionHint,
                e.Latitude,
                e.Longitude,
                e.Status,
                e.EscalationStage,
                e.AckDeadlineUtc,
                e.AcknowledgedAtUtc,
                e.CreatedAt AS CreatedAtUtc,
                e.LastEscalationNote,
                e.DriverRiskScore,
                e.CompositeRiskScore,
                e.SupportingSignalsJson
            FROM DriverFatigueEvents e
            INNER JOIN Drivers d ON d.Id = e.DriverId
            INNER JOIN Tenants t ON t.Id = e.TenantId
            LEFT JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE e.TenantId = @TenantId AND e.IsDeleted = 0
              AND (@Status IS NULL OR e.Status = @Status)
            ORDER BY e.CreatedAt DESC
            """,
            new { TenantId = tenantId, Status = status, Limit = Math.Clamp(limit, 1, 200) },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<FatigueEventListItemDto>> ListEventsPlatformAsync(
        int? tenantId, string? status, int limit, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<FatigueEventListItemDto>(new CommandDefinition(
            """
            SELECT TOP (@Limit)
                e.Id,
                e.TenantId,
                t.Name AS TenantName,
                e.DriverId,
                d.FullName AS DriverName,
                d.Phone AS DriverPhone,
                e.VehicleId,
                v.Name AS VehicleName,
                e.Source,
                e.SignalType,
                e.Confidence,
                e.EarScore,
                e.ExpressionHint,
                e.Latitude,
                e.Longitude,
                e.Status,
                e.EscalationStage,
                e.AckDeadlineUtc,
                e.AcknowledgedAtUtc,
                e.CreatedAt AS CreatedAtUtc,
                e.LastEscalationNote,
                e.DriverRiskScore,
                e.CompositeRiskScore,
                e.SupportingSignalsJson
            FROM DriverFatigueEvents e
            INNER JOIN Drivers d ON d.Id = e.DriverId
            INNER JOIN Tenants t ON t.Id = e.TenantId
            LEFT JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE e.IsDeleted = 0
              AND (@TenantId IS NULL OR e.TenantId = @TenantId)
              AND (@Status IS NULL OR e.Status = @Status)
            ORDER BY e.CompositeRiskScore DESC, e.CreatedAt DESC
            """,
            new { TenantId = tenantId, Status = status, Limit = Math.Clamp(limit, 1, 500) },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<decimal?> GetDriverRiskScoreAsync(int tenantId, int driverId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            """
            SELECT TOP 1 Probability / 100.0
            FROM AiPredictions
            WHERE TenantId = @TenantId AND EntityType = N'Driver' AND EntityId = @DriverId
              AND PredictionType = N'driver_risk'
            ORDER BY CreatedAt DESC
            """,
            new { TenantId = tenantId, DriverId = driverId },
            cancellationToken: ct));
    }

    public async Task<int?> GetDriverUserIdAsync(int tenantId, int driverId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT UserId FROM Drivers
            WHERE Id = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
            """,
            new { DriverId = driverId, TenantId = tenantId },
            cancellationToken: ct));
    }

    public async Task ResolveEventAsync(int tenantId, int eventId, string resolvedBy, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE DriverFatigueEvents SET
                Status = N'Resolved',
                ResolvedAt = GETUTCDATE(),
                ResolvedBy = @ResolvedBy,
                NextEscalationUtc = NULL
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """,
            new { Id = eventId, TenantId = tenantId, ResolvedBy = resolvedBy },
            cancellationToken: ct));
    }

    public async Task AcknowledgeEventAsync(int tenantId, int driverId, int eventId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE DriverFatigueEvents SET
                Status = N'Acknowledged',
                AcknowledgedAtUtc = GETUTCDATE(),
                NextEscalationUtc = NULL,
                LastEscalationNote = N'Driver acknowledged fatigue prompt.'
            WHERE Id = @Id AND TenantId = @TenantId AND DriverId = @DriverId AND IsDeleted = 0
              AND Status IN (N'AwaitingAck', N'Escalating')
            """,
            new { Id = eventId, TenantId = tenantId, DriverId = driverId },
            cancellationToken: ct));
    }

    public async Task<DriverFatigueStatusDto?> GetDriverFatigueStatusAsync(
        int tenantId, int driverId, int eventId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DriverFatigueStatusDto>(new CommandDefinition(
            """
            SELECT
                Id,
                Status,
                EscalationStage,
                AckDeadlineUtc,
                AcknowledgedAtUtc,
                LastEscalationNote,
                CASE Status
                    WHEN N'AwaitingAck' THEN N'Confirm you are alert within the ack window.'
                    WHEN N'Escalating' THEN N'Escalation in progress — acknowledge immediately.'
                    WHEN N'Acknowledged' THEN N'Thank you — drive safely.'
                    WHEN N'AutoStopped' THEN N'Vehicle safety stop was triggered.'
                    ELSE Status
                END AS Message
            FROM DriverFatigueEvents
            WHERE Id = @Id AND TenantId = @TenantId AND DriverId = @DriverId AND IsDeleted = 0
            """,
            new { Id = eventId, TenantId = tenantId, DriverId = driverId },
            cancellationToken: ct));
    }

    public async Task<DriverFatigueActivePromptDto?> GetActivePromptForDriverAsync(
        int tenantId, int driverId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(int Id, string Status, DateTime AckDeadlineUtc)>(
            new CommandDefinition(
                """
                SELECT TOP 1 Id, Status, AckDeadlineUtc
                FROM DriverFatigueEvents
                WHERE TenantId = @TenantId AND DriverId = @DriverId AND IsDeleted = 0
                  AND Status IN (N'AwaitingAck', N'Escalating')
                ORDER BY CreatedAt DESC
                """,
                new { TenantId = tenantId, DriverId = driverId },
                cancellationToken: ct));

        if (row.Id == 0)
            return null;

        var remaining = (int)Math.Max(0, (row.AckDeadlineUtc - DateTime.UtcNow).TotalSeconds);
        var title = row.Status == "Escalating"
            ? "Fatigue — respond now"
            : "Fatigue check";
        var body = row.Status == "Escalating"
            ? "No response received. Tap I am alert now or pull over safely."
            : "Possible drowsiness detected. Tap I am alert to confirm within the time window.";

        return new DriverFatigueActivePromptDto(row.Id, row.Status, row.AckDeadlineUtc, remaining, title, body);
    }

    public async Task<IReadOnlyList<FatigueEscalationCandidateRow>> ListEscalationCandidatesAsync(CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var now = DateTime.UtcNow;
        var rows = await connection.QueryAsync<FatigueEscalationCandidateRow>(new CommandDefinition(
            """
            SELECT
                e.Id,
                e.TenantId,
                e.DriverId,
                e.VehicleId,
                e.Status,
                e.EscalationStage,
                e.NextEscalationUtc,
                e.AckDeadlineUtc,
                CAST(COALESCE(s.AutoStopEnabled, 1) AS bit) AS AutoStopEnabled
            FROM DriverFatigueEvents e
            LEFT JOIN FatigueTenantSettings s ON s.TenantId = e.TenantId
            WHERE e.IsDeleted = 0
              AND (
                    (e.Status = N'AwaitingAck' AND e.AckDeadlineUtc <= @Now)
                 OR (e.Status = N'Escalating' AND e.NextEscalationUtc IS NOT NULL AND e.NextEscalationUtc <= @Now)
              )
            ORDER BY e.CreatedAt
            """,
            new { Now = now },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task AdvanceEscalationAsync(
        int eventId,
        int newStage,
        string status,
        DateTime? nextEscalationUtc,
        string? note,
        CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE DriverFatigueEvents SET
                EscalationStage = @Stage,
                Status = @Status,
                NextEscalationUtc = @NextEscalationUtc,
                LastEscalationNote = @Note
            WHERE Id = @Id AND IsDeleted = 0
            """,
            new
            {
                Id = eventId,
                Stage = newStage,
                Status = status,
                NextEscalationUtc = nextEscalationUtc,
                Note = note
            },
            cancellationToken: ct));
    }

    public async Task<int?> GetPrimaryGpsDeviceIdForVehicleAsync(int tenantId, int vehicleId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 d.Id
            FROM GpsDevices d
            INNER JOIN Vehicles v ON v.Id = d.VehicleId AND v.TenantId = @TenantId
            WHERE d.VehicleId = @VehicleId AND d.IsDeleted = 0
            ORDER BY d.Id DESC
            """,
            new { TenantId = tenantId, VehicleId = vehicleId },
            cancellationToken: ct));
    }

    public async Task<(string FullName, string? Phone)?> GetDriverContactAsync(int tenantId, int driverId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(string FullName, string? Phone)>(new CommandDefinition(
            """
            SELECT FullName, Phone FROM Drivers
            WHERE Id = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
            """,
            new { DriverId = driverId, TenantId = tenantId },
            cancellationToken: ct));
        return string.IsNullOrWhiteSpace(row.FullName) ? null : row;
    }

    public async Task<(int? VehicleId, int? BookingId)> GetActiveTripContextAsync(int tenantId, int driverId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(int? VehicleId, int? BookingId)>(new CommandDefinition(
            """
            SELECT TOP 1 VehicleId, Id AS BookingId
            FROM Bookings
            WHERE DriverId = @DriverId AND TenantId = @TenantId AND Status = @Started AND IsDeleted = 0
            ORDER BY PickupTime DESC
            """,
            new { TenantId = tenantId, DriverId = driverId, Started = (int)BookingStatus.Started },
            cancellationToken: ct));
        return row;
    }
}
