using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class WhatsAppAiAssistAuditRepository(
    IDbConnectionFactory dbFactory,
    ITrackingTokenProtector tokenProtector) : IWhatsAppAiAssistAuditRepository
{
    public async Task InsertAsync(
        int tenantId,
        int conversationId,
        int? userId,
        string operation,
        string? provider,
        string? model,
        int durationMs,
        bool success,
        string? confidence,
        string? errorCode,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO WhatsAppAiAssistLogs
                    (TenantId, ConversationId, UserId, Operation, Provider, Model, DurationMs, Success, Confidence, ErrorCode)
                VALUES
                    (@TenantId, @ConversationId, @UserId, @Operation, @Provider, @Model, @DurationMs, @Success, @Confidence, @ErrorCode)
                """, new
            {
                TenantId = tenantId,
                ConversationId = conversationId,
                UserId = userId,
                Operation = Truncate(operation, 40),
                Provider = Truncate(provider, 80),
                Model = Truncate(model, 120),
                DurationMs = durationMs,
                Success = success,
                Confidence = Truncate(confidence, 20),
                ErrorCode = Truncate(errorCode, 80)
            }, cancellationToken: cancellationToken));
        }
        catch
        {
            // Audit must not break assist — table may not exist until migration runs.
        }
    }

    public async Task<WhatsAppAiActiveTripFactsDto?> GetLatestActiveTripFactsAsync(
        int tenantId,
        string phoneE164,
        int? customerId,
        CancellationToken cancellationToken = default)
    {
        var digits = DigitsOnly(phoneE164);
        using var connection = dbFactory.CreateConnection();

        var trip = await connection.QuerySingleOrDefaultAsync<(
            int TripId, string? TripNumber, int? BookingId, string? BookingNumber, int Status,
            string? DriverFirstName, string? VehicleDescription, string? PlateNumber, string? TokenProtected)>(
            new CommandDefinition("""
            SELECT TOP 1
                   t.Id AS TripId,
                   t.TripNumber,
                   t.BookingId,
                   b.BookingNumber,
                   t.Status,
                   LEFT(d.FullName, CHARINDEX(' ', d.FullName + ' ') - 1) AS DriverFirstName,
                   COALESCE(v.Name, v.Make + N' ' + v.Model) AS VehicleDescription,
                   v.RegistrationNumber AS PlateNumber,
                   link.TokenProtected
            FROM Trips t
            INNER JOIN Bookings b ON b.Id = t.BookingId AND b.IsDeleted = 0
            INNER JOIN Customers c ON c.Id = b.CustomerId AND c.IsDeleted = 0
            LEFT JOIN Drivers d ON d.Id = t.DriverId
            LEFT JOIN Vehicles v ON v.Id = t.VehicleId
            OUTER APPLY (
                SELECT TOP 1 TokenProtected
                FROM TripTrackingLinks l
                WHERE l.TripId = t.Id AND l.RevokedAt IS NULL AND l.ExpiresAt > SYSUTCDATETIME()
                ORDER BY l.Id DESC
            ) link
            WHERE t.TenantId = @TenantId AND t.IsDeleted = 0
              AND t.Status NOT IN (@Completed, @Cancelled, @Failed)
              AND (
                    (@CustomerId IS NOT NULL AND b.CustomerId = @CustomerId)
                 OR (
                        @Digits <> N''
                    AND (
                            REPLACE(REPLACE(c.Phone, N'+', N''), N' ', N'') LIKE N'%' + @Digits
                         OR REPLACE(REPLACE(c.Phone, N'+', N''), N' ', N'') = @Digits
                        )
                    )
              )
            ORDER BY t.Id DESC
            """, new
            {
                TenantId = tenantId,
                CustomerId = customerId,
                Digits = digits,
                Completed = (int)TripStatus.Completed,
                Cancelled = (int)TripStatus.Cancelled,
                Failed = (int)TripStatus.Failed
            }, cancellationToken: cancellationToken));

        if (trip.TripId == 0)
            return null;

        string? trackingUrl = null;
        if (!string.IsNullOrWhiteSpace(trip.TokenProtected))
        {
            try
            {
                var token = tokenProtector.Unprotect(trip.TokenProtected);
                trackingUrl = $"https://track.sheikhgo.com/t/{token}";
            }
            catch
            {
                trackingUrl = null;
            }
        }

        return new WhatsAppAiActiveTripFactsDto(
            trip.TripId,
            trip.TripNumber,
            trip.BookingId,
            trip.BookingNumber,
            MapTripStatus(trip.Status),
            trip.DriverFirstName,
            trip.VehicleDescription,
            trip.PlateNumber,
            trackingUrl);
    }

    private static string MapTripStatus(int status) => ((TripStatus)status) switch
    {
        TripStatus.DriverAssigned or TripStatus.VehicleAssigned or TripStatus.Scheduled => "Assigned",
        TripStatus.Started => "Driver En Route",
        TripStatus.AtPickup => "Driver Arrived",
        TripStatus.Enroute or TripStatus.Delayed => "In Progress",
        TripStatus.Completed => "Completed",
        TripStatus.Cancelled or TripStatus.Failed => "Cancelled",
        _ => "In Progress"
    };

    private static string DigitsOnly(string? phone)
        => new string((phone ?? "").Where(char.IsDigit).ToArray());

    private static string? Truncate(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Length <= max ? t : t[..max];
    }
}
