using Dapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverApp;
using SheikhTravelSystem.Application.Features.Drivers;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.Trips.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Trips.Commands;

public record UpdateTripStatusCommand(int Id, TripStatus Status, string? Note = null, string? CancellationReason = null)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UpdateStatus";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => Id;
}

public class UpdateTripStatusCommandValidator : AbstractValidator<UpdateTripStatusCommand>
{
    public UpdateTripStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class UpdateTripStatusCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IServiceScopeFactory scopeFactory,
    ILogger<UpdateTripStatusCommandHandler> logger)
    : IRequestHandler<UpdateTripStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTripStatusCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        DriverAssignmentValidation.OpenConnection(connection);
        using var tx = connection.BeginTransaction();
        var tenantId = tenantContext.GetRequiredTenantId();
        var actor = currentUser.UserId?.ToString();

        try
        {
            var trip = await connection.QuerySingleOrDefaultAsync<TripStatusRow>(new CommandDefinition(
                @"SELECT Status, DriverId, VehicleId, BookingId
                  FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.Id, TenantId = tenantId },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (trip is null)
                throw new NotFoundException("Trip", request.Id);

            var current = (TripStatus)trip.Status;
            if (!TripLifecycle.CanTransition(current, request.Status))
                return ApiResponse<bool>.FailResponse($"Cannot transition from {current} to {request.Status}.");

            if (request.Status == TripStatus.Cancelled && string.IsNullOrWhiteSpace(request.CancellationReason))
                return ApiResponse<bool>.FailResponse("Cancellation reason is required.");

            if (request.Status == TripStatus.Started && trip.DriverId is null)
                return ApiResponse<bool>.FailResponse("Assign a driver before starting the trip.");

            if (request.Status == TripStatus.Started && trip.VehicleId is null
                && request.Note?.StartsWith("Driver:", StringComparison.Ordinal) != true)
                return ApiResponse<bool>.FailResponse("Assign both driver and vehicle before starting the trip.");

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
                    Status = (int)request.Status,
                    UpdatedBy = actor,
                    Cancelled = (int)TripStatus.Cancelled,
                    Started = (int)TripStatus.Started,
                    Completed = (int)TripStatus.Completed,
                    Failed = (int)TripStatus.Failed,
                    request.CancellationReason,
                    request.Id,
                    TenantId = tenantId
                },
                transaction: tx,
                cancellationToken: cancellationToken));

            await TripLifecycle.RecordStatusAsync(
                connection, tx, request.Id, current, request.Status, actor,
                request.Note ?? request.CancellationReason, cancellationToken);

            await TripLifecycle.SyncResourceStatusAsync(
                connection, tx, tenantId, trip.DriverId, trip.VehicleId, request.Status, cancellationToken);

            if (request.Status is TripStatus.Completed or TripStatus.Cancelled or TripStatus.Failed)
            {
                await TripLifecycle.CloseAssignmentHistoryAsync(
                    connection, tx, tenantId, request.Id, trip.BookingId, cancellationToken);
            }

            var tripNumber = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
                "SELECT TripNumber FROM Trips WHERE Id = @Id",
                new { request.Id },
                transaction: tx,
                cancellationToken: cancellationToken));

            tx.Commit();
            logger.LogInformation("Trip {TripId} status {From} → {To}", request.Id, current, request.Status);

            // Notify after commit on a background scope so Accept/Arrived/Complete
            // stay under the driver-app 20s timeout while Email/Browser still fire.
            QueueTripStatusNotifications(
                tenantId, request.Id, tripNumber, request.Status, request.Note, request.CancellationReason);

            return ApiResponse<bool>.SuccessResponse(true, $"Trip status updated to {request.Status}.");
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private void QueueTripStatusNotifications(
        int tenantId,
        int tripId,
        string? tripNumber,
        TripStatus status,
        string? note,
        string? cancellationReason)
    {
        NotificationDecisionRequest? payload = status switch
        {
            TripStatus.Started => new(
                "trip_started",
                $"Trip Started: {tripNumber}",
                $"Trip {tripNumber} has started.",
                NotificationType.TripStarted,
                ReferenceId: tripId,
                TenantId: tenantId,
                SuggestedPriority: 2,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
                ]),
            TripStatus.Completed => new(
                "trip_completed",
                $"Trip Completed: {tripNumber}",
                $"Trip {tripNumber} has been completed.",
                NotificationType.TripCompleted,
                ReferenceId: tripId,
                TenantId: tenantId,
                SuggestedPriority: 2,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
                ]),
            TripStatus.Delayed => new(
                "trip_delayed",
                $"Trip Delayed: {tripNumber}",
                $"Trip {tripNumber} is marked delayed." + (string.IsNullOrWhiteSpace(note) ? "" : $" Note: {note}"),
                NotificationType.TripDelayed,
                ReferenceId: tripId,
                TenantId: tenantId,
                SuggestedPriority: 3,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
                ]),
            TripStatus.Cancelled => new(
                "trip_cancelled",
                $"Trip Cancelled: {tripNumber}",
                $"Trip {tripNumber} was cancelled." + (string.IsNullOrWhiteSpace(cancellationReason) ? "" : $" Reason: {cancellationReason}"),
                NotificationType.TripCancelled,
                ReferenceId: tripId,
                TenantId: tenantId,
                SuggestedPriority: 3,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
                ]),
            TripStatus.AtPickup => new(
                "trip_driver_arriving",
                $"Driver Arriving: {tripNumber}",
                $"Driver has arrived at pickup for trip {tripNumber}.",
                NotificationType.TripDriverArriving,
                ReferenceId: tripId,
                TenantId: tenantId,
                SuggestedPriority: 2,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
                ]),
            _ => null
        };

        if (payload is null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                var engine = scope.ServiceProvider.GetRequiredService<INotificationDecisionEngine>();
                await engine.DispatchIfAllowedAsync(payload, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background trip notification failed for trip {TripId} status {Status}", tripId, status);
            }
        });
    }

    private sealed class TripStatusRow
    {
        public int Status { get; init; }
        public int? DriverId { get; init; }
        public int? VehicleId { get; init; }
        public int? BookingId { get; init; }
    }
}

