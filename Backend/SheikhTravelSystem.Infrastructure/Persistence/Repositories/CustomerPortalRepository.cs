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
        string phone, int? jwtCustomerId, CancellationToken cancellationToken = default)
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

    public async Task<bool> CustomerOwnsBookingAsync(
        int bookingId, string phone, int? customerId, CancellationToken cancellationToken = default)
    {
        var customerIds = await ResolvePortalCustomerIdsAsync(phone, customerId, cancellationToken);
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

    public async Task<int?> ResolveCustomerIdByPhoneAsync(string phone, CancellationToken cancellationToken = default)
    {
        var ids = await ResolvePortalCustomerIdsAsync(phone, null, cancellationToken);
        return ids.Count > 0 ? ids[0] : null;
    }

    public async Task<int> EnsureCustomerAsync(
        string phone, string fullName, int tenantId, CancellationToken cancellationToken = default)
    {
        var normalized = PortalPhoneHelper.Normalize(phone);
        var existing = await ResolveCustomerIdByPhoneAsync(phone, cancellationToken);
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

    public async Task<IReadOnlyList<PortalRouteDto>> GetRoutesAsync(CancellationToken cancellationToken = default)
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
                  WHERE IsDeleted = 0 AND IsActive = 1
                  ORDER BY Source, Destination",
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<PortalVehicleDto>> GetVehiclesAsync(CancellationToken cancellationToken = default)
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
                  WHERE IsDeleted = 0 AND Status <> @Retired
                  ORDER BY Name",
                new { Retired = (int)VehicleStatus.Retired },
                cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<decimal?> GetRouteBasePriceAsync(int routeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(
                "SELECT BasePrice FROM Routes WHERE Id = @Id AND IsDeleted = 0 AND IsActive = 1",
                new { Id = routeId },
                cancellationToken: cancellationToken));
    }

    public async Task<bool> VehicleExistsAndNotRetiredAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND IsDeleted = 0 AND Status <> 4) THEN 1 ELSE 0 END",
                new { Id = vehicleId },
                cancellationToken: cancellationToken));
    }

    public async Task<decimal?> GetPerKmBasePriceAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(
                "SELECT TOP 1 BasePrice / NULLIF(Distance, 0) FROM Routes WHERE IsDeleted = 0 AND IsActive = 1 AND Distance > 0 ORDER BY Id",
                cancellationToken: cancellationToken));
    }

    public async Task<int> GetFirstActiveRouteIdAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT TOP 1 Id FROM Routes WHERE IsDeleted = 0 AND IsActive = 1 ORDER BY Id",
                cancellationToken: cancellationToken));
    }

    public async Task<string?> GetRouteLabelAsync(int routeId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                "SELECT Source + N' → ' + Destination FROM Routes WHERE Id = @Id",
                new { Id = routeId },
                cancellationToken: cancellationToken));
    }

    public async Task<int?> GetVehicleSeatingCapacityAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT SeatingCapacity FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                new { Id = vehicleId },
                cancellationToken: cancellationToken));
    }

    public async Task<string?> GetVehicleLabelAsync(int vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                "SELECT Name + N' (' + RegistrationNumber + N')' FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                new { Id = vehicleId },
                cancellationToken: cancellationToken));
    }

    public async Task<string?> GetBookingNumberAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                "SELECT BookingNumber FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
                new { Id = bookingId },
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
        IReadOnlyList<int> customerIds, CancellationToken cancellationToken = default)
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
                  LEFT JOIN Routes r ON r.Id = b.RouteId AND r.IsDeleted = 0
                  WHERE b.IsDeleted = 0 AND b.CustomerId IN @CustomerIds
                  ORDER BY b.PickupTime DESC",
                new
                {
                    CustomerIds = customerIds,
                    Paid = (int)PaymentStatus.Paid,
                    Partial = (int)PaymentStatus.PartiallyPaid
                },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<PortalBookingDetailHeadRow?> GetBookingDetailHeadAsync(
        int bookingId, IReadOnlyList<int> customerIds, CancellationToken cancellationToken = default)
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
                  LEFT JOIN Routes r ON r.Id = b.RouteId AND r.IsDeleted = 0
                  LEFT JOIN Vehicles v ON v.Id = b.VehicleId AND v.IsDeleted = 0
                  LEFT JOIN Drivers d ON d.Id = b.DriverId AND d.IsDeleted = 0
                  WHERE b.Id = @Id AND b.IsDeleted = 0 AND b.CustomerId IN @CustomerIds",
                new
                {
                    Id = bookingId,
                    CustomerIds = customerIds,
                    Paid = (int)PaymentStatus.Paid,
                    Partial = (int)PaymentStatus.PartiallyPaid
                },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PortalPaymentLineDto>> GetBookingPaymentsAsync(
        int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var payments = await connection.QueryAsync<PortalPaymentLineDto>(
            new CommandDefinition(
                @"SELECT Id, Amount, Status, PaymentDate, PaymentMethod
                  FROM Payments WHERE BookingId = @BookingId AND IsDeleted = 0 ORDER BY PaymentDate DESC",
                new { BookingId = bookingId },
                cancellationToken: cancellationToken));
        return payments.ToList();
    }

    public async Task<IReadOnlyList<string>> GetBookingSeatsAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var seats = await connection.QueryAsync<string>(
            new CommandDefinition(
                "SELECT SeatLabel FROM BookingSeats WHERE BookingId = @BookingId ORDER BY SeatLabel",
                new { BookingId = bookingId },
                cancellationToken: cancellationToken));
        return seats.ToList();
    }

    public async Task ApplyPortalBookingExtrasAsync(PortalBookingExtrasUpdate update, CancellationToken cancellationToken = default)
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
                  WHERE Id = @Id",
                new
                {
                    Id = update.BookingId,
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
        int vehicleId, string seatLabel, DateTime windowStart, DateTime windowEnd,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM BookingSeats bs
                INNER JOIN Bookings b ON b.Id = bs.BookingId
                WHERE b.VehicleId = @VehicleId AND bs.SeatLabel = @SeatLabel
                  AND b.IsDeleted = 0 AND b.Status <> @Cancelled
                  AND b.PickupTime BETWEEN @Start AND @End) THEN 1 ELSE 0 END",
            new
            {
                VehicleId = vehicleId,
                SeatLabel = seatLabel,
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
        int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var head = await connection.QuerySingleOrDefaultAsync<(int Status, int? VehicleId)?>(
            new CommandDefinition(
                "SELECT Status, VehicleId FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
                new { Id = bookingId },
                cancellationToken: cancellationToken));
        return head;
    }

    public async Task<(decimal? Speed, DateTime? UpdatedAt)?> GetVehicleLiveLocationAsync(
        int vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<(decimal? Speed, DateTime? UpdatedAt)?>(
            new CommandDefinition(
                @"SELECT Speed, Timestamp FROM VehicleCurrentLocation WHERE VehicleId = @VehicleId",
                new { VehicleId = vehicleId },
                cancellationToken: cancellationToken));
    }

    public async Task<string?> GetStartedBookingDriverPhoneAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                @"SELECT d.Phone FROM Bookings b
                  INNER JOIN Drivers d ON d.Id = b.DriverId
                  WHERE b.Id = @Id AND b.Status = @Started",
                new { Id = bookingId, Started = (int)BookingStatus.Started },
                cancellationToken: cancellationToken));
    }

    public async Task<PortalInvoiceRow?> GetInvoiceRowAsync(int bookingId, CancellationToken cancellationToken = default)
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
                  INNER JOIN Customers c ON c.Id = b.CustomerId
                  LEFT JOIN Routes r ON r.Id = b.RouteId
                  WHERE b.Id = @Id AND b.IsDeleted = 0",
                new
                {
                    Id = bookingId,
                    Paid = (int)PaymentStatus.Paid,
                    Partial = (int)PaymentStatus.PartiallyPaid
                },
                cancellationToken: cancellationToken));
    }

    public async Task<(int Status, DateTime PickupTime)?> GetBookingStatusPickupAsync(
        int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<(int Status, DateTime PickupTime)?>(
            new CommandDefinition(
                "SELECT Status, PickupTime FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
                new { Id = bookingId },
                cancellationToken: cancellationToken));
    }

    public async Task CancelBookingAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Bookings SET Status = @Status, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id",
                new { Id = bookingId, Status = (int)BookingStatus.Cancelled },
                cancellationToken: cancellationToken));
    }

    public async Task<PortalCheckoutBookingRow?> GetCheckoutBookingAsync(
        int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PortalCheckoutBookingRow>(
            new CommandDefinition(
                @"SELECT b.TotalAmount,
                         c.Email,
                         ISNULL(SUM(CASE WHEN p.Status IN (@Partial, @Paid) THEN p.Amount ELSE 0 END), 0) AS PaidAmount
                  FROM Bookings b
                  INNER JOIN Customers c ON c.Id = b.CustomerId AND c.IsDeleted = 0
                  LEFT JOIN Payments p ON p.BookingId = b.Id AND p.IsDeleted = 0
                  WHERE b.Id = @BookingId AND b.IsDeleted = 0
                  GROUP BY b.TotalAmount, c.Email",
                new
                {
                    BookingId = bookingId,
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
        int customerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PortalSavedAddressDto>(
            new CommandDefinition(
                @"SELECT Id, Label, AddressLine, Latitude, Longitude
                  FROM CustomerSavedAddresses WHERE CustomerId = @Id AND IsDeleted = 0 ORDER BY Label",
                new { Id = customerId },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<PortalCustomerNotificationDto>> GetCustomerNotificationsAsync(
        int customerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PortalCustomerNotificationDto>(
            new CommandDefinition(
                @"SELECT Id, Title, Message, NotificationType, BookingId, IsRead, CreatedAt
                  FROM CustomerNotifications WHERE CustomerId = @Id AND IsDeleted = 0
                  ORDER BY CreatedAt DESC",
                new { Id = customerId },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<(string SeatLabel, int RowIndex, int ColIndex)>> GetVehicleSeatLayoutsAsync(
        int vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var layouts = await connection.QueryAsync<(string SeatLabel, int RowIndex, int ColIndex)>(
            new CommandDefinition(
                @"SELECT SeatLabel, RowIndex, ColIndex FROM VehicleSeatLayouts
                  WHERE VehicleId = @VehicleId AND IsActive = 1 ORDER BY RowIndex, ColIndex",
                new { VehicleId = vehicleId },
                cancellationToken: cancellationToken));
        return layouts.ToList();
    }

    public async Task<IReadOnlyList<string>> GetBookedSeatsNearPickupAsync(
        int vehicleId, DateTime pickupTime, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var booked = await connection.QueryAsync<string>(
            new CommandDefinition(
                @"SELECT bs.SeatLabel FROM BookingSeats bs
                  INNER JOIN Bookings b ON b.Id = bs.BookingId AND b.IsDeleted = 0
                  WHERE b.VehicleId = @VehicleId AND b.Status IN (@P, @C, @S)
                    AND ABS(DATEDIFF(MINUTE, b.PickupTime, @PickupTime)) < 180",
                new
                {
                    VehicleId = vehicleId,
                    PickupTime = pickupTime,
                    P = (int)BookingStatus.Pending,
                    C = (int)BookingStatus.Confirmed,
                    S = (int)BookingStatus.Started
                },
                cancellationToken: cancellationToken));
        return booked.ToList();
    }

    public async Task<(int Points, string Tier)> GetLoyaltyAsync(int customerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleAsync<(int Points, string Tier)>(
            new CommandDefinition(
                "SELECT Points, Tier FROM CustomerLoyalty WHERE CustomerId = @Id",
                new { Id = customerId },
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

    public async Task<decimal> GetWalletBalanceAsync(int customerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                "SELECT Balance FROM CustomerWallets WHERE CustomerId = @Id",
                new { Id = customerId },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PortalFavoriteRouteDto>> GetFavoriteRoutesAsync(
        int customerId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PortalFavoriteRouteDto>(new CommandDefinition(
            @"SELECT f.Id, f.RouteId,
                     r.Source + ' -> ' + r.Destination AS RouteName, f.Label
              FROM CustomerFavoriteRoutes f
              INNER JOIN Routes r ON r.Id = f.RouteId
              WHERE f.CustomerId = @CustomerId AND f.IsDeleted = 0
              ORDER BY f.SortOrder, f.Id",
            new { CustomerId = customerId },
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
