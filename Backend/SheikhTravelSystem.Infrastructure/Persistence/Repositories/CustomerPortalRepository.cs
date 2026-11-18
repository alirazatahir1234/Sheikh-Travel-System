using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.CustomerPortal;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Application.Features.CustomerPortal.Queries;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class CustomerPortalRepository(IDbConnectionFactory dbFactory) : ICustomerPortalRepository
{
    public async Task<IReadOnlyList<int>> ResolvePortalCustomerIdsAsync(
        int tenantId, string phone, int? jwtCustomerId, CancellationToken cancellationToken = default)
    {
        var ids = new HashSet<int>();
        using var connection = dbFactory.CreateConnection();

        if (jwtCustomerId is > 0)
        {
            var owned = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                """
                SELECT TOP 1 Id FROM Customers
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
                """,
                new { Id = jwtCustomerId.Value, TenantId = tenantId },
                cancellationToken: cancellationToken));
            if (owned is > 0)
                ids.Add(owned.Value);
        }

        var variants = PortalPhoneHelper.LookupVariants(phone);
        var suffix = PortalPhoneHelper.MobileSuffix(phone);
        if (variants.Count == 0 && string.IsNullOrEmpty(suffix))
            return ids.ToList();

        var fromPhone = await connection.QueryAsync<int>(new CommandDefinition(
            """
            SELECT Id FROM Customers WHERE IsDeleted = 0 AND TenantId = @TenantId AND (
              Phone IN @Phones
              OR (@Suffix <> '' AND LEN(@Suffix) = 10 AND
                  RIGHT(REPLACE(REPLACE(REPLACE(REPLACE(Phone, '+', ''), ' ', ''), '-', ''), '(', ''), 10) = @Suffix)
            )
            """,
            new
            {
                TenantId = tenantId,
                Phones = variants.Count > 0 ? variants : new[] { phone.Trim() },
                Suffix = suffix
            },
            cancellationToken: cancellationToken));

        foreach (var id in fromPhone)
            ids.Add(id);

        return ids.ToList();
    }

    public async Task<bool> CustomerOwnsBookingAsync(
        int tenantId, int bookingId, string phone, int? customerId, CancellationToken cancellationToken = default)
    {
        var customerIds = await ResolvePortalCustomerIdsAsync(tenantId, phone, customerId, cancellationToken);
        if (customerIds.Count == 0)
            return false;

        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM Bookings b
                WHERE b.Id = @BookingId AND b.IsDeleted = 0
                  AND b.TenantId = @TenantId AND b.CustomerId IN @CustomerIds
              ) THEN 1 ELSE 0 END",
            new { BookingId = bookingId, TenantId = tenantId, CustomerIds = customerIds },
            cancellationToken: cancellationToken));
    }

    // PLATFORM-SCOPE: startup phone normalize across customers (seed path).
    public async Task NormalizeCustomerPhonesAsync(CancellationToken cancellationToken = default)
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

    public async Task<int?> ResolveCustomerIdByPhoneAsync(int tenantId, string phone, CancellationToken cancellationToken = default)
    {
        var ids = await ResolvePortalCustomerIdsAsync(tenantId, phone, null, cancellationToken);
        return ids.Count > 0 ? ids[0] : null;
    }

    public async Task<int> EnsureCustomerAsync(
        string phone, string fullName, int tenantId, CancellationToken cancellationToken = default)
    {
        var normalized = PortalPhoneHelper.Normalize(phone);
        var existing = await ResolveCustomerIdByPhoneAsync(tenantId, phone, cancellationToken);
        if (existing.HasValue)
        {
            using var connection = dbFactory.CreateConnection();
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE Customers SET Phone = @Phone, FullName = @FullName WHERE Id = @Id AND IsDeleted = 0",
                new { Phone = normalized, FullName = fullName.Trim(), Id = existing.Value },
                cancellationToken: cancellationToken));
            return existing.Value;
        }

        using var insert = dbFactory.CreateConnection();
        return await insert.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO Customers (FullName, Phone, IsActive, TenantId, CreatedAt, CreatedBy, IsDeleted)
              VALUES (@FullName, @Phone, 1, @TenantId, SYSUTCDATETIME(), 'portal', 0);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { FullName = fullName.Trim(), Phone = normalized, TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    public async Task WriteCustomerNotificationAsync(
        int customerId, string title, string message, string type, int? bookingId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO CustomerNotifications (CustomerId, Title, Message, NotificationType, BookingId, IsRead, CreatedAt, IsDeleted)
                  VALUES (@CustomerId, @Title, @Message, @Type, @BookingId, 0, SYSUTCDATETIME(), 0)",
                new { CustomerId = customerId, Title = title, Message = message, Type = type, BookingId = bookingId },
                cancellationToken: cancellationToken));
    }

    public async Task EnsureLoyaltyRowAsync(int customerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                @"IF NOT EXISTS (SELECT 1 FROM CustomerLoyalty WHERE CustomerId = @Id)
                  INSERT INTO CustomerLoyalty (CustomerId, Points, Tier) VALUES (@Id, 0, 'Bronze')",
                new { Id = customerId },
                cancellationToken: cancellationToken));
    }

    public async Task AddLoyaltyPointsAsync(int customerId, int points, CancellationToken cancellationToken = default)
    {
        await EnsureLoyaltyRowAsync(customerId, cancellationToken);
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE CustomerLoyalty SET Points = Points + @Pts, UpdatedAt = SYSUTCDATETIME() WHERE CustomerId = @Id",
                new { Pts = points, Id = customerId },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PortalRouteDto>> GetRoutesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PortalRouteDto>(
            new CommandDefinition(
                @"SELECT Id,
                         (COALESCE(Source, N'') + N' → ' + COALESCE(Destination, N'')) AS Label,
                         Distance AS DistanceKm,
                         BasePrice,
                         COALESCE(Source, N'') AS Source,
                         COALESCE(Destination, N'') AS Destination,
                         Name,
                         COALESCE(EstimatedMinutes,
                             CASE WHEN Distance > 0 THEN CAST(CEILING(Distance / 70.0 * 60) AS INT) ELSE 120 END) AS EstimatedDurationMinutes
                  FROM Routes
                  WHERE IsDeleted = 0 AND IsActive = 1 AND TenantId = @TenantId
                  ORDER BY Source, Destination",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<PortalVehicleDto>> GetVehiclesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PortalVehicleDto>(
            new CommandDefinition(
                @"SELECT Id,
                         Name,
                         RegistrationNumber,
                         SeatingCapacity,
                         FuelAverage,
                         Model,
                         [Year],
                         FuelType,
                         Status
                  FROM Vehicles
                  WHERE IsDeleted = 0 AND Status <> @Retired AND TenantId = @TenantId
                  ORDER BY Name",
                new { Retired = (int)VehicleStatus.Retired, TenantId = tenantId },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<decimal?> GetRouteBasePriceAsync(int tenantId, int routeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(
                "SELECT BasePrice FROM Routes WHERE Id = @Id AND IsDeleted = 0 AND IsActive = 1 AND TenantId = @TenantId",
                new { Id = routeId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> VehicleExistsAndNotRetiredAsync(int tenantId, int vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND IsDeleted = 0 AND Status <> 4 AND TenantId = @TenantId) THEN 1 ELSE 0 END",
                new { Id = vehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<decimal?> GetPerKmBasePriceAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(
                "SELECT TOP 1 BasePrice / NULLIF(Distance, 0) FROM Routes WHERE IsDeleted = 0 AND IsActive = 1 AND Distance > 0 AND TenantId = @TenantId ORDER BY Id",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<int> GetFirstActiveRouteIdAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT TOP 1 Id FROM Routes WHERE IsDeleted = 0 AND IsActive = 1 AND TenantId = @TenantId ORDER BY Id",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<string?> GetRouteLabelAsync(int tenantId, int routeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                "SELECT Source + N' → ' + Destination FROM Routes WHERE Id = @Id AND TenantId = @TenantId",
                new { Id = routeId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<int?> GetVehicleSeatingCapacityAsync(int tenantId, int vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT SeatingCapacity FROM Vehicles WHERE Id = @Id AND IsDeleted = 0 AND TenantId = @TenantId",
                new { Id = vehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<string?> GetVehicleLabelAsync(int tenantId, int vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                "SELECT Name + N' (' + RegistrationNumber + N')' FROM Vehicles WHERE Id = @Id AND IsDeleted = 0 AND TenantId = @TenantId",
                new { Id = vehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<string?> GetBookingNumberAsync(int tenantId, int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                "SELECT BookingNumber FROM Bookings WHERE Id = @Id AND IsDeleted = 0 AND TenantId = @TenantId",
                new { Id = bookingId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<int?> GetPromoCodeIdAsync(string code, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT Id FROM PromoCodes WHERE Code = @Code AND IsDeleted = 0",
                new { Code = code },
                cancellationToken: cancellationToken));
    }

    public async Task<(int Id, decimal? Pct, decimal? Fixed)?> GetActivePromoAsync(
        string code, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var promo = await connection.QuerySingleOrDefaultAsync<(int Id, decimal? Pct, decimal? Fixed)>(
            new CommandDefinition(
                @"SELECT Id, DiscountPercent, DiscountFixed FROM PromoCodes
                  WHERE Code = @Code AND IsActive = 1 AND IsDeleted = 0
                    AND (ValidFrom IS NULL OR ValidFrom <= SYSUTCDATETIME())
                    AND (ValidTo IS NULL OR ValidTo >= SYSUTCDATETIME())",
                new { Code = code },
                cancellationToken: cancellationToken));
        return promo.Id == 0 ? null : promo;
    }

    public async Task<IReadOnlyList<PortalBookingCardRow>> GetBookingCardsAsync(
        int tenantId, IReadOnlyList<int> customerIds, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PortalBookingCardRow>(
            new CommandDefinition(
                @"SELECT b.Id,
                         b.BookingNumber,
                         ISNULL(r.Source + N' → ' + r.Destination, N'') AS RouteLabel,
                         b.PickupTime,
                         b.Status AS Status,
                         b.TotalAmount,
                         ISNULL((
                           SELECT SUM(p.Amount)
                           FROM Payments p
                           WHERE p.BookingId = b.Id
                             AND p.Status IN (@Paid, @Partial)
                             AND p.IsDeleted = 0
                         ), 0) AS PaidAmount
                  FROM Bookings b
                  LEFT JOIN Routes r ON r.Id = b.RouteId AND r.IsDeleted = 0 AND r.TenantId = b.TenantId
                  WHERE b.IsDeleted = 0 AND b.TenantId = @TenantId AND b.CustomerId IN @CustomerIds
                  ORDER BY b.PickupTime DESC",
                new
                {
                    TenantId = tenantId,
                    CustomerIds = customerIds,
                    Paid = (int)PaymentStatus.Paid,
                    Partial = (int)PaymentStatus.PartiallyPaid
                },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<PortalBookingDetailHeadRow?> GetBookingDetailHeadAsync(
        int tenantId, int bookingId, IReadOnlyList<int> customerIds, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PortalBookingDetailHeadRow>(
            new CommandDefinition(
                @"SELECT b.Id,
                         b.BookingNumber,
                         ISNULL(NULLIF(b.PickupAddress + N' → ' + b.DropoffAddress, N' → '), ISNULL(r.Source + N' → ' + r.Destination, N'')) AS RouteLabel,
                         b.PickupTime,
                         b.PassengerCount,
                         v.Name AS VehicleName,
                         b.Status,
                         b.TotalAmount,
                         ISNULL((
                           SELECT SUM(p.Amount)
                           FROM Payments p
                           WHERE p.BookingId = b.Id
                             AND p.Status IN (@Paid, @Partial)
                             AND p.IsDeleted = 0
                         ), 0) AS PaidAmount,
                         b.PickupAddress,
                         b.DropoffAddress,
                         b.DriverId,
                         d.FullName AS DriverName,
                         d.Rating AS DriverRating,
                         d.YearsExperience AS DriverYears
                  FROM Bookings b
                  LEFT JOIN Routes r ON r.Id = b.RouteId AND r.IsDeleted = 0 AND r.TenantId = b.TenantId
                  LEFT JOIN Vehicles v ON v.Id = b.VehicleId AND v.IsDeleted = 0 AND v.TenantId = b.TenantId
                  LEFT JOIN Drivers d ON d.Id = b.DriverId AND d.IsDeleted = 0 AND d.TenantId = b.TenantId
                  WHERE b.Id = @Id AND b.IsDeleted = 0 AND b.TenantId = @TenantId AND b.CustomerId IN @CustomerIds",
                new
                {
                    Id = bookingId,
                    TenantId = tenantId,
                    CustomerIds = customerIds,
                    Paid = (int)PaymentStatus.Paid,
                    Partial = (int)PaymentStatus.PartiallyPaid
                },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PortalPaymentLineDto>> GetBookingPaymentsAsync(
        int tenantId, int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var payments = await connection.QueryAsync<PortalPaymentLineDto>(
            new CommandDefinition(
                @"SELECT p.Id, p.Amount, p.Status, p.PaymentDate, p.PaymentMethod
                  FROM Payments p
                  INNER JOIN Bookings b ON b.Id = p.BookingId AND b.TenantId = @TenantId AND b.IsDeleted = 0
                  WHERE p.BookingId = @BookingId AND p.IsDeleted = 0
                  ORDER BY p.PaymentDate DESC",
                new { BookingId = bookingId, TenantId = tenantId },
                cancellationToken: cancellationToken));
        return payments.ToList();
    }

    public async Task<IReadOnlyList<string>> GetBookingSeatsAsync(
        int tenantId, int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var seats = await connection.QueryAsync<string>(
            new CommandDefinition(
                @"SELECT bs.SeatLabel
                  FROM BookingSeats bs
                  INNER JOIN Bookings b ON b.Id = bs.BookingId AND b.TenantId = @TenantId AND b.IsDeleted = 0
                  WHERE bs.BookingId = @BookingId
                  ORDER BY bs.SeatLabel",
                new { BookingId = bookingId, TenantId = tenantId },
                cancellationToken: cancellationToken));
        return seats.ToList();
    }

    public async Task ApplyPortalBookingExtrasAsync(
        int tenantId, PortalBookingExtrasUpdate update, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Bookings SET
                    PreferredPaymentMethod = @PreferredPaymentMethod,
                    PickupAddress = @PickupAddress,
                    DropoffAddress = @DropoffAddress,
                    PickupLat = @PickupLat,
                    PickupLng = @PickupLng,
                    DropLat = @DropLat,
                    DropLng = @DropLng,
                    QuotedDistanceKm = @QuotedDistanceKm,
                    QuotedDurationMinutes = @QuotedDurationMinutes,
                    AdultCount = @AdultCount,
                    ChildCount = @ChildCount,
                    LuggageCount = @LuggageCount,
                    PromoCodeId = @PromoCodeId,
                    DiscountAmount = @DiscountAmount,
                    UpdatedAt = SYSUTCDATETIME()
                  WHERE Id = @Id AND TenantId = @TenantId",
                new
                {
                    Id = update.BookingId,
                    TenantId = tenantId,
                    update.PreferredPaymentMethod,
                    update.PickupAddress,
                    update.DropoffAddress,
                    update.PickupLat,
                    update.PickupLng,
                    update.DropLat,
                    update.DropLng,
                    update.QuotedDistanceKm,
                    update.QuotedDurationMinutes,
                    update.AdultCount,
                    update.ChildCount,
                    update.LuggageCount,
                    update.PromoCodeId,
                    update.DiscountAmount
                },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> IsSeatTakenAsync(
        int tenantId, int vehicleId, string seatLabel, DateTime windowStart, DateTime windowEnd,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM BookingSeats bs
                INNER JOIN Bookings b ON b.Id = bs.BookingId
                WHERE b.VehicleId = @VehicleId AND bs.SeatLabel = @SeatLabel
                  AND b.IsDeleted = 0 AND b.Status <> @Cancelled
                  AND b.TenantId = @TenantId
                  AND b.PickupTime BETWEEN @Start AND @End) THEN 1 ELSE 0 END",
            new
            {
                VehicleId = vehicleId,
                SeatLabel = seatLabel,
                TenantId = tenantId,
                Cancelled = (int)BookingStatus.Cancelled,
                Start = windowStart,
                End = windowEnd
            },
            cancellationToken: cancellationToken));
    }

    public async Task InsertBookingSeatAsync(int bookingId, string seatLabel, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO BookingSeats (BookingId, SeatLabel) VALUES (@BookingId, @SeatLabel)",
                new { BookingId = bookingId, SeatLabel = seatLabel },
                cancellationToken: cancellationToken));
    }

    public async Task<(int Status, int? VehicleId)?> GetBookingStatusVehicleAsync(
        int tenantId, int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var head = await connection.QuerySingleOrDefaultAsync<(int Status, int? VehicleId)?>(
            new CommandDefinition(
                "SELECT Status, VehicleId FROM Bookings WHERE Id = @Id AND IsDeleted = 0 AND TenantId = @TenantId",
                new { Id = bookingId, TenantId = tenantId },
                cancellationToken: cancellationToken));
        return head;
    }

    public async Task<(decimal? Speed, DateTime? UpdatedAt)?> GetVehicleLiveLocationAsync(
        int tenantId, int vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<(decimal? Speed, DateTime? UpdatedAt)?>(
            new CommandDefinition(
                @"SELECT loc.Speed, loc.Timestamp
                  FROM VehicleCurrentLocation loc
                  INNER JOIN Vehicles v ON v.Id = loc.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
                  WHERE loc.VehicleId = @VehicleId",
                new { VehicleId = vehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<string?> GetStartedBookingDriverPhoneAsync(
        int tenantId, int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                @"SELECT d.Phone FROM Bookings b
                  INNER JOIN Drivers d ON d.Id = b.DriverId AND d.TenantId = b.TenantId
                  WHERE b.Id = @Id AND b.Status = @Started AND b.TenantId = @TenantId",
                new { Id = bookingId, Started = (int)BookingStatus.Started, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<PortalInvoiceRow?> GetInvoiceRowAsync(
        int tenantId, int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PortalInvoiceRow>(
            new CommandDefinition(
                @"SELECT b.BookingNumber,
                         ISNULL(r.Source + N' → ' + r.Destination, N'') AS RouteLabel,
                         b.PickupTime,
                         b.TotalAmount,
                         ISNULL((
                           SELECT SUM(p.Amount)
                           FROM Payments p
                           WHERE p.BookingId = b.Id
                             AND p.Status IN (@Paid, @Partial)
                             AND p.IsDeleted = 0
                         ), 0) AS PaidAmount,
                         c.FullName AS CustomerName,
                         c.Phone AS CustomerPhone
                  FROM Bookings b
                  INNER JOIN Customers c ON c.Id = b.CustomerId AND c.TenantId = b.TenantId
                  LEFT JOIN Routes r ON r.Id = b.RouteId AND r.TenantId = b.TenantId
                  WHERE b.Id = @Id AND b.IsDeleted = 0 AND b.TenantId = @TenantId",
                new
                {
                    Id = bookingId,
                    TenantId = tenantId,
                    Paid = (int)PaymentStatus.Paid,
                    Partial = (int)PaymentStatus.PartiallyPaid
                },
                cancellationToken: cancellationToken));
    }

    public async Task<(int Status, DateTime PickupTime)?> GetBookingStatusPickupAsync(
        int tenantId, int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<(int Status, DateTime PickupTime)?>(
            new CommandDefinition(
                "SELECT Status, PickupTime FROM Bookings WHERE Id = @Id AND IsDeleted = 0 AND TenantId = @TenantId",
                new { Id = bookingId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task CancelBookingAsync(int tenantId, int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Bookings SET Status = @Status, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND TenantId = @TenantId",
                new { Id = bookingId, TenantId = tenantId, Status = (int)BookingStatus.Cancelled },
                cancellationToken: cancellationToken));
    }

    public async Task<PortalCheckoutBookingRow?> GetCheckoutBookingAsync(
        int tenantId, int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PortalCheckoutBookingRow>(
            new CommandDefinition(
                @"SELECT b.TotalAmount,
                         c.Email,
                         ISNULL(SUM(CASE WHEN p.Status IN (@Partial, @Paid) THEN p.Amount ELSE 0 END), 0) AS PaidAmount
                  FROM Bookings b
                  INNER JOIN Customers c ON c.Id = b.CustomerId AND c.IsDeleted = 0 AND c.TenantId = b.TenantId
                  LEFT JOIN Payments p ON p.BookingId = b.Id AND p.IsDeleted = 0
                  WHERE b.Id = @BookingId AND b.IsDeleted = 0 AND b.TenantId = @TenantId
                  GROUP BY b.TotalAmount, c.Email",
                new
                {
                    BookingId = bookingId,
                    TenantId = tenantId,
                    Partial = (int)PaymentStatus.PartiallyPaid,
                    Paid = (int)PaymentStatus.Paid
                },
                cancellationToken: cancellationToken));
    }

    public async Task<int> SaveAddressAsync(
        int customerId, string label, string addressLine, double? lat, double? lng,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO CustomerSavedAddresses (CustomerId, Label, AddressLine, Latitude, Longitude)
                  VALUES (@CustomerId, @Label, @AddressLine, @Lat, @Lng);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    CustomerId = customerId,
                    Label = label,
                    AddressLine = addressLine,
                    Lat = lat,
                    Lng = lng
                },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PortalSavedAddressDto>> GetSavedAddressesAsync(
        int tenantId, int customerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PortalSavedAddressDto>(
            new CommandDefinition(
                @"SELECT a.Id, a.Label, a.AddressLine, a.Latitude, a.Longitude
                  FROM CustomerSavedAddresses a
                  INNER JOIN Customers c ON c.Id = a.CustomerId AND c.TenantId = @TenantId AND c.IsDeleted = 0
                  WHERE a.CustomerId = @Id AND a.IsDeleted = 0
                  ORDER BY a.Label",
                new { Id = customerId, TenantId = tenantId },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<PortalCustomerNotificationDto>> GetCustomerNotificationsAsync(
        int tenantId, int customerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PortalCustomerNotificationDto>(
            new CommandDefinition(
                @"SELECT n.Id, n.Title, n.Message, n.NotificationType, n.BookingId, n.IsRead, n.CreatedAt
                  FROM CustomerNotifications n
                  INNER JOIN Customers c ON c.Id = n.CustomerId AND c.TenantId = @TenantId AND c.IsDeleted = 0
                  WHERE n.CustomerId = @Id AND n.IsDeleted = 0
                  ORDER BY n.CreatedAt DESC",
                new { Id = customerId, TenantId = tenantId },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<(string SeatLabel, int RowIndex, int ColIndex)>> GetVehicleSeatLayoutsAsync(
        int tenantId, int vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var layouts = await connection.QueryAsync<(string SeatLabel, int RowIndex, int ColIndex)>(
            new CommandDefinition(
                @"SELECT s.SeatLabel, s.RowIndex, s.ColIndex
                  FROM VehicleSeatLayouts s
                  INNER JOIN Vehicles v ON v.Id = s.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
                  WHERE s.VehicleId = @VehicleId AND s.IsActive = 1
                  ORDER BY s.RowIndex, s.ColIndex",
                new { VehicleId = vehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));
        return layouts.ToList();
    }

    public async Task<IReadOnlyList<string>> GetBookedSeatsNearPickupAsync(
        int tenantId, int vehicleId, DateTime pickupTime, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var booked = await connection.QueryAsync<string>(
            new CommandDefinition(
                @"SELECT bs.SeatLabel FROM BookingSeats bs
                  INNER JOIN Bookings b ON b.Id = bs.BookingId AND b.IsDeleted = 0 AND b.TenantId = @TenantId
                  WHERE b.VehicleId = @VehicleId AND b.Status IN (@P, @C, @S)
                    AND ABS(DATEDIFF(MINUTE, b.PickupTime, @PickupTime)) < 180",
                new
                {
                    VehicleId = vehicleId,
                    TenantId = tenantId,
                    PickupTime = pickupTime,
                    P = (int)BookingStatus.Pending,
                    C = (int)BookingStatus.Confirmed,
                    S = (int)BookingStatus.Started
                },
                cancellationToken: cancellationToken));
        return booked.ToList();
    }

    public async Task<(int Points, string Tier)> GetLoyaltyAsync(
        int tenantId, int customerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleAsync<(int Points, string Tier)>(
            new CommandDefinition(
                @"SELECT l.Points, l.Tier
                  FROM CustomerLoyalty l
                  INNER JOIN Customers c ON c.Id = l.CustomerId AND c.TenantId = @TenantId AND c.IsDeleted = 0
                  WHERE l.CustomerId = @Id",
                new { Id = customerId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task EnsureWalletRowAsync(int customerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                @"IF NOT EXISTS (SELECT 1 FROM CustomerWallets WHERE CustomerId = @Id)
                  INSERT INTO CustomerWallets (CustomerId, Balance) VALUES (@Id, 0)",
                new { Id = customerId },
                cancellationToken: cancellationToken));
    }

    public async Task<decimal> GetWalletBalanceAsync(
        int tenantId, int customerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                @"SELECT w.Balance
                  FROM CustomerWallets w
                  INNER JOIN Customers c ON c.Id = w.CustomerId AND c.TenantId = @TenantId AND c.IsDeleted = 0
                  WHERE w.CustomerId = @Id",
                new { Id = customerId, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PortalFavoriteRouteDto>> GetFavoriteRoutesAsync(
        int tenantId, int customerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PortalFavoriteRouteDto>(new CommandDefinition(
            @"SELECT f.Id, f.RouteId,
                     r.Source + ' -> ' + r.Destination AS RouteName, f.Label
              FROM CustomerFavoriteRoutes f
              INNER JOIN Customers c ON c.Id = f.CustomerId AND c.TenantId = @TenantId AND c.IsDeleted = 0
              INNER JOIN Routes r ON r.Id = f.RouteId AND r.TenantId = @TenantId
              WHERE f.CustomerId = @CustomerId AND f.IsDeleted = 0
              ORDER BY f.SortOrder, f.Id",
            new { CustomerId = customerId, TenantId = tenantId },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<int> AddFavoriteRouteAsync(
        int customerId, int routeId, string? label, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO CustomerFavoriteRoutes (CustomerId, RouteId, Label, SortOrder, CreatedAt, IsDeleted)
              VALUES (@CustomerId, @RouteId, @Label, 0, GETUTCDATE(), 0);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { CustomerId = customerId, RouteId = routeId, Label = label },
            cancellationToken: cancellationToken));
    }
}
