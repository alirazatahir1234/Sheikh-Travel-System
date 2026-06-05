using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.CustomerPortal;

internal static class PortalBookingAccess
{
    public static async Task<IReadOnlyList<int>> ResolvePortalCustomerIdsAsync(
        IDbConnectionFactory dbFactory,
        string phone,
        int? jwtCustomerId,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<int>();
        if (jwtCustomerId is > 0)
            ids.Add(jwtCustomerId.Value);

        var variants = PortalPhoneHelper.LookupVariants(phone);
        var suffix = PortalPhoneHelper.MobileSuffix(phone);
        if (variants.Count == 0 && string.IsNullOrEmpty(suffix))
            return ids.ToList();

        using var connection = dbFactory.CreateConnection();
        var fromPhone = await connection.QueryAsync<int>(new CommandDefinition(
            """
            SELECT Id FROM Customers WHERE IsDeleted = 0 AND (
              Phone IN @Phones
              OR (@Suffix <> '' AND LEN(@Suffix) = 10 AND
                  RIGHT(REPLACE(REPLACE(REPLACE(REPLACE(Phone, '+', ''), ' ', ''), '-', ''), '(', ''), 10) = @Suffix)
            )
            """,
            new { Phones = variants.Count > 0 ? variants : new[] { phone.Trim() }, Suffix = suffix },
            cancellationToken: cancellationToken));

        foreach (var id in fromPhone)
            ids.Add(id);

        return ids.ToList();
    }

    public static async Task<bool> CustomerOwnsBookingAsync(
        IDbConnectionFactory dbFactory,
        int bookingId,
        string phone,
        int? customerId,
        CancellationToken cancellationToken)
    {
        var customerIds = await ResolvePortalCustomerIdsAsync(dbFactory, phone, customerId, cancellationToken);
        if (customerIds.Count == 0)
            return false;

        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM Bookings b
                WHERE b.Id = @BookingId AND b.IsDeleted = 0 AND b.CustomerId IN @CustomerIds
              ) THEN 1 ELSE 0 END",
            new { BookingId = bookingId, CustomerIds = customerIds },
            cancellationToken: cancellationToken));
    }

    /// <summary>Canonicalizes stored customer phones so portal JWT and bookings stay aligned.</summary>
    public static async Task NormalizeCustomerPhonesAsync(
        IDbConnectionFactory dbFactory,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<(int Id, string Phone)>(new CommandDefinition(
            "SELECT Id, Phone FROM Customers WHERE IsDeleted = 0 AND Phone IS NOT NULL",
            cancellationToken: cancellationToken));

        foreach (var row in rows)
        {
            var normalized = PortalPhoneHelper.Normalize(row.Phone);
            if (string.IsNullOrEmpty(normalized) || normalized == row.Phone)
                continue;

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE Customers SET Phone = @Phone WHERE Id = @Id AND IsDeleted = 0",
                new { Phone = normalized, row.Id },
                cancellationToken: cancellationToken));
        }
    }
}
