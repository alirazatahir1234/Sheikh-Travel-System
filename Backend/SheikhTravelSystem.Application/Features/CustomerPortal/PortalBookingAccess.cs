using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.CustomerPortal;

internal static class PortalBookingAccess
{
    public static async Task<bool> PhoneOwnsBookingAsync(
        IDbConnectionFactory dbFactory,
        int bookingId,
        string phone,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Bookings b
                    INNER JOIN Customers c ON c.Id = b.CustomerId AND c.IsDeleted = 0
                    WHERE b.Id = @BookingId AND b.IsDeleted = 0 AND c.Phone = @Phone) THEN 1 ELSE 0 END",
                new { BookingId = bookingId, Phone = phone.Trim() },
                cancellationToken: cancellationToken));
    }
}
