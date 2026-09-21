using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Infrastructure.Persistence;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverApp;
using SheikhTravelSystem.Application.Features.Drivers;
using SheikhTravelSystem.Application.Features.Trips;
using SheikhTravelSystem.Application.Features.Trips.DTOs;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;
using System.Globalization;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class TripRepository(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine) : ITripRepository
{
    public async Task<int> CreateAsync(CreateTripDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        DriverAssignmentOps.OpenConnection(connection);
        using var tx = connection.BeginTransaction();
        var tenantId = tenantContext.GetRequiredTenantId();
        var actor = currentUser.UserId?.ToString();

        try
        {
            var customerOk = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Customers WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = dto.CustomerId }, transaction: tx, cancellationToken: cancellationToken));
            if (!customerOk)
                throw new NotFoundException("Customer", dto.CustomerId);

            if (dto.RouteId is int routeId)
            {
                var routeOk = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                    "SELECT CASE WHEN EXISTS(SELECT 1 FROM Routes WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                    new { Id = routeId }, transaction: tx, cancellationToken: cancellationToken));
                if (!routeOk)
                    throw new NotFoundException("Route", routeId);
            }

            if (dto.BookingId is int bookingId)
            {
                var bookingOk = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                    "SELECT CASE WHEN EXISTS(SELECT 1 FROM Bookings WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                    new { Id = bookingId, TenantId = tenantId }, transaction: tx, cancellationToken: cancellationToken));
                if (!bookingOk)
                    throw new NotFoundException("Booking", bookingId);
            }

            var initialStatus = dto.DriverId.HasValue || dto.VehicleId.HasValue
                ? TripLifecycle.ResolveAssignmentStatus(TripStatus.Scheduled, dto.DriverId.HasValue, dto.VehicleId.HasValue)
                : TripStatus.Scheduled;

            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO Trips (
                    TenantId, TripNumber, BookingId, CustomerId, RouteId, TripName, TripType,
                    PickupAddress, PickupLatitude, PickupLongitude,
                    DestinationAddress, DestinationLatitude, DestinationLongitude,
                    TripDate, PlannedStart, PlannedEnd, EstimatedDurationMinutes,
                    DriverId, AssistantDriverId, VehicleId, PassengerCount, Priority, Status,
                    DriverNotes, PlannedDistanceKm, CreatedAt, CreatedBy, IsDeleted)
                VALUES (
                    @TenantId, '', @BookingId, @CustomerId, @RouteId, @TripName, @TripType,
                    @PickupAddress, @PickupLatitude, @PickupLongitude,
                    @DestinationAddress, @DestinationLatitude, @DestinationLongitude,
                    @TripDate, @PlannedStart, @PlannedEnd, @EstimatedDurationMinutes,
                    @DriverId, @AssistantDriverId, @VehicleId, @PassengerCount, @Priority, @Status,
                    @DriverNotes, @PlannedDistanceKm, GETUTCDATE(), @CreatedBy, 0);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """,
                new
                {
                    TenantId = tenantId,
                    dto.BookingId,
                    dto.CustomerId,
                    dto.RouteId,
                    dto.TripName,
                    TripType = (int)dto.TripType,
                    dto.PickupAddress,
                    dto.PickupLatitude,
                    dto.PickupLongitude,
                    dto.DestinationAddress,
                    dto.DestinationLatitude,
                    dto.DestinationLongitude,
                    TripDate = dto.TripDate.Date,
                    dto.PlannedStart,
                    dto.PlannedEnd,
                    dto.EstimatedDurationMinutes,
                    dto.DriverId,
                    dto.AssistantDriverId,
                    dto.VehicleId,
                    dto.PassengerCount,
                    Priority = (int)dto.Priority,
                    Status = (int)initialStatus,
                    dto.DriverNotes,
                    dto.PlannedDistanceKm,
                    CreatedBy = actor
                },
                transaction: tx,
                cancellationToken: cancellationToken));

            var tripNumber = $"TR-{DateTime.UtcNow.Year}-{id:D4}";
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE Trips SET TripNumber = @TripNumber WHERE Id = @Id",
                new { TripNumber = tripNumber, Id = id },
                transaction: tx,
                cancellationToken: cancellationToken));

            await TripLifecycleSql.RecordStatusAsync(connection, tx, id, null, initialStatus, actor, "Trip created", cancellationToken);

            if (dto.Stops is { Count: > 0 })
            {
                await TripLifecycleSql.ReplaceStopsAsync(
                    connection, tx, id,
                    dto.Stops.Select(s => (s.Sequence, s.Location, s.Latitude, s.Longitude, s.Eta)).ToList(),
                    cancellationToken);
            }

            if (dto.DriverId is int driverId && dto.VehicleId is int vehicleId)
            {
                await TripLifecycleSql.EnsureAssignmentHistoryAsync(
                    connection, tx, tenantId, id, dto.BookingId, driverId, vehicleId,
                    dto.PickupAddress, dto.DestinationAddress, actor, cancellationToken);
            }

            tx.Commit();
            return id;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<TripMutationResult> UpdateAsync(int id, UpdateTripDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        DriverAssignmentOps.OpenConnection(connection);
        using var tx = connection.BeginTransaction();
        var tenantId = tenantContext.GetRequiredTenantId();

        try
        {
            var existing = await connection.QuerySingleOrDefaultAsync<(int Status, string TripNumber)>(new CommandDefinition(
                "SELECT Status, TripNumber FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = id, TenantId = tenantId }, transaction: tx, cancellationToken: cancellationToken));

            if (existing.TripNumber is null)
                throw new NotFoundException("Trip", id);

            var current = (TripStatus)existing.Status;
            if (TripLifecycle.IsTerminal(current) || current is TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute)
            {
                tx.Rollback();
                return TripMutationResult.Fail("Cannot edit a trip that is in progress or completed.");
            }

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Trips SET
                    TripName = @TripName, TripType = @TripType, CustomerId = @CustomerId, RouteId = @RouteId,
                    PassengerCount = @PassengerCount, Priority = @Priority,
                    PickupAddress = @PickupAddress, PickupLatitude = @PickupLatitude, PickupLongitude = @PickupLongitude,
                    DestinationAddress = @DestinationAddress, DestinationLatitude = @DestinationLatitude, DestinationLongitude = @DestinationLongitude,
                    TripDate = @TripDate, PlannedStart = @PlannedStart, PlannedEnd = @PlannedEnd,
                    EstimatedDurationMinutes = @EstimatedDurationMinutes, PlannedDistanceKm = @PlannedDistanceKm,
                    DriverNotes = @DriverNotes, UpdatedAt = GETUTCDATE(), UpdatedBy = @UpdatedBy
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
                """,
                new
                {
                    Id = id,
                    TenantId = tenantId,
                    dto.TripName,
                    TripType = (int)dto.TripType,
                    dto.CustomerId,
                    dto.RouteId,
                    dto.PassengerCount,
                    Priority = (int)dto.Priority,
                    dto.PickupAddress,
                    dto.PickupLatitude,
                    dto.PickupLongitude,
                    dto.DestinationAddress,
                    dto.DestinationLatitude,
                    dto.DestinationLongitude,
                    TripDate = dto.TripDate.Date,
                    dto.PlannedStart,
                    dto.PlannedEnd,
                    dto.EstimatedDurationMinutes,
                    dto.PlannedDistanceKm,
                    dto.DriverNotes,
                    UpdatedBy = currentUser.UserId?.ToString()
                },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (dto.Stops is not null)
            {
                await TripLifecycleSql.ReplaceStopsAsync(
                    connection, tx, id,
                    dto.Stops.Select(s => (s.Sequence, s.Location, s.Latitude, s.Longitude, s.Eta)).ToList(),
                    cancellationToken);
            }

            tx.Commit();
            return TripMutationResult.Ok(existing.TripNumber);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<TripMutationResult> SoftDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var status = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Status FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (status is null)
            throw new NotFoundException("Trip", id);

        if ((TripStatus)status.Value is TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute or TripStatus.Delayed)
            return TripMutationResult.Fail("Cannot delete an active trip. Cancel it first.");

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Trips SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        return TripMutationResult.Ok();
    }

    public async Task<TripStatusUpdateResult> UpdateStatusAsync(
        int id, TripStatus status, string? note, string? cancellationReason, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        DriverAssignmentOps.OpenConnection(connection);
        using var tx = connection.BeginTransaction();
        var tenantId = tenantContext.GetRequiredTenantId();
        var actor = currentUser.UserId?.ToString();

        try
        {
            var trip = await connection.QuerySingleOrDefaultAsync<TripStatusRow>(new CommandDefinition(
                @"SELECT Status, DriverId, VehicleId, BookingId
                  FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = id, TenantId = tenantId },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (trip is null)
                throw new NotFoundException("Trip", id);

            var current = (TripStatus)trip.Status;
            if (!TripLifecycle.CanTransition(current, status))
            {
                tx.Rollback();
                return TripStatusUpdateResult.Fail($"Cannot transition from {current} to {status}.");
            }

            if (status == TripStatus.Cancelled && string.IsNullOrWhiteSpace(cancellationReason))
            {
                tx.Rollback();
                return TripStatusUpdateResult.Fail("Cancellation reason is required.");
            }

            if (status == TripStatus.Started && trip.DriverId is null)
            {
                tx.Rollback();
                return TripStatusUpdateResult.Fail("Assign a driver before starting the trip.");
            }

            if (status == TripStatus.Started && trip.VehicleId is null
                && note?.StartsWith("Driver:", StringComparison.Ordinal) != true)
            {
                tx.Rollback();
                return TripStatusUpdateResult.Fail("Assign both driver and vehicle before starting the trip.");
            }

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Trips SET
                    Status = @Status,
                    UpdatedAt = GETUTCDATE(),
                    UpdatedBy = @UpdatedBy,
                    CancellationReason = CASE WHEN @Status = @Cancelled THEN @CancellationReason ELSE CancellationReason END,
                    ActualStart = CASE WHEN @Status = @Started AND ActualStart IS NULL THEN GETUTCDATE() ELSE ActualStart END,
                    ActualEnd = CASE WHEN @Status IN (@Completed, @Cancelled, @Failed) THEN GETUTCDATE() ELSE ActualEnd END
                WHERE Id = @Id AND TenantId = @TenantId
                """,
                new
                {
                    Status = (int)status,
                    UpdatedBy = actor,
                    Cancelled = (int)TripStatus.Cancelled,
                    Started = (int)TripStatus.Started,
                    Completed = (int)TripStatus.Completed,
                    Failed = (int)TripStatus.Failed,
                    CancellationReason = cancellationReason,
                    Id = id,
                    TenantId = tenantId
                },
                transaction: tx,
                cancellationToken: cancellationToken));

            await TripLifecycleSql.RecordStatusAsync(
                connection, tx, id, current, status, actor,
                note ?? cancellationReason, cancellationToken);

            await TripLifecycleSql.SyncResourceStatusAsync(
                connection, tx, tenantId, trip.DriverId, trip.VehicleId, status, cancellationToken);

            if (status is TripStatus.Completed or TripStatus.Cancelled or TripStatus.Failed)
            {
                await TripLifecycleSql.CloseAssignmentHistoryAsync(
                    connection, tx, tenantId, id, trip.BookingId, cancellationToken);
            }

            var tripNumber = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
                "SELECT TripNumber FROM Trips WHERE Id = @Id",
                new { Id = id },
                transaction: tx,
                cancellationToken: cancellationToken));

            tx.Commit();
            return TripStatusUpdateResult.Ok(tripNumber, current, status);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<TripMutationResult> AssignDriverAsync(
        int tripId, int driverId, int? assistantDriverId, string? driverNotes, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        DriverAssignmentOps.OpenConnection(connection);
        using var tx = connection.BeginTransaction();
        var tenantId = tenantContext.GetRequiredTenantId();
        var actor = currentUser.UserId?.ToString();

        try
        {
            var trip = await connection.QuerySingleOrDefaultAsync<TripAssignRow>(new CommandDefinition(
                @"SELECT Status, VehicleId, DriverId, BookingId, PickupAddress AS Pickup, DestinationAddress AS Dest, PlannedStart
                  FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = tripId, TenantId = tenantId },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (trip is null)
                throw new NotFoundException("Trip", tripId);

            var current = (TripStatus)trip.Status;
            if (TripLifecycle.IsTerminal(current) || current is TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute)
            {
                tx.Rollback();
                return TripMutationResult.Fail("Cannot reassign driver on an active or completed trip.");
            }

            var driverStatus = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT Status FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1",
                new { Id = driverId, TenantId = tenantId },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (driverStatus is null)
                throw new NotFoundException("Driver", driverId);

            if (driverStatus != (int)DriverStatus.Available && driverStatus != (int)DriverStatus.OnTrip)
            {
                tx.Rollback();
                return TripMutationResult.Fail("Driver is not available.");
            }

            var conflict = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Trips
                    WHERE DriverId = @DriverId AND IsDeleted = 0 AND TenantId = @TenantId AND Id != @TripId
                      AND Status IN (@Scheduled, @DriverAssigned, @VehicleAssigned, @Started, @AtPickup, @Enroute, @Delayed)
                      AND PlannedStart < DATEADD(HOUR, 4, @PlannedStart)
                      AND DATEADD(HOUR, 4, PlannedStart) > @PlannedStart
                ) THEN 1 ELSE 0 END
                """,
                new
                {
                    DriverId = driverId,
                    TripId = tripId,
                    TenantId = tenantId,
                    trip.PlannedStart,
                    Scheduled = (int)TripStatus.Scheduled,
                    DriverAssigned = (int)TripStatus.DriverAssigned,
                    VehicleAssigned = (int)TripStatus.VehicleAssigned,
                    Started = (int)TripStatus.Started,
                    AtPickup = (int)TripStatus.AtPickup,
                    Enroute = (int)TripStatus.Enroute,
                    Delayed = (int)TripStatus.Delayed
                },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (conflict)
            {
                tx.Rollback();
                return TripMutationResult.Fail("Driver has a conflicting trip at this time.");
            }

            var nextStatus = TripLifecycle.ResolveAssignmentStatus(current, hasDriver: true, hasVehicle: trip.VehicleId.HasValue);

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Trips SET DriverId = @DriverId, AssistantDriverId = @AssistantDriverId,
                    DriverNotes = COALESCE(@DriverNotes, DriverNotes),
                    Status = @Status, UpdatedAt = GETUTCDATE(), UpdatedBy = @UpdatedBy
                WHERE Id = @Id AND TenantId = @TenantId
                """,
                new
                {
                    DriverId = driverId,
                    AssistantDriverId = assistantDriverId,
                    DriverNotes = driverNotes,
                    Status = (int)nextStatus,
                    UpdatedBy = actor,
                    Id = tripId,
                    TenantId = tenantId
                },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (nextStatus != current)
                await TripLifecycleSql.RecordStatusAsync(connection, tx, tripId, current, nextStatus, actor, "Driver assigned", cancellationToken);

            if (trip.VehicleId is int vehicleId)
            {
                await TripLifecycleSql.EnsureAssignmentHistoryAsync(
                    connection, tx, tenantId, tripId, trip.BookingId,
                    driverId, vehicleId, trip.Pickup, trip.Dest, actor, cancellationToken);
            }

            var tripNumber = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
                "SELECT TripNumber FROM Trips WHERE Id = @Id",
                new { Id = tripId },
                transaction: tx,
                cancellationToken: cancellationToken));

            tx.Commit();
            return TripMutationResult.Ok(tripNumber);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<TripMutationResult> AssignVehicleAsync(int tripId, int vehicleId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        DriverAssignmentOps.OpenConnection(connection);
        using var tx = connection.BeginTransaction();
        var tenantId = tenantContext.GetRequiredTenantId();
        var actor = currentUser.UserId?.ToString();

        try
        {
            var trip = await connection.QuerySingleOrDefaultAsync<TripAssignRow>(new CommandDefinition(
                @"SELECT Status, DriverId, BookingId, PickupAddress AS Pickup, DestinationAddress AS Dest, PlannedStart
                  FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = tripId, TenantId = tenantId },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (trip is null)
                throw new NotFoundException("Trip", tripId);

            var current = (TripStatus)trip.Status;
            if (TripLifecycle.IsTerminal(current) || current is TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute)
            {
                tx.Rollback();
                return TripMutationResult.Fail("Cannot reassign vehicle on an active or completed trip.");
            }

            var vehicleStatus = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT Status FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = vehicleId, TenantId = tenantId },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (vehicleStatus is null)
                throw new NotFoundException("Vehicle", vehicleId);

            if (vehicleStatus == (int)VehicleStatus.Maintenance)
            {
                tx.Rollback();
                return TripMutationResult.Fail("Vehicle is under maintenance.");
            }

            if (vehicleStatus != (int)VehicleStatus.Available && vehicleStatus != (int)VehicleStatus.OnTrip)
            {
                tx.Rollback();
                return TripMutationResult.Fail("Vehicle is not available.");
            }

            var otherOpen = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM AssignmentHistory
                    WHERE VehicleId = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0
                      AND Status IN (N'Active', N'Scheduled')
                      AND (@DriverId IS NULL OR DriverId IS NULL OR DriverId <> @DriverId)
                ) THEN 1 ELSE 0 END
                """,
                new { VehicleId = vehicleId, TenantId = tenantId, DriverId = trip.DriverId },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (otherOpen)
            {
                tx.Rollback();
                return TripMutationResult.Fail("Vehicle is already assigned to another driver.");
            }

            var tripConflict = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Trips
                    WHERE VehicleId = @VehicleId AND IsDeleted = 0 AND TenantId = @TenantId AND Id != @TripId
                      AND Status IN (@Scheduled, @DriverAssigned, @VehicleAssigned, @Started, @AtPickup, @Enroute, @Delayed)
                      AND PlannedStart < DATEADD(HOUR, 4, @PlannedStart)
                      AND DATEADD(HOUR, 4, PlannedStart) > @PlannedStart
                ) THEN 1 ELSE 0 END
                """,
                new
                {
                    VehicleId = vehicleId,
                    TripId = tripId,
                    TenantId = tenantId,
                    trip.PlannedStart,
                    Scheduled = (int)TripStatus.Scheduled,
                    DriverAssigned = (int)TripStatus.DriverAssigned,
                    VehicleAssigned = (int)TripStatus.VehicleAssigned,
                    Started = (int)TripStatus.Started,
                    AtPickup = (int)TripStatus.AtPickup,
                    Enroute = (int)TripStatus.Enroute,
                    Delayed = (int)TripStatus.Delayed
                },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (tripConflict)
            {
                tx.Rollback();
                return TripMutationResult.Fail("Vehicle has a conflicting trip at this time.");
            }

            var nextStatus = TripLifecycle.ResolveAssignmentStatus(current, hasDriver: trip.DriverId.HasValue, hasVehicle: true);

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Trips SET VehicleId = @VehicleId, Status = @Status,
                    UpdatedAt = GETUTCDATE(), UpdatedBy = @UpdatedBy
                WHERE Id = @Id AND TenantId = @TenantId
                """,
                new
                {
                    VehicleId = vehicleId,
                    Status = (int)nextStatus,
                    UpdatedBy = actor,
                    Id = tripId,
                    TenantId = tenantId
                },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (nextStatus != current)
                await TripLifecycleSql.RecordStatusAsync(connection, tx, tripId, current, nextStatus, actor, "Vehicle assigned", cancellationToken);

            if (trip.DriverId is int driverId)
            {
                await TripLifecycleSql.EnsureAssignmentHistoryAsync(
                    connection, tx, tenantId, tripId, trip.BookingId,
                    driverId, vehicleId, trip.Pickup, trip.Dest, actor, cancellationToken);
            }

            tx.Commit();
            return TripMutationResult.Ok();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<TripFromBookingSeedResult> GetCreateFromBookingSeedAsync(
        int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var existing = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 Id FROM Trips WHERE BookingId = @BookingId AND TenantId = @TenantId AND IsDeleted = 0",
            new { BookingId = bookingId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (existing is int existingId)
            return new TripFromBookingSeedResult(existingId, null);

        var booking = await connection.QuerySingleOrDefaultAsync<BookingSeedRow>(new CommandDefinition("""
            SELECT b.CustomerId, b.RouteId, b.VehicleId, b.DriverId, b.PickupTime, b.DropoffTime,
                   b.PassengerCount, b.Notes, b.BookingNumber,
                   b.PickupAddress, b.PickupLat, b.PickupLng, b.DropoffAddress, b.DropLat, b.DropLng,
                   r.Source, r.Destination, r.Distance
            FROM Bookings b
            LEFT JOIN Routes r ON b.RouteId = r.Id
            WHERE b.Id = @Id AND b.TenantId = @TenantId AND b.IsDeleted = 0
            """,
            new { Id = bookingId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (booking is null)
            throw new NotFoundException("Booking", bookingId);

        var pickupAddr = booking.PickupAddress ?? booking.Source;
        var dropAddr = booking.DropoffAddress ?? booking.Destination;
        var (pickupLat, pickupLng) = DriverAppGeo.ResolveCoords(
            booking.PickupLat, booking.PickupLng, pickupAddr);
        var (dropLat, dropLng) = DriverAppGeo.ResolveCoords(
            booking.DropLat, booking.DropLng, dropAddr);

        var create = new CreateTripDto(
            TripName: $"Trip for {booking.BookingNumber ?? $"Booking {bookingId}"}",
            TripType: TripType.Transfer,
            BookingId: bookingId,
            CustomerId: booking.CustomerId,
            RouteId: booking.RouteId,
            PassengerCount: booking.PassengerCount,
            Priority: TripPriority.Normal,
            PickupAddress: pickupAddr,
            PickupLatitude: pickupLat,
            PickupLongitude: pickupLng,
            DestinationAddress: dropAddr,
            DestinationLatitude: dropLat,
            DestinationLongitude: dropLng,
            TripDate: booking.PickupTime.Date,
            PlannedStart: booking.PickupTime,
            PlannedEnd: booking.DropoffTime,
            EstimatedDurationMinutes: null,
            PlannedDistanceKm: booking.Distance,
            DriverNotes: booking.Notes,
            DriverId: booking.DriverId,
            AssistantDriverId: null,
            VehicleId: booking.VehicleId,
            Stops: null);

        return new TripFromBookingSeedResult(null, create);
    }

    public async Task EnsureTripExistsAsync(int tripId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var ok = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { Id = tripId, TenantId = tenantId },
            cancellationToken: cancellationToken));
        if (!ok) throw new NotFoundException("Trip", tripId);
    }

    public async Task<int> AddExpenseAsync(int tripId, CreateTripExpenseDto expense, CancellationToken cancellationToken = default)
    {
        await EnsureTripExistsAsync(tripId, cancellationToken);
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TripExpenses (TripId, ExpenseType, Amount, Description, ExpenseDate, CreatedAt, CreatedBy, IsDeleted)
            VALUES (@TripId, @ExpenseType, @Amount, @Description, @ExpenseDate, GETUTCDATE(), @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new
            {
                TripId = tripId,
                expense.ExpenseType,
                expense.Amount,
                expense.Description,
                ExpenseDate = expense.ExpenseDate ?? DateTime.UtcNow,
                CreatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));
    }

    public async Task SoftDeleteExpenseAsync(int tripId, int expenseId, CancellationToken cancellationToken = default)
    {
        await EnsureTripExistsAsync(tripId, cancellationToken);
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TripExpenses SET IsDeleted = 1 WHERE Id = @ExpenseId AND TripId = @TripId AND IsDeleted = 0",
            new { ExpenseId = expenseId, TripId = tripId },
            cancellationToken: cancellationToken));
    }

    public async Task<int> AddPassengerAsync(int tripId, CreateTripPassengerDto passenger, CancellationToken cancellationToken = default)
    {
        await EnsureTripExistsAsync(tripId, cancellationToken);
        using var connection = dbFactory.CreateConnection();
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TripPassengers (TripId, FullName, Phone, BoardingStatus, DropStatus, Notes, CreatedAt, IsDeleted)
            VALUES (@TripId, @FullName, @Phone, N'Pending', N'Pending', @Notes, GETUTCDATE(), 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new { TripId = tripId, passenger.FullName, passenger.Phone, passenger.Notes },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Trips SET PassengerCount = (
                SELECT COUNT(*) FROM TripPassengers WHERE TripId = @TripId AND IsDeleted = 0
            ), UpdatedAt = GETUTCDATE()
            WHERE Id = @TripId
            """, new { TripId = tripId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdatePassengerAsync(int tripId, int passengerId, UpdateTripPassengerDto passenger, CancellationToken cancellationToken = default)
    {
        await EnsureTripExistsAsync(tripId, cancellationToken);
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TripPassengers SET
                FullName = @FullName, Phone = @Phone,
                BoardingStatus = @BoardingStatus, DropStatus = @DropStatus,
                Notes = @Notes, UpdatedAt = GETUTCDATE()
            WHERE Id = @PassengerId AND TripId = @TripId AND IsDeleted = 0
            """,
            new
            {
                PassengerId = passengerId,
                TripId = tripId,
                passenger.FullName,
                passenger.Phone,
                passenger.BoardingStatus,
                passenger.DropStatus,
                passenger.Notes
            },
            cancellationToken: cancellationToken));
    }

    public async Task SoftDeletePassengerAsync(int tripId, int passengerId, CancellationToken cancellationToken = default)
    {
        await EnsureTripExistsAsync(tripId, cancellationToken);
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TripPassengers SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @PassengerId AND TripId = @TripId AND IsDeleted = 0",
            new { PassengerId = passengerId, TripId = tripId },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Trips SET PassengerCount = (
                SELECT COUNT(*) FROM TripPassengers WHERE TripId = @TripId AND IsDeleted = 0
            ), UpdatedAt = GETUTCDATE()
            WHERE Id = @TripId
            """, new { TripId = tripId }, cancellationToken: cancellationToken));
    }

    public async Task<int> AddDocumentAsync(
        int tripId, string documentType, string fileName, string storageKey, string? uploadedBy, CancellationToken cancellationToken = default)
    {
        await EnsureTripExistsAsync(tripId, cancellationToken);
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TripDocuments (TripId, DocumentType, FileName, StorageKey, UploadedBy, CreatedAt, IsDeleted)
            VALUES (@TripId, @DocumentType, @FileName, @StorageKey, @UploadedBy, GETUTCDATE(), 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new { TripId = tripId, DocumentType = documentType, FileName = fileName, StorageKey = storageKey, UploadedBy = uploadedBy },
            cancellationToken: cancellationToken));
    }

    public async Task SoftDeleteDocumentAsync(int tripId, int documentId, CancellationToken cancellationToken = default)
    {
        await EnsureTripExistsAsync(tripId, cancellationToken);
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TripDocuments SET IsDeleted = 1 WHERE Id = @DocumentId AND TripId = @TripId AND IsDeleted = 0",
            new { DocumentId = documentId, TripId = tripId },
            cancellationToken: cancellationToken));
    }

    public async Task<TripDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var today = DateTime.UtcNow.Date;

        return await connection.QuerySingleAsync<TripDashboardDto>(new CommandDefinition("""
            SELECT
                COUNT(*) AS TotalTrips,
                SUM(CASE WHEN Status IN (@Scheduled, @DriverAssigned, @VehicleAssigned) THEN 1 ELSE 0 END) AS ScheduledTrips,
                SUM(CASE WHEN Status IN (@Started, @AtPickup, @Enroute, @Delayed) THEN 1 ELSE 0 END) AS OngoingTrips,
                SUM(CASE WHEN Status = @Completed THEN 1 ELSE 0 END) AS CompletedTrips,
                SUM(CASE WHEN Status = @Cancelled THEN 1 ELSE 0 END) AS CancelledTrips,
                SUM(CASE WHEN Status = @Delayed THEN 1 ELSE 0 END) AS DelayedTrips,
                SUM(CASE WHEN TripDate = @Today THEN 1 ELSE 0 END) AS TodaysTrips,
                SUM(CASE WHEN TripDate > @Today AND Status NOT IN (@Completed, @Cancelled, @Failed) THEN 1 ELSE 0 END) AS UpcomingTrips
            FROM Trips
            WHERE IsDeleted = 0 AND TenantId = @TenantId
            """,
            new
            {
                TenantId = tenantId,
                Today = today,
                Scheduled = (int)TripStatus.Scheduled,
                DriverAssigned = (int)TripStatus.DriverAssigned,
                VehicleAssigned = (int)TripStatus.VehicleAssigned,
                Started = (int)TripStatus.Started,
                AtPickup = (int)TripStatus.AtPickup,
                Enroute = (int)TripStatus.Enroute,
                Delayed = (int)TripStatus.Delayed,
                Completed = (int)TripStatus.Completed,
                Cancelled = (int)TripStatus.Cancelled,
                Failed = (int)TripStatus.Failed
            },
            cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TripListItemDto>> GetPagedAsync(
        int page,
        int pageSize,
        TripStatus? status,
        int? driverId,
        int? vehicleId,
        int? routeId,
        int? customerId,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? search,
        bool todayOnly,
        bool tomorrowOnly,
        bool upcomingOnly,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var offset = (page - 1) * pageSize;

        var clauses = new List<string> { "t.IsDeleted = 0", "t.TenantId = @TenantId" };
        if (status.HasValue) clauses.Add("t.Status = @Status");
        if (driverId.HasValue) clauses.Add("t.DriverId = @DriverId");
        if (vehicleId.HasValue) clauses.Add("t.VehicleId = @VehicleId");
        if (routeId.HasValue) clauses.Add("t.RouteId = @RouteId");
        if (customerId.HasValue) clauses.Add("t.CustomerId = @CustomerId");
        if (!string.IsNullOrWhiteSpace(search))
            clauses.Add("(t.TripNumber LIKE @SearchPattern OR t.TripName LIKE @SearchPattern OR c.FullName LIKE @SearchPattern OR b.BookingNumber LIKE @SearchPattern)");

        DateTime? effectiveFrom = dateFrom;
        DateTime? dateToExclusive = dateTo?.Date.AddDays(1);
        if (todayOnly)
        {
            effectiveFrom = DateTime.UtcNow.Date;
            dateToExclusive = effectiveFrom.Value.AddDays(1);
        }
        else if (tomorrowOnly)
        {
            effectiveFrom = DateTime.UtcNow.Date.AddDays(1);
            dateToExclusive = effectiveFrom.Value.AddDays(1);
        }
        else if (upcomingOnly)
        {
            effectiveFrom = DateTime.UtcNow.Date.AddDays(1);
            dateToExclusive = null;
            clauses.Add("t.Status NOT IN (@Completed, @Cancelled, @Failed)");
        }

        if (effectiveFrom.HasValue) clauses.Add("t.TripDate >= @DateFrom");
        if (dateToExclusive.HasValue) clauses.Add("t.TripDate < @DateTo");

        var parameters = new DynamicParameters(new
        {
            Offset = offset,
            PageSize = pageSize,
            TenantId = tenantId,
            Status = (int?)status,
            DriverId = driverId,
            VehicleId = vehicleId,
            RouteId = routeId,
            CustomerId = customerId,
            SearchPattern = $"%{search}%",
            DateFrom = effectiveFrom,
            DateTo = dateToExclusive,
            Completed = (int)TripStatus.Completed,
            Cancelled = (int)TripStatus.Cancelled,
            Failed = (int)TripStatus.Failed
        });

        if (currentUser.UserId is int userId)
        {
            var scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);
            DataScopeSqlBuilder.ApplyLinkedFleetScope(parameters, scope, clauses, "v", "d");
        }

        var where = "WHERE " + string.Join(" AND ", clauses);

        var items = await connection.QueryAsync<TripListItemDto>(new CommandDefinition(
            $"{TripSql.ListSelect} {where} ORDER BY t.PlannedStart DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            parameters,
            cancellationToken: cancellationToken));

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $@"SELECT COUNT(*) FROM Trips t
               LEFT JOIN Customers c ON t.CustomerId = c.Id
               LEFT JOIN Bookings b ON t.BookingId = b.Id
               LEFT JOIN Drivers d ON t.DriverId = d.Id
               LEFT JOIN Vehicles v ON t.VehicleId = v.Id
               {where}",
            parameters,
            cancellationToken: cancellationToken));

        return new PagedResult<TripListItemDto>
        {
            Items = items.ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<TripDetailLoadResult> GetDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var row = await connection.QuerySingleOrDefaultAsync<TripDetailRow>(new CommandDefinition("""
            SELECT t.Id, t.TripNumber, t.BookingId, b.BookingNumber,
                   t.CustomerId, c.FullName AS CustomerName,
                   t.RouteId,
                   COALESCE(NULLIF(r.Name, ''), r.Source + N' → ' + r.Destination) AS RouteName,
                   t.TripName, t.TripType,
                   t.PickupAddress, t.PickupLatitude, t.PickupLongitude,
                   t.DestinationAddress, t.DestinationLatitude, t.DestinationLongitude,
                   t.TripDate, t.PlannedStart, t.PlannedEnd, t.EstimatedDurationMinutes,
                   t.DriverId, d.FullName AS DriverName,
                   t.AssistantDriverId, ad.FullName AS AssistantDriverName,
                   t.VehicleId, v.Name AS VehicleName,
                   t.PassengerCount, t.Priority, t.Status, t.DriverNotes,
                   t.PlannedDistanceKm, t.ActualDistanceKm, t.ActualStart, t.ActualEnd,
                   t.CancellationReason, t.CreatedAt,
                   CAST(CASE WHEN t.VehicleId IS NOT NULL AND EXISTS (
                       SELECT 1 FROM GpsDevices gd
                       WHERE gd.VehicleId = t.VehicleId AND gd.IsDeleted = 0
                         AND gd.LastSeenAt >= DATEADD(minute, -15, GETUTCDATE())
                   ) THEN 1 ELSE 0 END AS BIT) AS GpsOnline
            FROM Trips t
            LEFT JOIN Bookings b ON t.BookingId = b.Id
            LEFT JOIN Customers c ON t.CustomerId = c.Id
            LEFT JOIN Routes r ON t.RouteId = r.Id
            LEFT JOIN Drivers d ON t.DriverId = d.Id
            LEFT JOIN Drivers ad ON t.AssistantDriverId = ad.Id
            LEFT JOIN Vehicles v ON t.VehicleId = v.Id
            WHERE t.Id = @Id AND t.TenantId = @TenantId AND t.IsDeleted = 0
            """,
            new { Id = id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (row is null)
            throw new NotFoundException("Trip", id);

        var stops = (await connection.QueryAsync<TripStopDto>(new CommandDefinition(
            @"SELECT Id, Sequence, Location, Latitude, Longitude, Eta, ArrivalTime, DepartureTime
              FROM TripStops WHERE TripId = @Id AND IsDeleted = 0 ORDER BY Sequence",
            new { Id = id },
            cancellationToken: cancellationToken))).ToList();

        var timeline = (await connection.QueryAsync<TripStatusHistoryDto>(new CommandDefinition(
            @"SELECT Id, FromStatus, ToStatus, ChangedAtUtc, ChangedBy, Note
              FROM TripStatusHistory WHERE TripId = @Id ORDER BY ChangedAtUtc, Id",
            new { Id = id },
            cancellationToken: cancellationToken))).ToList();

        var expenses = (await connection.QueryAsync<TripExpenseDto>(new CommandDefinition(
            @"SELECT Id, ExpenseType, Amount, Description, ExpenseDate, CreatedAt
              FROM TripExpenses WHERE TripId = @Id AND IsDeleted = 0 ORDER BY ExpenseDate DESC, Id DESC",
            new { Id = id },
            cancellationToken: cancellationToken))).ToList();

        var docs = (await connection.QueryAsync<(int Id, string DocumentType, string FileName, string StorageKey, string? UploadedBy, DateTime CreatedAt)>(
            new CommandDefinition(
                @"SELECT Id, DocumentType, FileName, StorageKey, UploadedBy, CreatedAt
                  FROM TripDocuments WHERE TripId = @Id AND IsDeleted = 0 ORDER BY CreatedAt DESC",
                new { Id = id },
                cancellationToken: cancellationToken))).ToList();

        var passengers = (await connection.QueryAsync<TripPassengerDto>(new CommandDefinition(
            @"SELECT Id, FullName, Phone, BoardingStatus, DropStatus, Notes
              FROM TripPassengers WHERE TripId = @Id AND IsDeleted = 0 ORDER BY Id",
            new { Id = id },
            cancellationToken: cancellationToken))).ToList();

        var openAlerts = 0;
        if (row.VehicleId is int vehicleId)
        {
            openAlerts = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM GpsAlertEvents
                WHERE VehicleId = @VehicleId AND IsDeleted = 0
                  AND (Status IS NULL OR LOWER(Status) = N'active')
                  AND Timestamp >= DATEADD(day, -7, GETUTCDATE())
                """,
                new { VehicleId = vehicleId },
                cancellationToken: cancellationToken));
        }

        var detail = new TripDetailDto(
            row.Id, row.TripNumber, row.BookingId, row.BookingNumber,
            row.CustomerId, row.CustomerName, row.RouteId, row.RouteName,
            row.TripName, (TripType)row.TripType,
            row.PickupAddress, row.PickupLatitude, row.PickupLongitude,
            row.DestinationAddress, row.DestinationLatitude, row.DestinationLongitude,
            row.TripDate, row.PlannedStart, row.PlannedEnd, row.EstimatedDurationMinutes,
            row.DriverId, row.DriverName, row.AssistantDriverId, row.AssistantDriverName,
            row.VehicleId, row.VehicleName, row.PassengerCount,
            (TripPriority)row.Priority, (TripStatus)row.Status, row.DriverNotes,
            row.PlannedDistanceKm, row.ActualDistanceKm, row.ActualStart, row.ActualEnd,
            row.CancellationReason, row.GpsOnline, row.CreatedAt, stops, timeline,
            expenses, Array.Empty<TripDocumentDto>(), passengers, openAlerts);

        return new TripDetailLoadResult(detail, docs);
    }

    public async Task<IReadOnlyList<TripCalendarItemDto>> GetCalendarAsync(
        DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var fromDate = from.Date;
        var toExclusive = to.Date.AddDays(1);

        var items = (await connection.QueryAsync<TripCalendarItemDto>(new CommandDefinition("""
            SELECT t.Id, t.TripNumber, t.TripName, t.TripDate, t.PlannedStart, t.PlannedEnd,
                   t.Status, c.FullName AS CustomerName, d.FullName AS DriverName,
                   v.Name AS VehicleName, t.Priority
            FROM Trips t
            LEFT JOIN Customers c ON t.CustomerId = c.Id
            LEFT JOIN Drivers d ON t.DriverId = d.Id
            LEFT JOIN Vehicles v ON t.VehicleId = v.Id
            WHERE t.IsDeleted = 0 AND t.TenantId = @TenantId
              AND t.TripDate >= @From AND t.TripDate < @ToExclusive
            ORDER BY t.TripDate, t.PlannedStart
            """,
            new { TenantId = tenantId, From = fromDate, ToExclusive = toExclusive },
            cancellationToken: cancellationToken))).ToList();

        return items;
    }

    public async Task<IReadOnlyList<TripListItemDto>> GetLiveAsync(
        bool todayOnly, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var today = DateTime.UtcNow.Date;

        var where = """
            WHERE t.IsDeleted = 0 AND t.TenantId = @TenantId
              AND t.Status NOT IN (@Completed, @Cancelled, @Failed)
              AND (
                    t.Status IN (@Started, @AtPickup, @Enroute, @Delayed)
                 OR (@TodayOnly = 0)
                 OR (t.TripDate = @Today)
              )
            """;

        var items = (await connection.QueryAsync<TripListItemDto>(new CommandDefinition(
            $"{TripSql.ListSelect} {where} ORDER BY t.PlannedStart",
            new
            {
                TenantId = tenantId,
                Today = today,
                TodayOnly = todayOnly ? 1 : 0,
                Started = (int)TripStatus.Started,
                AtPickup = (int)TripStatus.AtPickup,
                Enroute = (int)TripStatus.Enroute,
                Delayed = (int)TripStatus.Delayed,
                Completed = (int)TripStatus.Completed,
                Cancelled = (int)TripStatus.Cancelled,
                Failed = (int)TripStatus.Failed
            },
            cancellationToken: cancellationToken))).ToList();

        return items;
    }

    public async Task<TripAnalyticsDto> GetAnalyticsAsync(
        DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var toDate = (to ?? DateTime.UtcNow).Date;
        var fromDate = (from ?? toDate.AddDays(-29)).Date;
        var toExclusive = toDate.AddDays(1);

        var summary = await connection.QuerySingleAsync<(
            int TotalTrips,
            int CompletedTrips,
            int CancelledTrips,
            int DelayedTrips,
            int OngoingTrips,
            decimal? TotalPlannedDistanceKm,
            decimal? TotalActualDistanceKm)>(new CommandDefinition("""
            SELECT
                COUNT(*) AS TotalTrips,
                SUM(CASE WHEN Status = @Completed THEN 1 ELSE 0 END) AS CompletedTrips,
                SUM(CASE WHEN Status = @Cancelled THEN 1 ELSE 0 END) AS CancelledTrips,
                SUM(CASE WHEN Status = @Delayed THEN 1 ELSE 0 END) AS DelayedTrips,
                SUM(CASE WHEN Status IN (@Started, @AtPickup, @Enroute, @Delayed) THEN 1 ELSE 0 END) AS OngoingTrips,
                SUM(PlannedDistanceKm) AS TotalPlannedDistanceKm,
                SUM(ActualDistanceKm) AS TotalActualDistanceKm
            FROM Trips
            WHERE IsDeleted = 0 AND TenantId = @TenantId
              AND TripDate >= @From AND TripDate < @ToExclusive
            """,
            new
            {
                TenantId = tenantId,
                From = fromDate,
                ToExclusive = toExclusive,
                Completed = (int)TripStatus.Completed,
                Cancelled = (int)TripStatus.Cancelled,
                Delayed = (int)TripStatus.Delayed,
                Started = (int)TripStatus.Started,
                AtPickup = (int)TripStatus.AtPickup,
                Enroute = (int)TripStatus.Enroute
            },
            cancellationToken: cancellationToken));

        var totalExpenses = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT COALESCE(SUM(e.Amount), 0)
            FROM TripExpenses e
            INNER JOIN Trips t ON t.Id = e.TripId
            WHERE e.IsDeleted = 0 AND t.IsDeleted = 0 AND t.TenantId = @TenantId
              AND t.TripDate >= @From AND t.TripDate < @ToExclusive
            """,
            new { TenantId = tenantId, From = fromDate, ToExclusive = toExclusive },
            cancellationToken: cancellationToken));

        var byStatus = (await connection.QueryAsync<TripNamedCountDto>(new CommandDefinition("""
            SELECT CAST(Status AS nvarchar(32)) AS Name, COUNT(*) AS Count
            FROM Trips
            WHERE IsDeleted = 0 AND TenantId = @TenantId
              AND TripDate >= @From AND TripDate < @ToExclusive
            GROUP BY Status
            ORDER BY Count DESC
            """,
            new { TenantId = tenantId, From = fromDate, ToExclusive = toExclusive },
            cancellationToken: cancellationToken))).ToList();

        byStatus = byStatus.Select(x =>
        {
            if (int.TryParse(x.Name, out var n) && Enum.IsDefined(typeof(TripStatus), n))
                return new TripNamedCountDto(((TripStatus)n).ToString(), x.Count);
            return x;
        }).ToList();

        var byType = (await connection.QueryAsync<(int Type, int Count)>(new CommandDefinition("""
            SELECT TripType AS Type, COUNT(*) AS Count
            FROM Trips
            WHERE IsDeleted = 0 AND TenantId = @TenantId
              AND TripDate >= @From AND TripDate < @ToExclusive
            GROUP BY TripType
            ORDER BY Count DESC
            """,
            new { TenantId = tenantId, From = fromDate, ToExclusive = toExclusive },
            cancellationToken: cancellationToken)))
            .Select(x => new TripNamedCountDto(
                Enum.IsDefined(typeof(TripType), x.Type) ? ((TripType)x.Type).ToString() : x.Type.ToString(),
                x.Count))
            .ToList();

        var byDriver = (await connection.QueryAsync<TripNamedCountDto>(new CommandDefinition("""
            SELECT COALESCE(d.FullName, N'Unassigned') AS Name, COUNT(*) AS Count
            FROM Trips t
            LEFT JOIN Drivers d ON t.DriverId = d.Id
            WHERE t.IsDeleted = 0 AND t.TenantId = @TenantId
              AND t.TripDate >= @From AND t.TripDate < @ToExclusive
            GROUP BY COALESCE(d.FullName, N'Unassigned')
            ORDER BY Count DESC
            """,
            new { TenantId = tenantId, From = fromDate, ToExclusive = toExclusive },
            cancellationToken: cancellationToken))).Take(10).ToList();

        var byVehicle = (await connection.QueryAsync<TripNamedCountDto>(new CommandDefinition("""
            SELECT COALESCE(v.Name, N'Unassigned') AS Name, COUNT(*) AS Count
            FROM Trips t
            LEFT JOIN Vehicles v ON t.VehicleId = v.Id
            WHERE t.IsDeleted = 0 AND t.TenantId = @TenantId
              AND t.TripDate >= @From AND t.TripDate < @ToExclusive
            GROUP BY COALESCE(v.Name, N'Unassigned')
            ORDER BY COUNT(*) DESC
            """,
            new { TenantId = tenantId, From = fromDate, ToExclusive = toExclusive },
            cancellationToken: cancellationToken))).Take(10).ToList();

        var completionRate = summary.TotalTrips == 0
            ? 0m
            : Math.Round(100m * summary.CompletedTrips / summary.TotalTrips, 1);

        return new TripAnalyticsDto(
            fromDate,
            toDate,
            summary.TotalTrips,
            summary.CompletedTrips,
            summary.CancelledTrips,
            summary.DelayedTrips,
            summary.OngoingTrips,
            completionRate,
            summary.TotalPlannedDistanceKm,
            summary.TotalActualDistanceKm,
            totalExpenses,
            byStatus,
            byType,
            byDriver,
            byVehicle);
    }

    public async Task<TripRouteSummaryDto> GetRouteSummaryAsync(int tripId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var trip = await connection.QuerySingleOrDefaultAsync<TripRouteRow>(new CommandDefinition("""
            SELECT t.Id, t.TripNumber, t.RouteId,
                   COALESCE(NULLIF(r.Name, ''), r.Source + N' → ' + r.Destination) AS RouteName,
                   r.Distance AS RouteDistanceKm, r.EstimatedMinutes AS RouteEstimatedMinutes,
                   t.PickupAddress, t.PickupLatitude, t.PickupLongitude,
                   t.DestinationAddress, t.DestinationLatitude, t.DestinationLongitude,
                   t.PlannedDistanceKm, t.EstimatedDurationMinutes, t.ActualDistanceKm,
                   t.PlannedStart, t.PlannedEnd, t.ActualStart, t.Status, t.VehicleId
            FROM Trips t
            LEFT JOIN Routes r ON t.RouteId = r.Id
            WHERE t.Id = @Id AND t.TenantId = @TenantId AND t.IsDeleted = 0
            """,
            new { Id = tripId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (trip is null)
            throw new NotFoundException("Trip", tripId);

        double? liveLat = null, liveLng = null, liveSpeed = null;
        DateTime? liveAt = null;
        bool? ignition = null;
        if (trip.VehicleId is int vehicleId)
        {
            var live = await connection.QuerySingleOrDefaultAsync<(double Latitude, double Longitude, decimal? Speed, bool? Ignition, DateTime LastUpdate)>(
                new CommandDefinition("""
                    SELECT Latitude, Longitude, Speed, Ignition, LastUpdate
                    FROM VehicleCurrentLocation WHERE VehicleId = @VehicleId
                    """,
                    new { VehicleId = vehicleId },
                    cancellationToken: cancellationToken));

            if (live.LastUpdate != default)
            {
                liveLat = live.Latitude;
                liveLng = live.Longitude;
                liveSpeed = live.Speed.HasValue ? (double)live.Speed.Value : null;
                ignition = live.Ignition;
                liveAt = live.LastUpdate;
            }
        }

        var plannedKm = trip.PlannedDistanceKm
            ?? trip.RouteDistanceKm
            ?? (decimal?)HaversineKm(
                trip.PickupLatitude, trip.PickupLongitude,
                trip.DestinationLatitude, trip.DestinationLongitude);

        var estimatedMinutes = trip.EstimatedDurationMinutes
            ?? trip.RouteEstimatedMinutes
            ?? (plannedKm.HasValue ? (int)Math.Ceiling((double)plannedKm.Value / AvgSpeedKmh * 60d) : null);

        decimal? remainingKm = null;
        int? etaMinutes = null;
        if (liveLat.HasValue && liveLng.HasValue
            && trip.DestinationLatitude.HasValue && trip.DestinationLongitude.HasValue)
        {
            remainingKm = (decimal?)HaversineKm(
                liveLat, liveLng, trip.DestinationLatitude, trip.DestinationLongitude);
            if (remainingKm.HasValue)
            {
                var speed = liveSpeed is > 5 ? liveSpeed.Value : AvgSpeedKmh;
                etaMinutes = (int)Math.Ceiling((double)remainingKm.Value / speed * 60d);
            }
        }

        decimal? coveredKm = null;
        if (plannedKm.HasValue && remainingKm.HasValue)
            coveredKm = Math.Max(0, plannedKm.Value - remainingKm.Value);

        var googleMapsUrl = BuildGoogleMapsUrl(
            trip.PickupLatitude, trip.PickupLongitude, trip.PickupAddress,
            trip.DestinationLatitude, trip.DestinationLongitude, trip.DestinationAddress);

        var googleDirectionsUrl = BuildGoogleMapsUrl(
            trip.PickupLatitude, trip.PickupLongitude, trip.PickupAddress,
            trip.DestinationLatitude, trip.DestinationLongitude, trip.DestinationAddress);

        return new TripRouteSummaryDto(
            trip.Id,
            trip.TripNumber,
            trip.RouteId,
            trip.RouteName,
            plannedKm,
            estimatedMinutes,
            trip.ActualDistanceKm,
            remainingKm,
            coveredKm,
            etaMinutes,
            liveLat,
            liveLng,
            liveSpeed,
            ignition,
            liveAt,
            googleMapsUrl,
            googleDirectionsUrl,
            HasCoordinates: trip.PickupLatitude.HasValue && trip.DestinationLatitude.HasValue,
            CanOptimize: trip.RouteId.HasValue
                || (trip.PickupLatitude.HasValue && trip.DestinationLatitude.HasValue));
    }

    public async Task<TripMutationResult> OptimizeRouteAsync(int tripId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var trip = await connection.QuerySingleOrDefaultAsync<OptimizeTripRow>(new CommandDefinition("""
            SELECT Id, RouteId, PickupLatitude, PickupLongitude, DestinationLatitude, DestinationLongitude, Status
            FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """,
            new { Id = tripId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (trip is null)
            throw new NotFoundException("Trip", tripId);

        if (TripLifecycle.IsTerminal(trip.Status))
            return TripMutationResult.Fail("Cannot optimize a completed or cancelled trip.");

        decimal? distanceKm = null;
        int? minutes = null;
        string source;

        if (trip.RouteId is int routeId)
        {
            var route = await connection.QuerySingleOrDefaultAsync<(decimal? Distance, int? EstimatedMinutes)>(
                new CommandDefinition(
                    "SELECT Distance, EstimatedMinutes FROM Routes WHERE Id = @Id AND IsDeleted = 0",
                    new { Id = routeId },
                    cancellationToken: cancellationToken));
            distanceKm = route.Distance;
            minutes = route.EstimatedMinutes;
            source = "linked route";
        }
        else
        {
            var hv = HaversineKm(
                trip.PickupLatitude, trip.PickupLongitude,
                trip.DestinationLatitude, trip.DestinationLongitude);
            if (hv is null)
                return TripMutationResult.Fail(
                    "Add pickup/destination coordinates or link a route before optimizing.");
            distanceKm = (decimal)hv.Value;
            minutes = (int)Math.Ceiling(hv.Value / AvgSpeedKmh * 60d);
            source = "straight-line estimate";
        }

        if (!minutes.HasValue && distanceKm.HasValue)
            minutes = (int)Math.Ceiling((double)distanceKm.Value / AvgSpeedKmh * 60d);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Trips SET
                PlannedDistanceKm = @DistanceKm,
                EstimatedDurationMinutes = @Minutes,
                UpdatedAt = GETUTCDATE(),
                UpdatedBy = @Actor
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """,
            new
            {
                Id = tripId,
                TenantId = tenantId,
                DistanceKm = distanceKm,
                Minutes = minutes,
                Actor = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return TripMutationResult.Ok(source);
    }

    private const double AvgSpeedKmh = 40d;

    private static double? HaversineKm(double? lat1, double? lon1, double? lat2, double? lon2)
    {
        if (lat1 is null || lon1 is null || lat2 is null || lon2 is null) return null;
        const double R = 6371d;
        var dLat = DegreesToRadians(lat2.Value - lat1.Value);
        var dLon = DegreesToRadians(lon2.Value - lon1.Value);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(DegreesToRadians(lat1.Value)) * Math.Cos(DegreesToRadians(lat2.Value))
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(R * c, 2);
    }

    private static double DegreesToRadians(double deg) => deg * Math.PI / 180d;

    private static string? BuildGoogleMapsUrl(
        double? plat, double? plng, string? pAddr,
        double? dlat, double? dlng, string? dAddr)
    {
        var origin = FormatPoint(plat, plng, pAddr);
        var dest = FormatPoint(dlat, dlng, dAddr);
        if (origin is null && dest is null) return null;
        if (dest is null) return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(origin!)}";
        if (origin is null) return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(dest)}";
        return $"https://www.google.com/maps/dir/?api=1&origin={Uri.EscapeDataString(origin)}&destination={Uri.EscapeDataString(dest)}";
    }

    private static string? FormatPoint(double? lat, double? lng, string? address)
    {
        if (lat.HasValue && lng.HasValue)
            return string.Create(CultureInfo.InvariantCulture, $"{lat.Value},{lng.Value}");
        return string.IsNullOrWhiteSpace(address) ? null : address.Trim();
    }

    private sealed class TripStatusRow
    {
        public int Status { get; init; }
        public int? DriverId { get; init; }
        public int? VehicleId { get; init; }
        public int? BookingId { get; init; }
    }

    private sealed class TripAssignRow
    {
        public int Status { get; init; }
        public int? VehicleId { get; init; }
        public int? DriverId { get; init; }
        public int? BookingId { get; init; }
        public string? Pickup { get; init; }
        public string? Dest { get; init; }
        public DateTime PlannedStart { get; init; }
    }

    private sealed class BookingSeedRow
    {
        public int CustomerId { get; init; }
        public int? RouteId { get; init; }
        public int? VehicleId { get; init; }
        public int? DriverId { get; init; }
        public DateTime PickupTime { get; init; }
        public DateTime? DropoffTime { get; init; }
        public int PassengerCount { get; init; }
        public string? Notes { get; init; }
        public string? BookingNumber { get; init; }
        public string? Source { get; init; }
        public string? Destination { get; init; }
        public string? PickupAddress { get; init; }
        public double? PickupLat { get; init; }
        public double? PickupLng { get; init; }
        public string? DropoffAddress { get; init; }
        public double? DropLat { get; init; }
        public double? DropLng { get; init; }
        public decimal? Distance { get; init; }
    }

    private sealed class TripDetailRow
    {
        public int Id { get; init; }
        public string TripNumber { get; init; } = "";
        public int? BookingId { get; init; }
        public string? BookingNumber { get; init; }
        public int CustomerId { get; init; }
        public string? CustomerName { get; init; }
        public int? RouteId { get; init; }
        public string? RouteName { get; init; }
        public string TripName { get; init; } = "";
        public int TripType { get; init; }
        public string? PickupAddress { get; init; }
        public double? PickupLatitude { get; init; }
        public double? PickupLongitude { get; init; }
        public string? DestinationAddress { get; init; }
        public double? DestinationLatitude { get; init; }
        public double? DestinationLongitude { get; init; }
        public DateTime TripDate { get; init; }
        public DateTime PlannedStart { get; init; }
        public DateTime? PlannedEnd { get; init; }
        public int? EstimatedDurationMinutes { get; init; }
        public int? DriverId { get; init; }
        public string? DriverName { get; init; }
        public int? AssistantDriverId { get; init; }
        public string? AssistantDriverName { get; init; }
        public int? VehicleId { get; init; }
        public string? VehicleName { get; init; }
        public int PassengerCount { get; init; }
        public int Priority { get; init; }
        public int Status { get; init; }
        public string? DriverNotes { get; init; }
        public decimal? PlannedDistanceKm { get; init; }
        public decimal? ActualDistanceKm { get; init; }
        public DateTime? ActualStart { get; init; }
        public DateTime? ActualEnd { get; init; }
        public string? CancellationReason { get; init; }
        public bool GpsOnline { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class TripRouteRow
    {
        public int Id { get; init; }
        public string TripNumber { get; init; } = "";
        public int? RouteId { get; init; }
        public string? RouteName { get; init; }
        public decimal? RouteDistanceKm { get; init; }
        public int? RouteEstimatedMinutes { get; init; }
        public string? PickupAddress { get; init; }
        public double? PickupLatitude { get; init; }
        public double? PickupLongitude { get; init; }
        public string? DestinationAddress { get; init; }
        public double? DestinationLatitude { get; init; }
        public double? DestinationLongitude { get; init; }
        public decimal? PlannedDistanceKm { get; init; }
        public int? EstimatedDurationMinutes { get; init; }
        public decimal? ActualDistanceKm { get; init; }
        public DateTime PlannedStart { get; init; }
        public DateTime? PlannedEnd { get; init; }
        public DateTime? ActualStart { get; init; }
        public TripStatus Status { get; init; }
        public int? VehicleId { get; init; }
    }

    private sealed class OptimizeTripRow
    {
        public int Id { get; init; }
        public int? RouteId { get; init; }
        public double? PickupLatitude { get; init; }
        public double? PickupLongitude { get; init; }
        public double? DestinationLatitude { get; init; }
        public double? DestinationLongitude { get; init; }
        public TripStatus Status { get; init; }
    }
}
