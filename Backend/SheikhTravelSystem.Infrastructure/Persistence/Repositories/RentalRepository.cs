using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Rentals.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class RentalRepository(IDbConnectionFactory dbFactory) : IRentalRepository
{
    private const string RateSelect = """
        SELECT r.Id, r.VehicleId, v.Name AS VehicleName, v.RegistrationNumber,
               r.RateType, r.Amount, r.DepositAmount, r.Currency, r.IsActive, r.Notes
        FROM RentalRates r
        LEFT JOIN Vehicles v ON v.Id = r.VehicleId AND v.TenantId = r.TenantId AND v.IsDeleted = 0
        """;

    private const string BookingListSelect = """
        SELECT b.Id, b.RentalNumber, b.CustomerId, c.FullName AS CustomerName,
               b.VehicleId, v.Name AS VehicleName, v.RegistrationNumber,
               b.RateType, b.StartAt, b.EndAt, b.TotalAmount, b.DepositAmount,
               b.Status, b.DepositStatus, b.PaymentStatus
        FROM RentalBookings b
        LEFT JOIN Customers c ON c.Id = b.CustomerId AND c.TenantId = b.TenantId AND c.IsDeleted = 0
        LEFT JOIN Vehicles v ON v.Id = b.VehicleId AND v.TenantId = b.TenantId AND v.IsDeleted = 0
        """;

    private const string BookingDetailSelect = """
        SELECT b.Id, b.RentalNumber, b.CustomerId, c.FullName AS CustomerName, c.Phone AS CustomerPhone,
               b.VehicleId, v.Name AS VehicleName, v.RegistrationNumber, b.VehicleCategory,
               b.RateId, b.RateType, b.RateAmount, b.StartAt, b.EndAt,
               b.PickupLocation, b.ReturnLocation, b.DiscountAmount,
               b.TotalAmount, b.DepositAmount, b.DepositHeld, b.DepositRefunded,
               b.DamageCharges, b.DamageNotes, b.Status, b.DepositStatus, b.PaymentStatus,
               b.PaidAmount, b.PaymentMethod, b.PaymentReference, b.LinkedPaymentId,
               b.PickedUpAt, b.PickupOdometer, b.PickupFuelLevel, b.PickupNotes,
               b.ReturnedAt, b.ReturnOdometer, b.ReturnFuelLevel, b.ReturnNotes,
               b.AgreementText, b.AgreementSignedAt, b.AgreementSignedBy,
               b.Notes, b.CreatedAt
        FROM RentalBookings b
        LEFT JOIN Customers c ON c.Id = b.CustomerId AND c.TenantId = b.TenantId AND c.IsDeleted = 0
        LEFT JOIN Vehicles v ON v.Id = b.VehicleId AND v.TenantId = b.TenantId AND v.IsDeleted = 0
        """;

    public async Task<PagedResult<RentalRateDto>> GetRatesPagedAsync(
        int tenantId, int page, int pageSize, int? vehicleId, string? rateType, bool? isActive,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;
        var clauses = new List<string> { "r.IsDeleted = 0", "r.TenantId = @TenantId" };
        if (vehicleId.HasValue) clauses.Add("r.VehicleId = @VehicleId");
        if (!string.IsNullOrWhiteSpace(rateType)) clauses.Add("r.RateType = @RateType");
        if (isActive.HasValue) clauses.Add("r.IsActive = @IsActive");
        var where = string.Join(" AND ", clauses);

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM RentalRates r WHERE {where}",
            new { TenantId = tenantId, VehicleId = vehicleId, RateType = rateType, IsActive = isActive },
            cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<RentalRateDto>(new CommandDefinition($"""
            {RateSelect}
            WHERE {where}
            ORDER BY r.RateType, v.Name, r.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """,
            new { TenantId = tenantId, VehicleId = vehicleId, RateType = rateType, IsActive = isActive, Offset = offset, PageSize = pageSize },
            cancellationToken: cancellationToken))).ToList();

        return new PagedResult<RentalRateDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<RentalRateDto?> GetRateByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<RentalRateDto>(new CommandDefinition($"""
            {RateSelect}
            WHERE r.Id = @Id AND r.TenantId = @TenantId AND r.IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<int> CreateRateAsync(int tenantId, CreateRentalRateDto dto, int? createdBy, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO RentalRates (TenantId, VehicleId, RateType, Amount, DepositAmount, Currency, IsActive, Notes, CreatedAt, CreatedBy, IsDeleted)
            VALUES (@TenantId, @VehicleId, @RateType, @Amount, @DepositAmount, @Currency, 1, @Notes, SYSUTCDATETIME(), @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new
            {
                TenantId = tenantId,
                dto.VehicleId,
                RateType = dto.RateType.Trim(),
                dto.Amount,
                dto.DepositAmount,
                Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "PKR" : dto.Currency.Trim(),
                dto.Notes,
                CreatedBy = createdBy
            },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateRateAsync(int id, int tenantId, UpdateRentalRateDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE RentalRates
            SET VehicleId = @VehicleId, RateType = @RateType, Amount = @Amount, DepositAmount = @DepositAmount,
                Currency = @Currency, IsActive = @IsActive, Notes = @Notes, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """,
            new
            {
                Id = id,
                TenantId = tenantId,
                dto.VehicleId,
                RateType = dto.RateType.Trim(),
                dto.Amount,
                dto.DepositAmount,
                Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "PKR" : dto.Currency.Trim(),
                dto.IsActive,
                dto.Notes
            },
            cancellationToken: cancellationToken));
        return rows > 0;
    }

    public async Task<bool> SoftDeleteRateAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE RentalRates SET IsDeleted = 1, IsActive = 0, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows > 0;
    }

    public async Task<RentalRateDto?> FindBestRateAsync(
        int tenantId, int vehicleId, string rateType, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<RentalRateDto>(new CommandDefinition($"""
            {RateSelect}
            WHERE r.TenantId = @TenantId AND r.IsDeleted = 0 AND r.IsActive = 1
              AND r.RateType = @RateType
              AND (r.VehicleId = @VehicleId OR r.VehicleId IS NULL)
            ORDER BY CASE WHEN r.VehicleId = @VehicleId THEN 0 ELSE 1 END, r.Id DESC
            """,
            new { TenantId = tenantId, VehicleId = vehicleId, RateType = rateType.Trim() },
            cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<RentalBookingListItemDto>> GetBookingsPagedAsync(
        int tenantId, int page, int pageSize, string? status, int? vehicleId, int? customerId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;
        var clauses = new List<string> { "b.IsDeleted = 0", "b.TenantId = @TenantId" };
        if (!string.IsNullOrWhiteSpace(status)) clauses.Add("b.Status = @Status");
        if (vehicleId.HasValue) clauses.Add("b.VehicleId = @VehicleId");
        if (customerId.HasValue) clauses.Add("b.CustomerId = @CustomerId");
        var where = string.Join(" AND ", clauses);

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM RentalBookings b WHERE {where}",
            new { TenantId = tenantId, Status = status, VehicleId = vehicleId, CustomerId = customerId },
            cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<RentalBookingListItemDto>(new CommandDefinition($"""
            {BookingListSelect}
            WHERE {where}
            ORDER BY b.StartAt DESC, b.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """,
            new { TenantId = tenantId, Status = status, VehicleId = vehicleId, CustomerId = customerId, Offset = offset, PageSize = pageSize },
            cancellationToken: cancellationToken))).ToList();

        return new PagedResult<RentalBookingListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<RentalBookingDetailDto?> GetBookingByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<RentalBookingDetailDto>(new CommandDefinition($"""
            {BookingDetailSelect}
            WHERE b.Id = @Id AND b.TenantId = @TenantId AND b.IsDeleted = 0
            """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> CustomerExistsAsync(int customerId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM Customers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            ) THEN 1 ELSE 0 END
            """, new { Id = customerId, TenantId = tenantId }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> VehicleExistsAsync(int vehicleId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            ) THEN 1 ELSE 0 END
            """, new { Id = vehicleId, TenantId = tenantId }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> HasOverlappingBookingAsync(
        int tenantId, int vehicleId, DateTime startAt, DateTime endAt, int? excludeBookingId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM RentalBookings
                WHERE TenantId = @TenantId AND VehicleId = @VehicleId AND IsDeleted = 0
                  AND Status IN @Blocking
                  AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
                  AND StartAt < @EndAt AND EndAt > @StartAt
            ) THEN 1 ELSE 0 END
            """,
            new
            {
                TenantId = tenantId,
                VehicleId = vehicleId,
                StartAt = startAt,
                EndAt = endAt,
                ExcludeId = excludeBookingId,
                Blocking = RentalBookingStatuses.Blocking
            },
            cancellationToken: cancellationToken)) == 1;
    }

    private sealed class VehicleSnap
    {
        public string? Name { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? Model { get; set; }
    }

    public async Task<(string? Name, string? RegistrationNumber, string? Model)?> GetVehicleSnapshotAsync(
        int vehicleId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<VehicleSnap>(
            new CommandDefinition("""
                SELECT Name, RegistrationNumber, Model FROM Vehicles
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
                """, new { Id = vehicleId, TenantId = tenantId }, cancellationToken: cancellationToken));
        return row is null ? null : (row.Name, row.RegistrationNumber, row.Model);
    }

    public async Task<string?> GetCustomerNameAsync(int customerId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            SELECT FullName FROM Customers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = customerId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<int> CreateBookingAsync(
        int tenantId, CreateRentalBookingDto dto, string rentalNumber, decimal totalAmount,
        decimal depositAmount, decimal rateAmount, string? vehicleCategory, string? agreementText,
        int? createdBy, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO RentalBookings (
                TenantId, RentalNumber, CustomerId, VehicleId, VehicleCategory, RateId, RateType, RateAmount,
                StartAt, EndAt, PickupLocation, ReturnLocation, DiscountAmount,
                TotalAmount, DepositAmount, DepositHeld, DepositRefunded, DamageCharges,
                Status, DepositStatus, PaymentStatus, PaidAmount, AgreementText, Notes,
                CreatedAt, CreatedBy, IsDeleted)
            VALUES (
                @TenantId, @RentalNumber, @CustomerId, @VehicleId, @VehicleCategory, @RateId, @RateType, @RateAmount,
                @StartAt, @EndAt, @PickupLocation, @ReturnLocation, @DiscountAmount,
                @TotalAmount, @DepositAmount, 0, 0, 0,
                @Status, @DepositStatus, N'Unpaid', 0, @AgreementText, @Notes,
                SYSUTCDATETIME(), @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new
            {
                TenantId = tenantId,
                RentalNumber = rentalNumber,
                dto.CustomerId,
                dto.VehicleId,
                VehicleCategory = vehicleCategory,
                dto.RateId,
                RateType = dto.RateType.Trim(),
                RateAmount = rateAmount,
                dto.StartAt,
                dto.EndAt,
                dto.PickupLocation,
                dto.ReturnLocation,
                DiscountAmount = dto.DiscountAmount ?? 0,
                TotalAmount = totalAmount,
                DepositAmount = depositAmount,
                Status = RentalBookingStatuses.Draft,
                DepositStatus = RentalDepositStatuses.Pending,
                AgreementText = agreementText,
                dto.Notes,
                CreatedBy = createdBy
            },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateBookingScheduleAsync(
        int id, int tenantId, DateTime startAt, DateTime endAt,
        string? pickupLocation, string? returnLocation, string? notes,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE RentalBookings
            SET StartAt = @StartAt, EndAt = @EndAt,
                PickupLocation = COALESCE(@PickupLocation, PickupLocation),
                ReturnLocation = COALESCE(@ReturnLocation, ReturnLocation),
                Notes = COALESCE(@Notes, Notes), UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
              AND Status IN (N'Draft', N'Confirmed')
            """,
            new
            {
                Id = id,
                TenantId = tenantId,
                StartAt = startAt,
                EndAt = endAt,
                PickupLocation = pickupLocation,
                ReturnLocation = returnLocation,
                Notes = notes
            },
            cancellationToken: cancellationToken));
        return rows > 0;
    }

    public async Task<bool> UpdateBookingStatusAsync(
        int id, int tenantId, string status, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE RentalBookings SET Status = @Status, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = id, TenantId = tenantId, Status = status }, cancellationToken: cancellationToken));
        return rows > 0;
    }

    public async Task<bool> MarkPickedUpAsync(
        int id, int tenantId, DateTime pickedUpAt, int? odometer, string? fuelLevel, string? notes,
        string depositStatus, decimal depositHeld, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE RentalBookings
            SET Status = @Status, PickedUpAt = @PickedUpAt, PickupOdometer = @Odometer,
                PickupFuelLevel = @FuelLevel, PickupNotes = @Notes,
                DepositStatus = @DepositStatus, DepositHeld = @DepositHeld, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Confirmed'
            """,
            new
            {
                Id = id,
                TenantId = tenantId,
                Status = RentalBookingStatuses.PickedUp,
                PickedUpAt = pickedUpAt,
                Odometer = odometer,
                FuelLevel = fuelLevel,
                Notes = notes,
                DepositStatus = depositStatus,
                DepositHeld = depositHeld
            },
            cancellationToken: cancellationToken));
        return rows > 0;
    }

    public async Task<bool> MarkReturnedAsync(
        int id, int tenantId, DateTime returnedAt, int? odometer, string? fuelLevel, string? notes,
        decimal damageCharges, string? damageNotes, decimal depositRefunded, string depositStatus,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE RentalBookings
            SET Status = @Status, ReturnedAt = @ReturnedAt, ReturnOdometer = @Odometer,
                ReturnFuelLevel = @FuelLevel, ReturnNotes = @Notes,
                DamageCharges = @DamageCharges, DamageNotes = @DamageNotes,
                DepositRefunded = @DepositRefunded, DepositStatus = @DepositStatus,
                TotalAmount = TotalAmount + @DamageCharges,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'PickedUp'
            """,
            new
            {
                Id = id,
                TenantId = tenantId,
                Status = RentalBookingStatuses.Returned,
                ReturnedAt = returnedAt,
                Odometer = odometer,
                FuelLevel = fuelLevel,
                Notes = notes,
                DamageCharges = damageCharges,
                DamageNotes = damageNotes,
                DepositRefunded = depositRefunded,
                DepositStatus = depositStatus
            },
            cancellationToken: cancellationToken));
        return rows > 0;
    }

    public async Task<bool> CloseBookingAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE RentalBookings SET Status = @Status, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Returned'
            """,
            new { Id = id, TenantId = tenantId, Status = RentalBookingStatuses.Closed },
            cancellationToken: cancellationToken));
        return rows > 0;
    }

    public async Task<bool> SignAgreementAsync(
        int id, int tenantId, string signedBy, string agreementText, DateTime signedAt,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE RentalBookings
            SET AgreementText = @AgreementText, AgreementSignedBy = @SignedBy,
                AgreementSignedAt = @SignedAt, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
              AND Status NOT IN (N'Cancelled', N'Closed')
            """,
            new { Id = id, TenantId = tenantId, AgreementText = agreementText, SignedBy = signedBy, SignedAt = signedAt },
            cancellationToken: cancellationToken));
        return rows > 0;
    }

    public async Task<int> RecordPaymentAsync(
        int tenantId, int rentalBookingId, RecordRentalPaymentDto dto, int? createdBy,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO RentalPayments (
                TenantId, RentalBookingId, Amount, PaymentMethod, Status, PaymentDate,
                TransactionReference, Notes, CreatedAt, CreatedBy, IsDeleted)
            VALUES (
                @TenantId, @RentalBookingId, @Amount, @PaymentMethod, N'Paid', SYSUTCDATETIME(),
                @TransactionReference, @Notes, SYSUTCDATETIME(), @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new
            {
                TenantId = tenantId,
                RentalBookingId = rentalBookingId,
                dto.Amount,
                PaymentMethod = dto.PaymentMethod.Trim(),
                dto.TransactionReference,
                dto.Notes,
                CreatedBy = createdBy
            },
            cancellationToken: cancellationToken));
    }

    public async Task UpdateBookingPaymentTotalsAsync(
        int rentalBookingId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE b
            SET b.PaidAmount = ISNULL(p.TotalPaid, 0),
                b.PaymentMethod = p.LastMethod,
                b.PaymentReference = p.LastRef,
                b.PaymentStatus = CASE
                    WHEN ISNULL(p.TotalPaid, 0) <= 0 THEN N'Unpaid'
                    WHEN ISNULL(p.TotalPaid, 0) >= (b.TotalAmount + b.DepositAmount) THEN N'Paid'
                    ELSE N'Partial'
                END,
                b.UpdatedAt = SYSUTCDATETIME()
            FROM RentalBookings b
            OUTER APPLY (
                SELECT SUM(Amount) AS TotalPaid,
                       MAX(PaymentMethod) AS LastMethod,
                       MAX(TransactionReference) AS LastRef
                FROM RentalPayments
                WHERE RentalBookingId = b.Id AND TenantId = b.TenantId AND IsDeleted = 0 AND Status = N'Paid'
            ) p
            WHERE b.Id = @Id AND b.TenantId = @TenantId AND b.IsDeleted = 0
            """, new { Id = rentalBookingId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<RentalPaymentDto>> GetPaymentsAsync(
        int rentalBookingId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<RentalPaymentDto>(new CommandDefinition("""
            SELECT Id, RentalBookingId, Amount, PaymentMethod, Status, PaymentDate, TransactionReference, Notes
            FROM RentalPayments
            WHERE RentalBookingId = @RentalBookingId AND TenantId = @TenantId AND IsDeleted = 0
            ORDER BY PaymentDate DESC, Id DESC
            """, new { RentalBookingId = rentalBookingId, TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<RentalAvailabilityDto> CheckAvailabilityAsync(
        int tenantId, int vehicleId, DateTime from, DateTime to,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var vehicle = await connection.QuerySingleOrDefaultAsync<(string? Name, string? RegistrationNumber)>(new CommandDefinition("""
            SELECT Name, RegistrationNumber FROM Vehicles
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = vehicleId, TenantId = tenantId }, cancellationToken: cancellationToken));

        var conflicts = (await connection.QueryAsync<RentalAvailabilitySlotDto>(new CommandDefinition("""
            SELECT b.Id AS RentalBookingId, b.RentalNumber, b.Status, b.StartAt, b.EndAt, c.FullName AS CustomerName
            FROM RentalBookings b
            LEFT JOIN Customers c ON c.Id = b.CustomerId AND c.TenantId = b.TenantId AND c.IsDeleted = 0
            WHERE b.TenantId = @TenantId AND b.VehicleId = @VehicleId AND b.IsDeleted = 0
              AND b.Status IN @Blocking
              AND b.StartAt < @To AND b.EndAt > @From
            ORDER BY b.StartAt
            """,
            new { TenantId = tenantId, VehicleId = vehicleId, From = from, To = to, Blocking = RentalBookingStatuses.Blocking },
            cancellationToken: cancellationToken))).ToList();

        return new RentalAvailabilityDto(
            vehicleId,
            vehicle.Name,
            vehicle.RegistrationNumber,
            from,
            to,
            conflicts.Count == 0,
            conflicts);
    }

    public async Task<string> NextRentalNumberAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var seq = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT ISNULL(MAX(Id), 0) + 1 FROM RentalBookings WHERE TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return $"RNT-{tenantId}-{seq:D5}";
    }
}