public record AssignTripDriverCommand(int TripId, int DriverId, int? AssistantDriverId = null, string? DriverNotes = null)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "AssignDriver";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => TripId;
}

public class AssignTripDriverCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    INotificationDecisionEngine decisionEngine)
    : IRequestHandler<AssignTripDriverCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AssignTripDriverCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        DriverAssignmentValidation.OpenConnection(connection);
        using var tx = connection.BeginTransaction();
        var tenantId = tenantContext.GetRequiredTenantId();
        var actor = currentUser.UserId?.ToString();

        try
        {
            var trip = await connection.QuerySingleOrDefaultAsync<TripAssignRow>(new CommandDefinition(
                @"SELECT Status, VehicleId, DriverId, BookingId, PickupAddress AS Pickup, DestinationAddress AS Dest, PlannedStart
                  FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = request.TripId, TenantId = tenantId },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (trip is null)
                throw new NotFoundException("Trip", request.TripId);

            var current = (TripStatus)trip.Status;
            if (TripLifecycle.IsTerminal(current) || current is TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute)
                return ApiResponse<bool>.FailResponse("Cannot reassign driver on an active or completed trip.");

            var driverStatus = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT Status FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1",
                new { Id = request.DriverId, TenantId = tenantId },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (driverStatus is null)
                throw new NotFoundException("Driver", request.DriverId);

            if (driverStatus != (int)DriverStatus.Available && driverStatus != (int)DriverStatus.OnTrip)
                return ApiResponse<bool>.FailResponse("Driver is not available.");

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
                    request.DriverId,
                    request.TripId,
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
                return ApiResponse<bool>.FailResponse("Driver has a conflicting trip at this time.");

            var nextStatus = TripLifecycle.ResolveAssignmentStatus(current, hasDriver: true, hasVehicle: trip.VehicleId.HasValue);

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Trips SET DriverId = @DriverId, AssistantDriverId = @AssistantDriverId,
                    DriverNotes = COALESCE(@DriverNotes, DriverNotes),
                    Status = @Status, UpdatedAt = GETUTCDATE(), UpdatedBy = @UpdatedBy
                WHERE Id = @Id AND TenantId = @TenantId
                """,
                new
                {
                    request.DriverId,
                    request.AssistantDriverId,
                    request.DriverNotes,
                    Status = (int)nextStatus,
                    UpdatedBy = actor,
                    Id = request.TripId,
                    TenantId = tenantId
                },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (nextStatus != current)
                await TripLifecycle.RecordStatusAsync(connection, tx, request.TripId, current, nextStatus, actor, "Driver assigned", cancellationToken);

            if (trip.VehicleId is int vehicleId)
            {
                await TripLifecycle.EnsureAssignmentHistoryAsync(
                    connection, tx, tenantId, request.TripId, trip.BookingId,
                    request.DriverId, vehicleId, trip.Pickup, trip.Dest, actor, cancellationToken);
            }

            var tripNumber = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
                "SELECT TripNumber FROM Trips WHERE Id = @Id",
                new { Id = request.TripId },
                transaction: tx,
                cancellationToken: cancellationToken));

            tx.Commit();

            await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                "trip_driver_assigned",
                $"Driver Assigned: {tripNumber}",
                $"A driver has been assigned to trip {tripNumber}.",
                NotificationType.TripDriverAssigned,
                ReferenceId: request.TripId,
                SuggestedPriority: 2,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
                ]), cancellationToken);

            return ApiResponse<bool>.SuccessResponse(true, "Driver assigned.");
        }
        catch
        {
            tx.Rollback();
            throw;
        }
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
}

public record AssignTripVehicleCommand(int TripId, int VehicleId)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "AssignVehicle";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => TripId;
}

public class AssignTripVehicleCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<AssignTripVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AssignTripVehicleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        DriverAssignmentValidation.OpenConnection(connection);
        using var tx = connection.BeginTransaction();
        var tenantId = tenantContext.GetRequiredTenantId();
        var actor = currentUser.UserId?.ToString();

        try
        {
            var trip = await connection.QuerySingleOrDefaultAsync<TripAssignRow>(new CommandDefinition(
                @"SELECT Status, DriverId, BookingId, PickupAddress AS Pickup, DestinationAddress AS Dest, PlannedStart
                  FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = request.TripId, TenantId = tenantId },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (trip is null)
                throw new NotFoundException("Trip", request.TripId);

            var current = (TripStatus)trip.Status;
            if (TripLifecycle.IsTerminal(current) || current is TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute)
                return ApiResponse<bool>.FailResponse("Cannot reassign vehicle on an active or completed trip.");

            var vehicleStatus = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT Status FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = request.VehicleId, TenantId = tenantId },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (vehicleStatus is null)
                throw new NotFoundException("Vehicle", request.VehicleId);

            if (vehicleStatus == (int)VehicleStatus.Maintenance)
                return ApiResponse<bool>.FailResponse("Vehicle is under maintenance.");

            if (vehicleStatus != (int)VehicleStatus.Available && vehicleStatus != (int)VehicleStatus.OnTrip)
                return ApiResponse<bool>.FailResponse("Vehicle is not available.");

            var otherOpen = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM AssignmentHistory
                    WHERE VehicleId = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0
                      AND Status IN (N'Active', N'Scheduled')
                      AND (@DriverId IS NULL OR DriverId IS NULL OR DriverId <> @DriverId)
                ) THEN 1 ELSE 0 END
                """,
                new { request.VehicleId, TenantId = tenantId, DriverId = trip.DriverId },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (otherOpen)
                return ApiResponse<bool>.FailResponse("Vehicle is already assigned to another driver.");

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
                    request.VehicleId,
                    request.TripId,
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
                return ApiResponse<bool>.FailResponse("Vehicle has a conflicting trip at this time.");

            var nextStatus = TripLifecycle.ResolveAssignmentStatus(current, hasDriver: trip.DriverId.HasValue, hasVehicle: true);

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Trips SET VehicleId = @VehicleId, Status = @Status,
                    UpdatedAt = GETUTCDATE(), UpdatedBy = @UpdatedBy
                WHERE Id = @Id AND TenantId = @TenantId
                """,
                new
                {
                    request.VehicleId,
                    Status = (int)nextStatus,
                    UpdatedBy = actor,
                    Id = request.TripId,
                    TenantId = tenantId
                },
                transaction: tx,
                cancellationToken: cancellationToken));

            if (nextStatus != current)
                await TripLifecycle.RecordStatusAsync(connection, tx, request.TripId, current, nextStatus, actor, "Vehicle assigned", cancellationToken);

            if (trip.DriverId is int driverId)
            {
                await TripLifecycle.EnsureAssignmentHistoryAsync(
                    connection, tx, tenantId, request.TripId, trip.BookingId,
                    driverId, request.VehicleId, trip.Pickup, trip.Dest, actor, cancellationToken);
            }

            tx.Commit();
            return ApiResponse<bool>.SuccessResponse(true, "Vehicle assigned.");
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private sealed class TripAssignRow
    {
        public int Status { get; init; }
        public int? DriverId { get; init; }
        public int? BookingId { get; init; }
        public string? Pickup { get; init; }
        public string? Dest { get; init; }
        public DateTime PlannedStart { get; init; }
    }
}

public record CreateTripFromBookingCommand(int BookingId) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "CreateFromBooking";
    public string AuditEntityName => "Trip";
    public int? AuditEntityId => null;
}

public class CreateTripFromBookingCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ISender mediator)
    : IRequestHandler<CreateTripFromBookingCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateTripFromBookingCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var existing = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 Id FROM Trips WHERE BookingId = @BookingId AND TenantId = @TenantId AND IsDeleted = 0",
            new { request.BookingId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (existing is int existingId)
            return ApiResponse<int>.SuccessResponse(existingId, "Trip already exists for this booking.");

        var booking = await connection.QuerySingleOrDefaultAsync<BookingSeedRow>(new CommandDefinition("""
            SELECT b.CustomerId, b.RouteId, b.VehicleId, b.DriverId, b.PickupTime, b.DropoffTime,
                   b.PassengerCount, b.Notes, b.BookingNumber,
                   b.PickupAddress, b.PickupLat, b.PickupLng, b.DropoffAddress, b.DropLat, b.DropLng,
                   r.Source, r.Destination, r.Distance
            FROM Bookings b
            LEFT JOIN Routes r ON b.RouteId = r.Id
            WHERE b.Id = @Id AND b.TenantId = @TenantId AND b.IsDeleted = 0
            """,
            new { Id = request.BookingId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (booking is null)
            throw new NotFoundException("Booking", request.BookingId);

        var pickupAddr = booking.PickupAddress ?? booking.Source;
        var dropAddr = booking.DestinationAddress ?? booking.Destination;
        var (pickupLat, pickupLng) = DriverAppGeo.ResolveCoords(
            booking.PickupLat, booking.PickupLng, pickupAddr);
        var (dropLat, dropLng) = DriverAppGeo.ResolveCoords(
            booking.DropLat, booking.DropLng, dropAddr);

        var create = new CreateTripDto(
            TripName: $"Trip for {booking.BookingNumber ?? $"Booking {request.BookingId}"}",
            TripType: TripType.Transfer,
            BookingId: request.BookingId,
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

        return await mediator.Send(new CreateTripCommand(create), cancellationToken);
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
        public string? DestinationAddress { get; init; }
        public double? DropLat { get; init; }
        public double? DropLng { get; init; }
        public decimal? Distance { get; init; }
    }
}
