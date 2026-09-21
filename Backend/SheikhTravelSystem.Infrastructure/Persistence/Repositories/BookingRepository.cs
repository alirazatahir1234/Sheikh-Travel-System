using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Infrastructure.Persistence;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.Drivers;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine) : IBookingRepository
{
    public async Task<CreateBookingResult> CreateAsync(
        CreateBookingDto dto,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var customerExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Customers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = dto.CustomerId },
                cancellationToken: cancellationToken));

        if (!customerExists)
            throw new NotFoundException("Customer", dto.CustomerId);

        var routeName = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                "SELECT Source + ' → ' + Destination FROM Routes WHERE Id = @Id AND IsDeleted = 0",
                new { Id = dto.RouteId },
                cancellationToken: cancellationToken));

        if (routeName == null)
            throw new NotFoundException("Route", dto.RouteId);

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO Bookings (CustomerId, RouteId, PickupTime, PassengerCount, TotalAmount, Status, Notes, CreatedAt, IsDeleted)
                  VALUES (@CustomerId, @RouteId, @PickupTime, @PassengerCount, @TotalAmount, @Status, @Notes, @CreatedAt, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    dto.CustomerId,
                    dto.RouteId,
                    dto.PickupTime,
                    dto.PassengerCount,
                    dto.TotalAmount,
                    Status = (int)BookingStatus.Pending,
                    dto.Notes,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        var bookingNumber = $"BK-{DateTime.UtcNow.Year}-{id:D4}";
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Bookings SET BookingNumber = @BookingNumber WHERE Id = @Id",
                new { BookingNumber = bookingNumber, Id = id },
                cancellationToken: cancellationToken));

        return new CreateBookingResult(id, bookingNumber, routeName);
    }

    public async Task<BookingMutationResult> UpdateAsync(
        int id,
        UpdateBookingDto dto,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var currentStatus = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT Status FROM Bookings WHERE Id = @Id",
                new { Id = id },
                cancellationToken: cancellationToken));

        if (currentStatus is null)
            throw new NotFoundException("Booking", id);

        var status = (BookingStatus)currentStatus.Value;
        if (status == BookingStatus.Completed || status == BookingStatus.Cancelled)
            return BookingMutationResult.Fail($"Cannot edit a {status} booking.");

        var customerExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Customers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = dto.CustomerId },
                cancellationToken: cancellationToken));

        if (!customerExists)
            throw new NotFoundException("Customer", dto.CustomerId);

        var routeExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Routes WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = dto.RouteId },
                cancellationToken: cancellationToken));

        if (!routeExists)
            throw new NotFoundException("Route", dto.RouteId);

        var normalizedVehicleId = dto.VehicleId;
        if (dto.VehicleId.HasValue)
        {
            var vehicleExists = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                    new { Id = dto.VehicleId },
                    cancellationToken: cancellationToken));

            // Keep edit flow resilient when a previously assigned vehicle was archived.
            if (!vehicleExists)
                normalizedVehicleId = null;
        }

        var normalizedDriverId = dto.DriverId;
        if (dto.DriverId.HasValue)
        {
            var driverExists = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                    new { Id = dto.DriverId },
                    cancellationToken: cancellationToken));

            // Keep edit flow resilient when a previously assigned driver was archived.
            if (!driverExists)
                normalizedDriverId = null;
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Bookings SET
                    CustomerId = @CustomerId,
                    RouteId = @RouteId,
                    PickupTime = @PickupTime,
                    PassengerCount = @PassengerCount,
                    TotalAmount = @TotalAmount,
                    VehicleId = @VehicleId,
                    DriverId = @DriverId,
                    Notes = @Notes,
                    IsDeleted = 0,
                    UpdatedAt = @UpdatedAt
                  WHERE Id = @Id",
                new
                {
                    Id = id,
                    dto.CustomerId,
                    dto.RouteId,
                    dto.PickupTime,
                    dto.PassengerCount,
                    dto.TotalAmount,
                    VehicleId = normalizedVehicleId,
                    DriverId = normalizedDriverId,
                    dto.Notes,
                    UpdatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        return BookingMutationResult.Ok();
    }

    public async Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Bookings SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id AND IsDeleted = 0",
                new { Id = id, UpdatedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));

        if (rowsAffected == 0)
            throw new NotFoundException("Booking", id);
    }

    public async Task<int> SoftDeleteManyAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        return await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Bookings SET IsDeleted = 1, UpdatedAt = @UpdatedAt
                  WHERE IsDeleted = 0 AND Id IN @Ids",
                new { Ids = ids, UpdatedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));
    }

    public async Task<BookingMutationResult> AssignDriverAsync(
        int bookingId,
        int driverId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // Verify booking exists and is in valid state
        var bookingStatus = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT Status FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
                new { Id = bookingId },
                cancellationToken: cancellationToken));

        if (bookingStatus is null)
            throw new NotFoundException("Booking", bookingId);

        if (bookingStatus != (int)BookingStatus.Pending && bookingStatus != (int)BookingStatus.Confirmed)
            return BookingMutationResult.Fail("Can only assign driver to pending or confirmed bookings.");

        // Verify driver exists and is available
        var driverStatus = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT Status FROM Drivers WHERE Id = @Id AND IsDeleted = 0 AND IsActive = 1",
                new { Id = driverId },
                cancellationToken: cancellationToken));

        if (driverStatus is null)
            throw new NotFoundException("Driver", driverId);

        if (driverStatus != (int)DriverStatus.Available)
            return BookingMutationResult.Fail("Driver is not available.");

        // Check for double booking - driver already assigned to active trip at same time
        var booking = await connection.QuerySingleAsync<(DateTime PickupTime, DateTime? DropoffTime)>(
            new CommandDefinition(
                "SELECT PickupTime, DropoffTime FROM Bookings WHERE Id = @Id",
                new { Id = bookingId },
                cancellationToken: cancellationToken));

        var driverConflict = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Bookings
                    WHERE DriverId = @DriverId AND IsDeleted = 0
                    AND Status IN (@Confirmed, @Started)
                    AND Id != @BookingId
                    AND PickupTime < DATEADD(HOUR, 4, @PickupTime)
                    AND DATEADD(HOUR, 4, PickupTime) > @PickupTime
                  ) THEN 1 ELSE 0 END",
                new
                {
                    DriverId = driverId,
                    BookingId = bookingId,
                    Confirmed = (int)BookingStatus.Confirmed,
                    Started = (int)BookingStatus.Started,
                    booking.PickupTime
                },
                cancellationToken: cancellationToken));

        if (driverConflict)
            return BookingMutationResult.Fail("Driver has a conflicting booking at this time.");

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Bookings SET DriverId = @DriverId, UpdatedAt = @Now WHERE Id = @Id",
                new { DriverId = driverId, Now = DateTime.UtcNow, Id = bookingId },
                cancellationToken: cancellationToken));

        return BookingMutationResult.Ok();
    }

    public async Task<BookingMutationResult> AssignVehicleAsync(
        int bookingId,
        int vehicleId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var bookingStatus = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT Status FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
                new { Id = bookingId },
                cancellationToken: cancellationToken));

        if (bookingStatus is null)
            throw new NotFoundException("Booking", bookingId);

        if (bookingStatus != (int)BookingStatus.Pending && bookingStatus != (int)BookingStatus.Confirmed)
            return BookingMutationResult.Fail("Can only assign vehicle to pending or confirmed bookings.");

        var vehicleStatus = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT Status FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                new { Id = vehicleId },
                cancellationToken: cancellationToken));

        if (vehicleStatus is null)
            throw new NotFoundException("Vehicle", vehicleId);

        if (vehicleStatus != (int)VehicleStatus.Available)
            return BookingMutationResult.Fail("Vehicle is not available.");

        // Check for double booking
        var booking = await connection.QuerySingleAsync<(DateTime PickupTime, DateTime? DropoffTime)>(
            new CommandDefinition(
                "SELECT PickupTime, DropoffTime FROM Bookings WHERE Id = @Id",
                new { Id = bookingId },
                cancellationToken: cancellationToken));

        var vehicleConflict = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Bookings
                    WHERE VehicleId = @VehicleId AND IsDeleted = 0
                    AND Status IN (@Confirmed, @Started)
                    AND Id != @BookingId
                    AND PickupTime < DATEADD(HOUR, 4, @PickupTime)
                    AND DATEADD(HOUR, 4, PickupTime) > @PickupTime
                  ) THEN 1 ELSE 0 END",
                new
                {
                    VehicleId = vehicleId,
                    BookingId = bookingId,
                    Confirmed = (int)BookingStatus.Confirmed,
                    Started = (int)BookingStatus.Started,
                    booking.PickupTime
                },
                cancellationToken: cancellationToken));

        if (vehicleConflict)
            return BookingMutationResult.Fail("Vehicle has a conflicting booking at this time.");

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Bookings SET VehicleId = @VehicleId, UpdatedAt = @Now WHERE Id = @Id",
                new { VehicleId = vehicleId, Now = DateTime.UtcNow, Id = bookingId },
                cancellationToken: cancellationToken));

        return BookingMutationResult.Ok();
    }

    public async Task<UpdateBookingStatusResult> UpdateStatusAsync(
        int id,
        BookingStatus status,
        string? cancellationReason,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        DriverAssignmentOps.OpenConnection(connection);
        using var transaction = connection.BeginTransaction();
        var tenantId = tenantContext.GetRequiredTenantId();

        try
        {
            var currentStatus = await connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    "SELECT Status FROM Bookings WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                    new { Id = id, TenantId = tenantId },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            if (currentStatus is null)
                throw new NotFoundException("Booking", id);

            var current = (BookingStatus)currentStatus.Value;

            var valid = (current, status) switch
            {
                (BookingStatus.Pending, BookingStatus.Confirmed) => true,
                (BookingStatus.Pending, BookingStatus.Cancelled) => true,
                (BookingStatus.Confirmed, BookingStatus.Started) => true,
                (BookingStatus.Confirmed, BookingStatus.Cancelled) => true,
                (BookingStatus.Started, BookingStatus.Completed) => true,
                (BookingStatus.Started, BookingStatus.Cancelled) => true,
                _ => false
            };

            if (!valid)
                return UpdateBookingStatusResult.Fail($"Cannot transition from {current} to {status}.");

            if (status == BookingStatus.Cancelled && string.IsNullOrWhiteSpace(cancellationReason))
                return UpdateBookingStatusResult.Fail("Cancellation reason is required.");

            var booking = await connection.QuerySingleOrDefaultAsync<(int? DriverId, int? VehicleId)>(
                new CommandDefinition(
                    "SELECT DriverId, VehicleId FROM Bookings WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                    new { Id = id, TenantId = tenantId },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"UPDATE Bookings SET Status = @Status, UpdatedAt = @UpdatedAt,
                      CancellationReason = @CancellationReason,
                      DropoffTime = CASE WHEN @Status = @CompletedStatus THEN @Now ELSE DropoffTime END
                      WHERE Id = @Id AND TenantId = @TenantId",
                    new
                    {
                        Status = (int)status,
                        UpdatedAt = DateTime.UtcNow,
                        CancellationReason = cancellationReason,
                        CompletedStatus = (int)BookingStatus.Completed,
                        Now = DateTime.UtcNow,
                        Id = id,
                        TenantId = tenantId
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            if (booking.DriverId is int driverId)
            {
                var driverStatus = status switch
                {
                    BookingStatus.Started => DriverStatus.OnTrip,
                    BookingStatus.Completed or BookingStatus.Cancelled => DriverStatus.Available,
                    _ => (DriverStatus?)null
                };

                if (driverStatus.HasValue)
                {
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            @"UPDATE Drivers SET Status = @Status, UpdatedAt = GETUTCDATE()
                              WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                            new { Status = (int)driverStatus.Value, Id = driverId, TenantId = tenantId },
                            transaction: transaction,
                            cancellationToken: cancellationToken));
                }
            }

            if (booking.VehicleId is int vehicleId)
            {
                var vehicleStatus = status switch
                {
                    BookingStatus.Started => VehicleStatus.OnTrip,
                    BookingStatus.Completed or BookingStatus.Cancelled => VehicleStatus.Available,
                    _ => (VehicleStatus?)null
                };

                if (vehicleStatus.HasValue)
                {
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            @"UPDATE Vehicles SET Status = @Status, UpdatedAt = GETUTCDATE()
                              WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                            new { Status = (int)vehicleStatus.Value, Id = vehicleId, TenantId = tenantId },
                            transaction: transaction,
                            cancellationToken: cancellationToken));
                }
            }

            transaction.Commit();
            return UpdateBookingStatusResult.Ok(current, $"Booking status updated to {status}.");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<PagedResult<BookingDto>> GetPagedAsync(
        int page,
        int pageSize,
        BookingStatus? status,
        string? search,
        DateTime? dateFrom,
        DateTime? dateTo,
        decimal? amountMin,
        decimal? amountMax,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;
        var tenantId = tenantContext.GetRequiredTenantId();

        var clauses = new List<string> { "b.IsDeleted = 0", "b.TenantId = @TenantId" };
        if (status.HasValue)
            clauses.Add("b.Status = @Status");
        if (!string.IsNullOrWhiteSpace(search))
            clauses.Add("(b.BookingNumber LIKE @SearchPattern OR c.FullName LIKE @SearchPattern OR r.Source LIKE @SearchPattern OR r.Destination LIKE @SearchPattern)");
        if (dateFrom.HasValue)
            clauses.Add("b.PickupTime >= @DateFrom");
        if (dateTo.HasValue)
            clauses.Add("b.PickupTime < @DateTo");
        if (amountMin.HasValue)
            clauses.Add("b.TotalAmount >= @AmountMin");
        if (amountMax.HasValue)
            clauses.Add("b.TotalAmount <= @AmountMax");

        var parameters = new DynamicParameters(new
        {
            Offset = offset,
            PageSize = pageSize,
            TenantId = tenantId,
            Status = (int?)status,
            SearchPattern = $"%{search}%",
            DateFrom = dateFrom,
            DateTo = dateTo.HasValue ? (DateTime?)dateTo.Value.Date.AddDays(1) : null,
            AmountMin = amountMin,
            AmountMax = amountMax
        });

        if (currentUser.UserId is int userId)
        {
            var scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);
            DataScopeSqlBuilder.ApplyLinkedFleetScope(parameters, scope, clauses, "v", "d");
        }

        var whereClause = "WHERE " + string.Join(" AND ", clauses);

        var bookings = await connection.QueryAsync<BookingDto>(
            new CommandDefinition(
                $@"SELECT b.Id, b.BookingNumber, b.CustomerId, c.FullName AS CustomerName, b.RouteId,
                  r.Source + ' -> ' + r.Destination AS RouteName,
                  b.VehicleId, v.Name AS VehicleName, b.DriverId, d.FullName AS DriverName,
                  b.PickupTime, b.DropoffTime, b.PassengerCount, b.TotalAmount, b.Status, b.Notes, b.CreatedAt
                  FROM Bookings b
                  LEFT JOIN Customers c ON b.CustomerId = c.Id
                  LEFT JOIN Routes r ON b.RouteId = r.Id
                  LEFT JOIN Vehicles v ON b.VehicleId = v.Id
                  LEFT JOIN Drivers d ON b.DriverId = d.Id
                  {whereClause}
                  ORDER BY b.CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                parameters,
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $@"SELECT COUNT(*) FROM Bookings b
                   LEFT JOIN Customers c ON b.CustomerId = c.Id
                   LEFT JOIN Routes r ON b.RouteId = r.Id
                   LEFT JOIN Vehicles v ON b.VehicleId = v.Id
                   LEFT JOIN Drivers d ON b.DriverId = d.Id
                   {whereClause}",
                parameters,
                cancellationToken: cancellationToken));

        return new PagedResult<BookingDto>
        {
            Items = bookings.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<BookingDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var booking = await connection.QuerySingleOrDefaultAsync<BookingDto>(
            new CommandDefinition(
                @"SELECT b.Id, b.BookingNumber, b.CustomerId, c.FullName AS CustomerName, b.RouteId,
                  r.Source + ' -> ' + r.Destination AS RouteName,
                  b.VehicleId, v.Name AS VehicleName, b.DriverId, d.FullName AS DriverName,
                  b.PickupTime, b.DropoffTime, b.PassengerCount, b.TotalAmount, b.Status, b.Notes, b.CreatedAt
                  FROM Bookings b
                  LEFT JOIN Customers c ON b.CustomerId = c.Id
                  LEFT JOIN Routes r ON b.RouteId = r.Id
                  LEFT JOIN Vehicles v ON b.VehicleId = v.Id
                  LEFT JOIN Drivers d ON b.DriverId = d.Id
                  WHERE b.Id = @Id AND b.IsDeleted = 0 AND b.TenantId = @TenantId",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (booking is null)
            throw new NotFoundException("Booking", id);

        return booking;
    }
}
