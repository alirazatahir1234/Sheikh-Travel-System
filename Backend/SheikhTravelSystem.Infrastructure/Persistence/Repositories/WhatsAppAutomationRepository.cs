using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class WhatsAppAutomationRepository(IDbConnectionFactory dbFactory) : IWhatsAppAutomationRepository
{
    public async Task<IReadOnlyList<WhatsAppAutomationRuleDto>> GetRulesAsync(int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<WhatsAppAutomationRuleDto>(new CommandDefinition("""
            SELECT Id, TenantId, EventType, IsEnabled, AccountId, TemplateName, OffsetMinutes, NotifyBooker, IsUrgent
            FROM WhatsAppAutomationRules
            WHERE TenantId = @TenantId
            ORDER BY EventType
            """, new { TenantId = tenantId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<WhatsAppAutomationRuleDto?> GetRuleAsync(int tenantId, string eventType, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<WhatsAppAutomationRuleDto>(new CommandDefinition("""
            SELECT Id, TenantId, EventType, IsEnabled, AccountId, TemplateName, OffsetMinutes, NotifyBooker, IsUrgent
            FROM WhatsAppAutomationRules
            WHERE TenantId = @TenantId AND EventType = @EventType
            """, new { TenantId = tenantId, EventType = eventType }, cancellationToken: ct));
    }

    public async Task<WhatsAppAutomationRuleDto> UpsertRuleAsync(WhatsAppAutomationRuleDto rule, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            MERGE WhatsAppAutomationRules AS t
            USING (SELECT @TenantId AS TenantId, @EventType AS EventType) AS s
            ON t.TenantId = s.TenantId AND t.EventType = s.EventType
            WHEN MATCHED THEN UPDATE SET
                IsEnabled = @IsEnabled,
                AccountId = @AccountId,
                TemplateName = @TemplateName,
                OffsetMinutes = @OffsetMinutes,
                NotifyBooker = @NotifyBooker,
                IsUrgent = @IsUrgent,
                UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT
                (TenantId, EventType, IsEnabled, AccountId, TemplateName, OffsetMinutes, NotifyBooker, IsUrgent)
            VALUES
                (@TenantId, @EventType, @IsEnabled, @AccountId, @TemplateName, @OffsetMinutes, @NotifyBooker, @IsUrgent);
            """, new
            {
                rule.TenantId,
                rule.EventType,
                rule.IsEnabled,
                rule.AccountId,
                rule.TemplateName,
                rule.OffsetMinutes,
                rule.NotifyBooker,
                rule.IsUrgent
            }, cancellationToken: ct));

        return (await GetRuleAsync(rule.TenantId, rule.EventType, ct))!;
    }

    public async Task<long> InsertEventAsync(
        AutomationEventInsert request, IDbTransaction? tx = null, CancellationToken ct = default)
    {
        var connection = tx?.Connection ?? dbFactory.CreateConnection();
        var ownsConnection = tx is null;
        if (ownsConnection && connection.State != ConnectionState.Open)
            connection.Open();

        try
        {
            return await connection.ExecuteScalarAsync<long>(new CommandDefinition("""
                INSERT INTO WhatsAppAutomationEvents
                    (TenantId, EventType, BookingId, TripId, DedupeKey, Status, DueAt, PayloadJson)
                OUTPUT INSERTED.Id
                VALUES
                    (@TenantId, @EventType, @BookingId, @TripId, @DedupeKey, 0, @DueAt, @PayloadJson);
                """, new
                {
                    request.TenantId,
                    request.EventType,
                    request.BookingId,
                    request.TripId,
                    request.DedupeKey,
                    request.DueAt,
                    request.PayloadJson
                }, transaction: tx, cancellationToken: ct));
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            var existing = await connection.ExecuteScalarAsync<long?>(new CommandDefinition("""
                SELECT Id FROM WhatsAppAutomationEvents
                WHERE TenantId = @TenantId AND DedupeKey = @DedupeKey
                """, new { request.TenantId, request.DedupeKey }, transaction: tx, cancellationToken: ct));
            return existing ?? 0;
        }
        finally
        {
            if (ownsConnection)
                connection.Dispose();
        }
    }

    public async Task<WhatsAppAutomationEventRow?> GetEventAsync(long id, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<WhatsAppAutomationEventRow>(new CommandDefinition("""
            SELECT Id, TenantId, EventType, BookingId, TripId, DedupeKey, Status, SkipReason, DueAt,
                   Attempts, Error, MessageId, RecipientPhone, PayloadJson
            FROM WhatsAppAutomationEvents WHERE Id = @Id
            """, new { Id = id }, cancellationToken: ct));
    }

    public async Task<bool> ClaimEventAsync(long id, TimeSpan lockDuration, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppAutomationEvents
            SET Status = 1,
                Attempts = Attempts + 1,
                LockedUntil = DATEADD(SECOND, @LockSeconds, SYSUTCDATETIME())
            WHERE Id = @Id
              AND Status IN (0, 1)
              AND (LockedUntil IS NULL OR LockedUntil < SYSUTCDATETIME() OR Status = 0)
            """, new { Id = id, LockSeconds = (int)lockDuration.TotalSeconds }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task MarkEventSentAsync(long id, int? messageId, string? recipientPhone, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppAutomationEvents
            SET Status = 2, MessageId = @MessageId, RecipientPhone = @RecipientPhone,
                ProcessedAt = SYSUTCDATETIME(), Error = NULL, SkipReason = NULL, LockedUntil = NULL
            WHERE Id = @Id
            """, new { Id = id, MessageId = messageId, RecipientPhone = recipientPhone }, cancellationToken: ct));
    }

    public async Task MarkEventSkippedAsync(long id, string skipReason, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppAutomationEvents
            SET Status = 3, SkipReason = @SkipReason, ProcessedAt = SYSUTCDATETIME(), LockedUntil = NULL
            WHERE Id = @Id
            """, new { Id = id, SkipReason = Truncate(skipReason, 64) }, cancellationToken: ct));
    }

    public async Task MarkEventFailedAsync(long id, string error, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppAutomationEvents
            SET Status = CASE WHEN Attempts >= @MaxAttempts THEN 4 ELSE 0 END,
                Error = @Error,
                LockedUntil = NULL,
                ProcessedAt = CASE WHEN Attempts >= @MaxAttempts THEN SYSUTCDATETIME() ELSE ProcessedAt END
            WHERE Id = @Id
            """, new
            {
                Id = id,
                Error = Truncate(error, 2000),
                MaxAttempts = AutomationPolicy.MaxProcessAttempts
            }, cancellationToken: ct));
    }

    public async Task RescheduleEventAsync(long id, DateTime newDueAtUtc, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppAutomationEvents
            SET Status = 0,
                DueAt = @DueAt,
                LockedUntil = NULL,
                Error = N'QuietHoursReschedule'
            WHERE Id = @Id
            """, new { Id = id, DueAt = newDueAtUtc }, cancellationToken: ct));
    }

    public async Task CancelPendingForBookingAsync(
        int tenantId, int bookingId, IReadOnlyList<string>? eventTypes = null, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (eventTypes is { Count: > 0 })
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE WhatsAppAutomationEvents
                SET Status = 5, ProcessedAt = SYSUTCDATETIME(), LockedUntil = NULL
                WHERE TenantId = @TenantId AND BookingId = @BookingId AND Status IN (0, 1)
                  AND EventType IN @EventTypes
                """, new { TenantId = tenantId, BookingId = bookingId, EventTypes = eventTypes },
                cancellationToken: ct));
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppAutomationEvents
            SET Status = 5, ProcessedAt = SYSUTCDATETIME(), LockedUntil = NULL
            WHERE TenantId = @TenantId AND BookingId = @BookingId AND Status IN (0, 1)
            """, new { TenantId = tenantId, BookingId = bookingId }, cancellationToken: ct));
    }

    public async Task CancelPendingForTripAsync(int tenantId, int tripId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppAutomationEvents
            SET Status = 5, ProcessedAt = SYSUTCDATETIME(), LockedUntil = NULL
            WHERE TenantId = @TenantId AND TripId = @TripId AND Status IN (0, 1)
            """, new { TenantId = tenantId, TripId = tripId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<long>> ListDueEventIdsAsync(DateTime utcNow, int take, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<long>(new CommandDefinition("""
            SELECT TOP (@Take) Id
            FROM WhatsAppAutomationEvents
            WHERE Status IN (0, 1)
              AND DueAt <= @Now
              AND (LockedUntil IS NULL OR LockedUntil < @Now)
            ORDER BY DueAt, Id
            """, new { Take = take, Now = utcNow }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<WhatsAppAutomationTimelineItemDto>> GetBookingTimelineAsync(
        int tenantId, int bookingId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<WhatsAppAutomationTimelineItemDto>(new CommandDefinition("""
            SELECT e.Id, e.EventType, e.Status, e.SkipReason, e.DueAt, e.ProcessedAt,
                   e.RecipientPhone, e.MessageId, m.Status AS MessageStatus, m.CreatedAt AS MessageCreatedAt
            FROM WhatsAppAutomationEvents e
            LEFT JOIN WhatsAppMessages m ON m.Id = e.MessageId
            WHERE e.TenantId = @TenantId AND e.BookingId = @BookingId
            ORDER BY e.CreatedAt DESC, e.Id DESC
            """, new { TenantId = tenantId, BookingId = bookingId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<WhatsAppConsentDto?> GetConsentAsync(int tenantId, string waId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<WhatsAppConsentDto>(new CommandDefinition("""
            SELECT Id, WaId, OptInAt, OptInSource, OptOutAt, Note
            FROM WhatsAppContactConsents
            WHERE TenantId = @TenantId AND WaId = @WaId
            """, new { TenantId = tenantId, WaId = NormalizeWaId(waId) }, cancellationToken: ct));
    }

    public async Task UpsertConsentOptInAsync(
        int tenantId, string waId, string source, string? note, int? userId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            MERGE WhatsAppContactConsents AS t
            USING (SELECT @TenantId AS TenantId, @WaId AS WaId) AS s
            ON t.TenantId = s.TenantId AND t.WaId = s.WaId
            WHEN MATCHED THEN UPDATE SET
                OptInAt = SYSUTCDATETIME(),
                OptInSource = @Source,
                OptOutAt = NULL,
                Note = @Note,
                CreatedByUserId = COALESCE(@UserId, CreatedByUserId),
                UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT
                (TenantId, WaId, OptInAt, OptInSource, Note, CreatedByUserId)
            VALUES
                (@TenantId, @WaId, SYSUTCDATETIME(), @Source, @Note, @UserId);
            """, new
            {
                TenantId = tenantId,
                WaId = NormalizeWaId(waId),
                Source = Truncate(source, 40),
                Note = Truncate(note, 500),
                UserId = userId
            }, cancellationToken: ct));
    }

    public async Task OptOutAsync(int tenantId, string waId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            MERGE WhatsAppContactConsents AS t
            USING (SELECT @TenantId AS TenantId, @WaId AS WaId) AS s
            ON t.TenantId = s.TenantId AND t.WaId = s.WaId
            WHEN MATCHED THEN UPDATE SET
                OptOutAt = SYSUTCDATETIME(),
                UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT
                (TenantId, WaId, OptOutAt)
            VALUES
                (@TenantId, @WaId, SYSUTCDATETIME());
            """, new { TenantId = tenantId, WaId = NormalizeWaId(waId) }, cancellationToken: ct));
    }

    public async Task<bool> HasActiveOptInAsync(int tenantId, string waId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var n = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE
                WHEN EXISTS (
                    SELECT 1 FROM WhatsAppContactConsents
                    WHERE TenantId = @TenantId AND WaId = @WaId
                      AND OptInAt IS NOT NULL AND OptOutAt IS NULL)
                THEN 1 ELSE 0 END
            """, new { TenantId = tenantId, WaId = NormalizeWaId(waId) }, cancellationToken: ct));
        return n == 1;
    }

    public async Task<TripTrackingLinkRow?> GetActiveTrackingLinkAsync(int tripId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TripTrackingLinkRow>(new CommandDefinition("""
            SELECT Id, TenantId, TripId, BookingId, TokenHash, TokenProtected, ExpiresAt, RevokedAt, ViewCount
            FROM TripTrackingLinks
            WHERE TripId = @TripId AND RevokedAt IS NULL
            """, new { TripId = tripId }, cancellationToken: ct));
    }

    public async Task<TripTrackingLinkRow?> GetTrackingLinkByHashAsync(byte[] tokenHash, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TripTrackingLinkRow>(new CommandDefinition("""
            SELECT Id, TenantId, TripId, BookingId, TokenHash, TokenProtected, ExpiresAt, RevokedAt, ViewCount
            FROM TripTrackingLinks
            WHERE TokenHash = @TokenHash
            """, new { TokenHash = tokenHash }, cancellationToken: ct));
    }

    public async Task<long> InsertTrackingLinkAsync(
        int tenantId, int tripId, int? bookingId, byte[] tokenHash, string tokenProtected,
        DateTime expiresAt, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition("""
            INSERT INTO TripTrackingLinks
                (TenantId, TripId, BookingId, TokenHash, TokenProtected, ExpiresAt)
            OUTPUT INSERTED.Id
            VALUES (@TenantId, @TripId, @BookingId, @TokenHash, @TokenProtected, @ExpiresAt)
            """, new
            {
                TenantId = tenantId,
                TripId = tripId,
                BookingId = bookingId,
                TokenHash = tokenHash,
                TokenProtected = tokenProtected,
                ExpiresAt = expiresAt
            }, cancellationToken: ct));
    }

    public async Task RevokeTrackingLinksForTripAsync(int tripId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TripTrackingLinks
            SET RevokedAt = SYSUTCDATETIME()
            WHERE TripId = @TripId AND RevokedAt IS NULL
            """, new { TripId = tripId }, cancellationToken: ct));
    }

    public async Task ExpireTrackingLinksForTripAsync(int tripId, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TripTrackingLinks
            SET ExpiresAt = CASE
                WHEN ExpiresAt > @ExpiresAt THEN @ExpiresAt
                ELSE ExpiresAt
            END
            WHERE TripId = @TripId AND RevokedAt IS NULL
            """, new { TripId = tripId, ExpiresAt = expiresAtUtc }, cancellationToken: ct));
    }

    public async Task IncrementTrackingViewAsync(long linkId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TripTrackingLinks
            SET ViewCount = ViewCount + 1, LastViewedAt = SYSUTCDATETIME()
            WHERE Id = @Id
            """, new { Id = linkId }, cancellationToken: ct));
    }

    public async Task<bool> TryInsertTripRatingAsync(
        int tenantId, int tripId, int? driverId, int score, int? messageId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TripRatings (TenantId, TripId, DriverId, Score, Source, MessageId)
                VALUES (@TenantId, @TripId, @DriverId, @Score, N'WhatsApp', @MessageId)
                """, new { TenantId = tenantId, TripId = tripId, DriverId = driverId, Score = score, MessageId = messageId },
                cancellationToken: ct));
            return true;
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    public async Task<AutomationBookingSnapshot?> GetBookingSnapshotAsync(
        int tenantId, int bookingId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleOrDefaultAsync<AutomationBookingSnapshot>(new CommandDefinition("""
                SELECT b.Id, b.BookingNumber, b.Status, b.PickupTime,
                       COALESCE(r.Source, N'') AS PickupAddress,
                       b.CustomerId,
                       c.FullName AS CustomerName,
                       c.Phone AS CustomerPhone,
                       c.PreferredLanguage,
                       b.DriverId,
                       CASE WHEN d.FullName IS NULL THEN NULL
                            ELSE LEFT(d.FullName, CASE WHEN CHARINDEX(' ', d.FullName) > 0
                                THEN CHARINDEX(' ', d.FullName) - 1 ELSE LEN(d.FullName) END)
                       END AS DriverFirstName,
                       b.VehicleId,
                       COALESCE(v.Make + N' ' + v.Model, v.Name, N'') AS VehicleDescription,
                       v.PlateNumber,
                       NULL AS BookerPhone
                FROM Bookings b
                LEFT JOIN Customers c ON c.Id = b.CustomerId
                LEFT JOIN Routes r ON r.Id = b.RouteId
                LEFT JOIN Drivers d ON d.Id = b.DriverId
                LEFT JOIN Vehicles v ON v.Id = b.VehicleId
                WHERE b.Id = @BookingId AND b.TenantId = @TenantId AND b.IsDeleted = 0
                """, new { TenantId = tenantId, BookingId = bookingId }, cancellationToken: ct));
        }
        catch
        {
            // Soft-fail if PreferredLanguage / columns differ across environments.
            return await connection.QuerySingleOrDefaultAsync<AutomationBookingSnapshot>(new CommandDefinition("""
                SELECT b.Id, b.BookingNumber, b.Status, b.PickupTime,
                       N'' AS PickupAddress,
                       b.CustomerId,
                       c.FullName AS CustomerName,
                       c.Phone AS CustomerPhone,
                       CAST(NULL AS NVARCHAR(8)) AS PreferredLanguage,
                       b.DriverId,
                       NULL AS DriverFirstName,
                       b.VehicleId,
                       N'' AS VehicleDescription,
                       NULL AS PlateNumber,
                       NULL AS BookerPhone
                FROM Bookings b
                LEFT JOIN Customers c ON c.Id = b.CustomerId
                WHERE b.Id = @BookingId AND b.TenantId = @TenantId AND b.IsDeleted = 0
                """, new { TenantId = tenantId, BookingId = bookingId }, cancellationToken: ct));
        }
    }

    public async Task<AutomationTripSnapshot?> GetTripSnapshotAsync(
        int tenantId, int tripId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            return await connection.QuerySingleOrDefaultAsync<AutomationTripSnapshot>(new CommandDefinition("""
                SELECT t.Id, t.TripNumber, t.Status, t.BookingId,
                       t.DriverId,
                       CASE WHEN d.FullName IS NULL THEN NULL
                            ELSE LEFT(d.FullName, CASE WHEN CHARINDEX(' ', d.FullName) > 0
                                THEN CHARINDEX(' ', d.FullName) - 1 ELSE LEN(d.FullName) END)
                       END AS DriverFirstName,
                       t.VehicleId,
                       COALESCE(v.Make + N' ' + v.Model, v.Name, N'') AS VehicleDescription,
                       v.PlateNumber,
                       t.PickupLatitude AS PickupLat,
                       t.PickupLongitude AS PickupLng,
                       t.PickupAddress,
                       t.DropoffLatitude AS DropoffLat,
                       t.DropoffLongitude AS DropoffLng,
                       t.DropoffAddress,
                       t.ScheduledStart AS PickupAt,
                       t.DistanceKm,
                       t.TotalAmount,
                       N'PKR' AS Currency,
                       ISNULL(t.TripType, 2) AS TripType
                FROM Trips t
                LEFT JOIN Drivers d ON d.Id = t.DriverId
                LEFT JOIN Vehicles v ON v.Id = t.VehicleId
                WHERE t.Id = @TripId AND t.TenantId = @TenantId AND t.IsDeleted = 0
                """, new { TenantId = tenantId, TripId = tripId }, cancellationToken: ct));
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<int>> ListActiveEnRouteTripVehicleIdsAsync(CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            var rows = await connection.QueryAsync<int>(new CommandDefinition("""
                SELECT DISTINCT VehicleId
                FROM Trips
                WHERE IsDeleted = 0
                  AND VehicleId IS NOT NULL
                  AND Status = @Started
                """, new { Started = (int)TripStatus.Started }, cancellationToken: ct));
            return rows.ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task LinkMessageToAutomationAsync(int messageId, long automationEventId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppMessages SET AutomationEventId = @EventId WHERE Id = @MessageId
            """, new { MessageId = messageId, EventId = automationEventId }, cancellationToken: ct));
    }

    private static string NormalizeWaId(string waId)
    {
        var digits = new string((waId ?? "").Where(char.IsDigit).ToArray());
        return digits;
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains("2627", StringComparison.Ordinal)
                || e.Message.Contains("2601", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}
