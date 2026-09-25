using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.Bot;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class WhatsAppSelfServiceRepository(
    IDbConnectionFactory dbFactory,
    ITrackingTokenProtector tokenProtector) : IWhatsAppSelfServiceRepository
{
    public async Task<string?> GetBotSessionJsonAsync(int tenantId, int conversationId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            SELECT BotSessionJson FROM WhatsAppConversations
            WHERE TenantId = @TenantId AND Id = @Id
            """, new { TenantId = tenantId, Id = conversationId }, cancellationToken: ct));
    }

    public async Task SetBotSessionJsonAsync(int tenantId, int conversationId, string? json, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppConversations
            SET BotSessionJson = @Json, UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND Id = @Id
            """, new { TenantId = tenantId, Id = conversationId, Json = json }, cancellationToken: ct));
    }

    public async Task<WhatsAppSelfServiceTripSummary?> FindAuthorizedTripOrBookingAsync(
        int tenantId, string phoneE164, string reference, CancellationToken ct = default)
    {
        var digits = DigitsOnly(phoneE164);
        var refKey = reference.Trim();
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
              AND (
                    REPLACE(REPLACE(c.Phone, N'+', N''), N' ', N'') LIKE N'%' + @Digits
                 OR REPLACE(REPLACE(c.Phone, N'+', N''), N' ', N'') = @Digits
              )
              AND (
                    t.TripNumber = @Ref
                 OR b.BookingNumber = @Ref
                 OR t.TripNumber LIKE N'%' + @Ref + N'%'
                 OR b.BookingNumber LIKE N'%' + @Ref + N'%'
              )
            ORDER BY t.Id DESC
            """, new { TenantId = tenantId, Digits = digits, Ref = refKey }, cancellationToken: ct));

        if (trip.TripId == 0)
        {
            // Booking-only (no trip yet)
            var booking = await connection.QuerySingleOrDefaultAsync<(
                int BookingId, string? BookingNumber, string Status, string? DriverFirstName,
                string? VehicleDescription, string? PlateNumber)>(new CommandDefinition("""
                SELECT TOP 1
                       b.Id AS BookingId,
                       b.BookingNumber,
                       CAST(b.Status AS NVARCHAR(40)) AS Status,
                       LEFT(d.FullName, CHARINDEX(' ', d.FullName + ' ') - 1) AS DriverFirstName,
                       COALESCE(v.Name, v.Make + N' ' + v.Model) AS VehicleDescription,
                       v.RegistrationNumber AS PlateNumber
                FROM Bookings b
                INNER JOIN Customers c ON c.Id = b.CustomerId AND c.IsDeleted = 0
                LEFT JOIN Drivers d ON d.Id = b.DriverId
                LEFT JOIN Vehicles v ON v.Id = b.VehicleId
                WHERE b.TenantId = @TenantId AND b.IsDeleted = 0
                  AND (
                        REPLACE(REPLACE(c.Phone, N'+', N''), N' ', N'') LIKE N'%' + @Digits
                     OR REPLACE(REPLACE(c.Phone, N'+', N''), N' ', N'') = @Digits
                  )
                  AND (b.BookingNumber = @Ref OR b.BookingNumber LIKE N'%' + @Ref + N'%')
                ORDER BY b.Id DESC
                """, new { TenantId = tenantId, Digits = digits, Ref = refKey }, cancellationToken: ct));

            if (booking.BookingId == 0)
                return null;

            return new WhatsAppSelfServiceTripSummary(
                0,
                null,
                booking.BookingId,
                booking.BookingNumber,
                booking.Status,
                booking.DriverFirstName,
                booking.VehicleDescription,
                booking.PlateNumber,
                null);
        }

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

        return new WhatsAppSelfServiceTripSummary(
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

    public async Task<IReadOnlyList<WhatsAppSelfServiceInvoiceItem>> ListInvoicesForPhoneAsync(
        int tenantId, string phoneE164, int take = 5, CancellationToken ct = default)
    {
        var digits = DigitsOnly(phoneE164);
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<WhatsAppSelfServiceInvoiceItem>(new CommandDefinition("""
            SELECT TOP (@Take)
                   b.Id AS BookingId,
                   b.BookingNumber,
                   b.PickupTime,
                   b.TotalAmount,
                   ISNULL((
                       SELECT SUM(p.Amount) FROM Payments p
                       WHERE p.BookingId = b.Id AND p.IsDeleted = 0
                         AND p.Status IN (N'Paid', N'PartiallyPaid', N'Completed')
                   ), 0) AS PaidAmount,
                   N'PKR' AS Currency
            FROM Bookings b
            INNER JOIN Customers c ON c.Id = b.CustomerId AND c.IsDeleted = 0
            WHERE b.TenantId = @TenantId AND b.IsDeleted = 0
              AND (
                    REPLACE(REPLACE(c.Phone, N'+', N''), N' ', N'') LIKE N'%' + @Digits
                 OR REPLACE(REPLACE(c.Phone, N'+', N''), N' ', N'') = @Digits
              )
              AND b.Status NOT IN (N'Cancelled', N'Canceled')
            ORDER BY b.PickupTime DESC, b.Id DESC
            """, new { TenantId = tenantId, Digits = digits, Take = Math.Clamp(take, 1, 10) },
            cancellationToken: ct))).ToList();
        return rows;
    }

    public async Task<WhatsAppSelfServiceInvoiceItem?> GetAuthorizedInvoiceAsync(
        int tenantId, string phoneE164, int bookingId, CancellationToken ct = default)
    {
        var all = await ListInvoicesForPhoneAsync(tenantId, phoneE164, 50, ct);
        return all.FirstOrDefault(i => i.BookingId == bookingId);
    }

    public async Task<int?> EnsureCustomerIdForPhoneAsync(
        int tenantId, string phoneE164, string? displayName, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        var digits = DigitsOnly(phoneE164);
        var existing = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 Id FROM Customers
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (
                    REPLACE(REPLACE(Phone, N'+', N''), N' ', N'') LIKE N'%' + @Digits
                 OR REPLACE(REPLACE(Phone, N'+', N''), N' ', N'') = @Digits
              )
            ORDER BY Id
            """, new { TenantId = tenantId, Digits = digits }, cancellationToken: ct));
        if (existing is > 0)
            return existing;

        var e164 = WhatsAppPhone.ToE164(phoneE164) ?? phoneE164;
        var name = string.IsNullOrWhiteSpace(displayName) ? "WhatsApp Customer" : displayName.Trim();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Customers (TenantId, FullName, Phone, IsActive, IsDeleted, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@TenantId, @FullName, @Phone, 1, 0, SYSUTCDATETIME())
            """, new { TenantId = tenantId, FullName = Truncate(name, 100), Phone = Truncate(e164, 20) },
            cancellationToken: ct));
    }

    public async Task<int?> GetDefaultRouteIdAsync(int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 Id FROM Routes
            WHERE TenantId = @TenantId AND IsDeleted = 0
            ORDER BY Id
            """, new { TenantId = tenantId }, cancellationToken: ct));
    }

    public async Task<(int VehicleId, string Name)?> FindVehicleByTypeHintAsync(
        int tenantId, string? vehicleTypeHint, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var hint = string.IsNullOrWhiteSpace(vehicleTypeHint) ? null : vehicleTypeHint.Trim();
        var row = await connection.QuerySingleOrDefaultAsync<(int Id, string Name)>(new CommandDefinition("""
            SELECT TOP 1 Id, COALESCE(Name, Make + N' ' + Model) AS Name
            FROM Vehicles
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (@Hint IS NULL
                   OR Name LIKE N'%' + @Hint + N'%'
                   OR Make LIKE N'%' + @Hint + N'%'
                   OR Model LIKE N'%' + @Hint + N'%')
            ORDER BY CASE WHEN @Hint IS NOT NULL AND (
                        Name LIKE N'%' + @Hint + N'%'
                     OR Make LIKE N'%' + @Hint + N'%'
                     OR Model LIKE N'%' + @Hint + N'%') THEN 0 ELSE 1 END, Id
            """, new { TenantId = tenantId, Hint = hint }, cancellationToken: ct));
        if (row.Id == 0) return null;
        return (row.Id, row.Name);
    }

    public async Task<bool> TryInsertFlowSubmissionAsync(
        int tenantId, int conversationId, string idempotencyKey, int? bookingId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        try
        {
            var n = await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO WhatsAppFlowSubmissions (TenantId, ConversationId, IdempotencyKey, BookingId)
                VALUES (@TenantId, @ConversationId, @Key, @BookingId)
                """, new
            {
                TenantId = tenantId,
                ConversationId = conversationId,
                Key = Truncate(idempotencyKey, 128),
                BookingId = bookingId
            }, cancellationToken: ct));
            return n > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<int?> GetBookingIdForFlowSubmissionAsync(
        int tenantId, string idempotencyKey, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT BookingId FROM WhatsAppFlowSubmissions
            WHERE TenantId = @TenantId AND IdempotencyKey = @Key
            """, new { TenantId = tenantId, Key = Truncate(idempotencyKey, 128) }, cancellationToken: ct));
    }

    public async Task LinkConversationCustomerAsync(
        int tenantId, int conversationId, int customerId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE WhatsAppConversations
            SET CustomerId = @CustomerId, UpdatedAt = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND Id = @Id
            """, new { TenantId = tenantId, Id = conversationId, CustomerId = customerId }, cancellationToken: ct));
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

    private static string Truncate(string v, int max)
        => v.Length <= max ? v : v[..max];
}
